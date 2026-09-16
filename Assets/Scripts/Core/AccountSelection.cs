using UnityEngine;
using UnityEngine.SceneManagement;
using YG;
using YG.Insides;

namespace Unpuzzle
{
    /// <summary>
    /// П. 1.9: диалог выбора игрового аккаунта (ACCOUNT_SELECTION_DIALOG_*).
    /// На open — пауза звука и записи сейвов. На close — перечитать игрока и облако,
    /// не смешивать два прогресса; при смене сохранения выйти в меню.
    /// </summary>
    public static class AccountSelection
    {
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// Следующая подмена YG2.saves — выбор аккаунта, а не вход в пустой профиль:
        /// прогресс сессии на выбранный сейв не копируем.
        /// </summary>
        public static bool SkipNextSaveMerge { get; private set; }

        private static bool awaitingReload;
        private static Fingerprint snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            SkipNextSaveMerge = false;
            awaitingReload = false;
            snapshot = default;
        }

        public static bool ConsumeSkipNextSaveMerge()
        {
            bool skip = SkipNextSaveMerge;
            SkipNextSaveMerge = false;
            return skip;
        }

        public static void HandleDialog(string state)
        {
            if (state == "opened") HandleOpened();
            else if (state == "closed") HandleClosed();
            else if (state == "reload-failed") HandleReloadFailed();
        }

        /// <summary>Плагин перечитал сейвы после закрытия диалога.</summary>
        public static void HandleSavesReloaded()
        {
            if (!awaitingReload) return;
            FinishReload(true);
        }

        private static void HandleOpened()
        {
            snapshot = Fingerprint.Capture();
            SkipNextSaveMerge = true;
            awaitingReload = false;
            IsOpen = true;
            GameSaves.SetWritesSuspended(true);
            GameAudio.ApplySettings();
            GameplaySession.Refresh();
        }

        private static void HandleClosed()
        {
            awaitingReload = true;
            SkipNextSaveMerge = true;
#if UNITY_EDITOR
            YG2.GetAuth();
            YGInsides.LoadProgress();
            HandleSavesReloaded();
#endif
        }

        private static void HandleReloadFailed()
        {
            if (!awaitingReload && !IsOpen) return;
            FinishReload(false);
        }

        private static void FinishReload(bool compareProgress)
        {
            ConsumeSkipNextSaveMerge();
            awaitingReload = false;
            IsOpen = false;
            GameSaves.SetWritesSuspended(false);
            GameAudio.ApplySettings();
            GameplaySession.Refresh();

            bool changed = !compareProgress || !snapshot.MatchesCurrent();
            snapshot = default;
            if (!changed) return;
            if (SceneManager.GetActiveScene().name != GameConstants.GameSceneName) return;

            GameplaySession.EndLevel();
            SceneManager.LoadScene(GameConstants.MainMenuSceneName);
        }

        private struct Fingerprint
        {
            private string playerId;
            private int revision;
            private int campaignLevel;
            private int schoolLevel;
            private int gold;
            private string skin;
            private string background;

            public static Fingerprint Capture()
            {
                SavesYG data = YG2.saves;
                return new Fingerprint
                {
                    playerId = YG2.player != null ? YG2.player.id : string.Empty,
                    revision = data != null ? data.unpuzzleRevision : 0,
                    campaignLevel = data != null ? data.campaignLevel : 0,
                    schoolLevel = data != null ? data.schoolLevel : 0,
                    gold = data != null ? data.gold : 0,
                    skin = data != null ? data.selectedSkin : string.Empty,
                    background = data != null ? data.selectedBackground : string.Empty
                };
            }

            public bool MatchesCurrent()
            {
                Fingerprint now = Capture();
                return playerId == now.playerId
                    && revision == now.revision
                    && campaignLevel == now.campaignLevel
                    && schoolLevel == now.schoolLevel
                    && gold == now.gold
                    && skin == now.skin
                    && background == now.background;
            }
        }
    }
}

namespace YG.Insides
{
    public partial class YGSendMessage
    {
        public void AccountSelectionDialog(string state) => Unpuzzle.AccountSelection.HandleDialog(state);
    }
}
