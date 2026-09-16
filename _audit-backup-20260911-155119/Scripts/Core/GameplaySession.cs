using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Разметка геймплея для Яндекс Игр: GameplayAPI.start() / stop().
    /// Геймплей считается идущим, когда уровень открыт и играбелен, поверх него нет оверлеев,
    /// не показывается реклама, нет паузы и вкладка в фокусе.
    /// Состояние сверяется каждый кадр, плюс сразу в колбэках фокуса и паузы:
    /// при runInBackground = 0 следующего Update может уже не быть.
    /// </summary>
    public static class GameplaySession
    {
        private static bool levelActive;
        private static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            levelActive = false;
            EnsureRunner();
        }

        /// <summary>Уровень собран и готов к ходам.</summary>
        public static void BeginLevel()
        {
            levelActive = true;
            EnsureRunner();
            Sync();
        }

        /// <summary>Победа, поражение или выход в меню.</summary>
        public static void EndLevel()
        {
            levelActive = false;
            Sync();
        }

        private static void Sync()
        {
            if (IsPlaying()) YG2.GameplayStart();
            else YG2.GameplayStop();
        }

        private static bool IsPlaying()
        {
            if (!levelActive) return false;
            if (!YG2.isFocusWindowGame || YG2.isPauseGame) return false;
            if (YG2.nowAdsShow || YG2.nowInterAdv || YG2.nowRewardAdv) return false;
            if (RewardedAds.IsBusy || InterstitialAds.IsShowing) return false;

            // Интро стрелок ещё идёт — ввод выключен, ходов игрок делать не может.
            GameManager game = GameManager.Instance;
            if (game == null || game.State != GameState.Playing || game.IsIntroPlaying) return false;

            return !ShopPanel.IsOpen
                   && !SettingsPanel.IsOpen
                   && !PlayModePanel.IsOpen
                   && !SchoolTutorialPanel.IsOpen
                   && !DailyHintsPanel.IsOpen;
        }

        private static void EnsureRunner()
        {
            Subscribe();
            if (runner != null) return;
            var go = new GameObject("GameplaySessionRunner");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            YG2.onFocusWindowGame -= HandlePlatformState;
            YG2.onFocusWindowGame += HandlePlatformState;
            YG2.onPauseGame -= HandlePlatformState;
            YG2.onPauseGame += HandlePlatformState;
        }

        /// <summary>Новая сцена: уровень заново соберёт GameManager, до этого геймплея нет.</summary>
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            levelActive = false;
            Sync();
        }

        /// <summary>
        /// Фокус и пауза приходят из SendMessage. Флаги YG2 к этому моменту уже выставлены,
        /// поэтому stop уходит здесь же, не дожидаясь кадра.
        /// </summary>
        private static void HandlePlatformState(bool _) => Sync();

        private sealed class Runner : MonoBehaviour
        {
            private void Update() => Sync();
        }
    }
}
