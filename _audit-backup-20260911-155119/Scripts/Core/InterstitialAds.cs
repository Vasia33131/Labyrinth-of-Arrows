using System;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Unpuzzle
{
    public enum InterstitialGate
    {
        NextLevel,
        Restart,
        Play
    }

    /// <summary>
    /// Fullscreen только по таймеру 90 с и только на допустимом клике.
    /// Ролик сразу в onClick, без своей заставки «реклама через 2».
    /// </summary>
    public static class InterstitialAds
    {
        private static float elapsed;
        private static bool intervalReady;
        private static bool pendingShow;
        private static bool waitingClose;
        private static bool rewardedBusy;
        private static float pendingSince = -1f;
        private static Action continuation;
        private static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            elapsed = 0f;
            intervalReady = false;
            pendingShow = false;
            waitingClose = false;
            rewardedBusy = RewardedAds.IsBusy;
            pendingSince = -1f;
            continuation = null;
            EnsureRunner();
            EnsureSubscribed();
        }

        public static bool IsReady => intervalReady && !pendingShow && !IsAnyAdShowing();
        public static bool IsShowing => pendingShow || waitingClose || IsAnyAdShowing();

        /// <summary>true — показ реально запрошен у SDK, дальше ждём onOpen/onClose.</summary>
        public static bool TryShow(InterstitialGate gate)
        {
            if (!intervalReady || pendingShow) return false;
            if (IsAnyAdShowing()) return false;
            if (gate == InterstitialGate.Play && ShouldSkipPlay()) return false;

            // SDK молча проглатывает вызов, если его собственный таймер ещё идёт.
            // Без этой проверки гейт ждал бы сторожа 2 с и только потом пускал игрока дальше.
#if UNITY_EDITOR
            if (!YG2.infoYG.Simulation.enableInterAdv) return false;
#endif
#if InterstitialAdv_yg
            if (!YG2.isTimerAdvCompleted) return false;
#endif

            pendingShow = true;
            pendingSince = Time.unscaledTime;
            waitingClose = false;
            ShowNow();
            return pendingShow || waitingClose;
        }

        /// <summary>
        /// Показ на логической паузе с отложенным продолжением: смена уровня уходит в onClose,
        /// иначе новый уровень собирается и проигрывает интро под уже открытым роликом.
        /// </summary>
        public static void TryShowThen(InterstitialGate gate, Action after)
        {
            if (!TryShow(gate))
            {
                after?.Invoke();
                return;
            }

            continuation = after;
        }

        public static void MarkHasPlayed()
        {
            if (GameSaves.Data.hasPlayed) return;

            GameSaves.Data.hasPlayed = true;
            GameSaves.RequestSave();
        }

        private static void ShowNow()
        {
#if InterstitialAdv_yg
            YG2.InterstitialAdvShow();
#elif UNITY_EDITOR
            SimulateInEditor();
#else
            ClearPendingKeepReady();
#endif
        }

        private static bool ShouldSkipPlay()
        {
            int level = PlayProgress.CampaignLevel;
            if (level <= GameConstants.InterstitialSkipPlayThroughLevel) return true;
            if (!GameSaves.Data.hasPlayed) return true;
            return YG2.isFirstGameSession;
        }

        private static bool IsAnyAdShowing()
        {
            if (RewardedAds.IsBusy) return true;
            return YG2.nowAdsShow || YG2.nowInterAdv || YG2.nowRewardAdv;
        }

        private static bool ShouldPauseTimer()
        {
            if (IsAnyAdShowing() || pendingShow || waitingClose) return true;
            if (ShopPanel.IsOpen || SettingsPanel.IsOpen || PlayModePanel.IsOpen
                || SchoolTutorialPanel.IsOpen || DailyHintsPanel.IsOpen) return true;
            return false;
        }

        private static void Tick(float dt)
        {
            if (pendingShow
                && pendingSince >= 0f
                && Time.unscaledTime - pendingSince > 2f
                && !YG2.nowInterAdv
                && !waitingClose)
            {
                ClearPendingKeepReady();
            }

            // Ролика нет и уже не будет, а продолжение всё ещё висит — игрок не должен
            // застрять на кнопке «Дальше» из-за проглоченного показа.
            if (continuation != null && !pendingShow && !waitingClose && !IsAnyAdShowing())
                RunContinuation();

            bool nowRewarded = RewardedAds.IsBusy || YG2.nowRewardAdv;
            if (nowRewarded != rewardedBusy)
            {
                rewardedBusy = nowRewarded;
                ResetTimer();
                return;
            }

            if (ShouldPauseTimer()) return;
            if (intervalReady) return;

            elapsed += dt;
            if (elapsed >= GameConstants.InterstitialIntervalSeconds)
            {
                elapsed = GameConstants.InterstitialIntervalSeconds;
                intervalReady = true;
            }
        }

        private static void ResetTimer()
        {
            elapsed = 0f;
            intervalReady = false;
            pendingShow = false;
            waitingClose = false;
            pendingSince = -1f;
        }

        private static void ClearPendingKeepReady()
        {
            pendingShow = false;
            waitingClose = false;
            pendingSince = -1f;
        }

        private static void RunContinuation()
        {
            Action after = continuation;
            continuation = null;
            after?.Invoke();
        }

        private static void HandleOpened()
        {
            ResetTimer();
            waitingClose = true;
        }

        private static void HandleClosed()
        {
            ResetTimer();
            RunContinuation();
        }

        /// <summary>
        /// SDK на ошибке обнуляет свой таймер, поэтому свой откатываем на полный интервал:
        /// иначе серия неудач превращается в запрос рекламы на каждый клик.
        /// </summary>
        private static void HandleError()
        {
            ResetTimer();
            RunContinuation();
        }

        private static void EnsureSubscribed()
        {
#if InterstitialAdv_yg
            YG2.onOpenInterAdv -= HandleOpened;
            YG2.onOpenInterAdv += HandleOpened;
            YG2.onCloseInterAdv -= HandleClosed;
            YG2.onCloseInterAdv += HandleClosed;
            YG2.onErrorInterAdv -= HandleError;
            YG2.onErrorInterAdv += HandleError;
#endif
            RewardedAds.OnStateChanged -= HandleRewardedState;
            RewardedAds.OnStateChanged += HandleRewardedState;
        }

        private static void HandleRewardedState()
        {
            bool nowRewarded = RewardedAds.IsBusy || YG2.nowRewardAdv;
            if (nowRewarded == rewardedBusy) return;
            rewardedBusy = nowRewarded;
            ResetTimer();
            RunContinuation();
        }

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("InterstitialAdsRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

#if UNITY_EDITOR && !InterstitialAdv_yg
        private static void SimulateInEditor()
        {
            var go = new GameObject("InterstitialSimulation");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var sim = go.AddComponent<EditorSimulation>();
            sim.Begin(HandleOpened, HandleClosed);
        }

        private sealed class EditorSimulation : MonoBehaviour
        {
            private Action onOpened;
            private Action onClosed;
            private bool paused;

            public void Begin(Action opened, Action closed)
            {
                onOpened = opened;
                onClosed = closed;

                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767;
                gameObject.AddComponent<GraphicRaycaster>();

                var image = gameObject.AddComponent<Image>();
                image.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
                image.raycastTarget = true;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0.1f, 0.45f);
                labelRt.anchorMax = new Vector2(0.9f, 0.7f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var label = labelGo.AddComponent<Text>();
                label.font = GoldCounterView.ResolveFont();
                label.fontSize = 44;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.text = "Реклама (симуляция YG2)";
                label.raycastTarget = false;

                var btnGo = new GameObject("Close", typeof(RectTransform));
                btnGo.transform.SetParent(transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.3f, 0.22f);
                btnRt.anchorMax = new Vector2(0.7f, 0.34f);
                btnRt.offsetMin = Vector2.zero;
                btnRt.offsetMax = Vector2.zero;
                var btnImage = btnGo.AddComponent<Image>();
                btnImage.color = GameConstants.PlayBlue;
                var button = btnGo.AddComponent<Button>();
                button.targetGraphic = btnImage;
                button.onClick.AddListener(Close);

                var btnLabelGo = new GameObject("Text", typeof(RectTransform));
                btnLabelGo.transform.SetParent(btnGo.transform, false);
                var btnLabelRt = btnLabelGo.GetComponent<RectTransform>();
                btnLabelRt.anchorMin = Vector2.zero;
                btnLabelRt.anchorMax = Vector2.one;
                btnLabelRt.offsetMin = Vector2.zero;
                btnLabelRt.offsetMax = Vector2.zero;
                var btnLabel = btnLabelGo.AddComponent<Text>();
                btnLabel.font = GoldCounterView.ResolveFont();
                btnLabel.fontSize = 32;
                btnLabel.alignment = TextAnchor.MiddleCenter;
                btnLabel.color = Color.white;
                btnLabel.text = "Закрыть";
                btnLabel.raycastTarget = false;

                YG2.PauseGame(true);
                paused = true;
                YG2.onOpenAnyAdv?.Invoke();
                onOpened?.Invoke();
            }

            private void Close()
            {
                if (paused)
                {
                    YG2.PauseGame(false);
                    paused = false;
                }

                onClosed?.Invoke();
                YG2.onCloseAnyAdv?.Invoke();
                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (paused) YG2.PauseGame(false);
            }
        }
#endif

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                Tick(Time.unscaledDeltaTime);
            }
        }
    }
}
