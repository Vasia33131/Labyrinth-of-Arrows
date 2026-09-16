using System;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Rewarded через YG2. Награда только из onRewardAdv / callback модуля.
    /// Паузу игры и звука делает YG2 — здесь её не дублируем.
    /// </summary>
    public static class RewardedAds
    {
        public const string Gold300Id = "gold_300";
        public const string Hints3Id = "hints_3";
        public const string Undo1Id = "undo_1";
        public const string ExtraMove1Id = "extra_move_1";
        /// <summary>Текст ошибки на языке игрока: не const, иначе он вмерзает в вызывающий код.</summary>
        public static string FailMessage => GameTexts.AdFailed;

        public static event Action OnStateChanged;
        public static event Action<string> OnFailed;

        /// <summary>Яндекс может прислать close раньше reward — столько ждём опоздавшую награду.</summary>
        private const float RewardGraceSeconds = 1.5f;

        /// <summary>Ролик так и не открылся: колбэков уже не будет, снимаем блокировку кнопок.</summary>
        private const float OpenTimeoutSeconds = 15f;

        private static string pendingId;
        private static bool rewardedThisShow;
        private static bool adOpened;
        private static float pendingSince = -1f;
        private static float graceUntil = -1f;
        private static Runner runner;

        public static bool IsBusy
        {
            get
            {
                if (!string.IsNullOrEmpty(pendingId)) return true;
                return YG2.nowRewardAdv || YG2.nowAdsShow;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            ClearShow();
            EnsureRunner();
            EnsureSubscribed();
        }

        public static void Show(string id)
        {
            if (string.IsNullOrEmpty(id) || IsBusy) return;
            if (!IsKnownId(id)) return;

            EnsureSubscribed();
            EnsureRunner();
            pendingId = id;
            rewardedThisShow = false;
            adOpened = false;
            pendingSince = Time.unscaledTime;
            graceUntil = -1f;
            OnStateChanged?.Invoke();

#if RewardedAdv_yg
            YG2.RewardedAdvShow(id);
#elif UNITY_EDITOR
            SimulateInEditor(id);
#else
            FinishWithoutReward();
#endif
        }

        public static void ShowGold300() => Show(Gold300Id);

        public static void ShowHints() => Show(Hints3Id);

        public static void ShowUndo() => Show(Undo1Id);

        public static void ShowExtraMove() => Show(ExtraMove1Id);

        public static void ShowBooster(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo:
                    ShowUndo();
                    return;
                case BoosterKind.ExtraMove:
                    ShowExtraMove();
                    return;
                default:
                    ShowHints();
                    return;
            }
        }

        public static string IdFor(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return Undo1Id;
                case BoosterKind.ExtraMove: return ExtraMove1Id;
                default: return Hints3Id;
            }
        }

        public static int RewardAmount(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return GameConstants.UndoPerRewardedAd;
                case BoosterKind.ExtraMove: return GameConstants.ExtraMovePerRewardedAd;
                default: return GameConstants.HintsPerRewardedAd;
            }
        }

        private static void EnsureSubscribed()
        {
#if RewardedAdv_yg
            YG2.onRewardAdv -= HandleYgReward;
            YG2.onRewardAdv += HandleYgReward;
            YG2.onCloseRewardedAdv -= HandleYgClose;
            YG2.onCloseRewardedAdv += HandleYgClose;
            YG2.onErrorRewardedAdv -= HandleYgError;
            YG2.onErrorRewardedAdv += HandleYgError;
#endif
            YG2.onCloseAnyAdv -= HandleAnyClose;
            YG2.onCloseAnyAdv += HandleAnyClose;
            YG2.onErrorAnyAdv -= HandleAnyError;
            YG2.onErrorAnyAdv += HandleAnyError;
        }

#if RewardedAdv_yg
        private static void HandleYgReward(string id) => TryGrant(id);

        private static void HandleYgClose() => FinishAfterAdClosed();

        private static void HandleYgError() => FinishWithoutReward();
#endif

        private static void HandleAnyClose()
        {
            if (string.IsNullOrEmpty(pendingId)) return;
            FinishAfterAdClosed();
        }

        private static void HandleAnyError()
        {
            if (string.IsNullOrEmpty(pendingId)) return;
            FinishWithoutReward();
        }

        private static void TryGrant(string id)
        {
            if (string.IsNullOrEmpty(pendingId) || rewardedThisShow) return;
            if (!string.IsNullOrEmpty(id) && id != pendingId) return;

            Grant(pendingId);
        }

        private static void Grant(string id)
        {
            if (rewardedThisShow) return;
            rewardedThisShow = true;

            if (id == Gold300Id)
            {
                Wallet.Add(GameConstants.GoldPerRewardedAd);
            }
            else if (id == Hints3Id)
            {
                Hints.AddHints(GameConstants.HintsPerRewardedAd);
            }
            else if (id == Undo1Id)
            {
                UndoCharges.Add(GameConstants.UndoPerRewardedAd);
            }
            else if (id == ExtraMove1Id)
            {
                ExtraMoves.Add(GameConstants.ExtraMovePerRewardedAd);
            }

            GameSaves.Flush();
            OnStateChanged?.Invoke();
        }

        private static void FinishAfterAdClosed()
        {
            if (string.IsNullOrEmpty(pendingId)) return;

            if (rewardedThisShow)
            {
                ClearShow();
                OnStateChanged?.Invoke();
                return;
            }

            // Ролик досмотрен, но onRewardAdv ещё в пути: гасить показ нельзя, иначе
            // вместо награды игрок увидит «Не удалось», а награда уйдёт в никуда.
            if (adOpened)
            {
                if (graceUntil < 0f) graceUntil = Time.unscaledTime + RewardGraceSeconds;
                return;
            }

            FinishWithoutReward();
        }

        private static void FinishWithoutReward()
        {
            if (string.IsNullOrEmpty(pendingId)) return;

            string id = pendingId;
            bool granted = rewardedThisShow;
            ClearShow();
            if (!granted) OnFailed?.Invoke(id);
            OnStateChanged?.Invoke();
        }

        private static void ClearShow()
        {
            pendingId = null;
            rewardedThisShow = false;
            adOpened = false;
            pendingSince = -1f;
            graceUntil = -1f;
        }

        /// <summary>
        /// Показ живёт на колбэках SDK, но молчание SDK не должно навсегда гасить кнопки RV:
        /// здесь и только здесь снимается залипший pendingId.
        /// </summary>
        private static void Tick()
        {
            if (string.IsNullOrEmpty(pendingId)) return;

            if (!adOpened && YG2.nowRewardAdv)
            {
                adOpened = true;
                pendingSince = -1f;
            }

            if (rewardedThisShow && !YG2.nowRewardAdv)
            {
                ClearShow();
                OnStateChanged?.Invoke();
                return;
            }

            if (adOpened)
            {
                if (!YG2.nowRewardAdv && graceUntil < 0f)
                    graceUntil = Time.unscaledTime + RewardGraceSeconds;

                if (graceUntil >= 0f && Time.unscaledTime >= graceUntil)
                    FinishWithoutReward();

                return;
            }

            if (pendingSince >= 0f && Time.unscaledTime - pendingSince > OpenTimeoutSeconds)
                FinishWithoutReward();
        }

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("RewardedAdsRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        private static bool IsKnownId(string id)
        {
            return id == Gold300Id || id == Hints3Id || id == Undo1Id || id == ExtraMove1Id;
        }

#if UNITY_EDITOR && !RewardedAdv_yg
        private static void SimulateInEditor(string id)
        {
            bool watched = UnityEditor.EditorUtility.DisplayDialog(
                "Реклама (симуляция YG2)",
                "Засчитать успешный просмотр ролика?\n\nПросмотрел — награда.\nЗакрыть — без награды.",
                "Просмотрел",
                "Закрыть без награды");

            if (watched) TryGrant(id);
            FinishAfterAdClosed();
        }
#endif

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }
        }
    }
}
