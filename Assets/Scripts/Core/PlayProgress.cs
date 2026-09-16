using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Режим, школьный прогресс и уровень кампании в облачных сохранениях.
    /// </summary>
    public static class PlayProgress
    {
        /// <summary>
        /// Запрос повтора туториала живёт только в памяти: это намерение игрока на переход
        /// из меню в игру, переживать перезагрузку страницы ему незачем.
        /// </summary>
        private static bool tutorialReplayRequested;

        public static bool IsSchool => GetMode() == GameConstants.PlayModeSchool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tutorialReplayRequested = false;
        }

        public static string GetMode()
        {
            return GameSaves.Data.playMode == GameConstants.PlayModeSchool
                ? GameConstants.PlayModeSchool
                : GameConstants.PlayModeNormal;
        }

        public static void SetNormal()
        {
            GameSaves.Data.playMode = GameConstants.PlayModeNormal;
            GameSaves.SaveNow();
        }

        public static void SetSchool(int level)
        {
            GameSaves.Data.playMode = GameConstants.PlayModeSchool;
            SchoolLevel = level;
        }

        public static int SchoolLevel
        {
            get => Mathf.Clamp(GameSaves.Data.schoolLevel, 1, SchoolLevelCatalog.TargetCount);
            set
            {
                GameSaves.Data.schoolLevel = Mathf.Clamp(value, 1, SchoolLevelCatalog.TargetCount);
                GameSaves.SaveNow();
            }
        }

        public static int SchoolUnlocked
        {
            get
            {
                int unlocked = GameSaves.Data.schoolUnlocked;
                if (unlocked < 1) unlocked = 1;
                return Mathf.Min(unlocked, SchoolLevelCatalog.TargetCount);
            }
        }

        /// <summary>Уровень кампании. Ноль в сейвах означает, что кампанию ещё не начинали.</summary>
        public static int CampaignLevel => Mathf.Max(1, GameSaves.Data.campaignLevel);

        public static bool HasCampaignProgress => GameSaves.Data.campaignLevel > 0;

        /// <summary>Школьный туториал уже проходили. «?» его не сбрасывает.</summary>
        public static bool SchoolTutorialSeen => GameSaves.Data.schoolTutorialSeen;

        public static void MarkSchoolTutorialSeen()
        {
            if (GameSaves.Data.schoolTutorialSeen) return;

            GameSaves.Data.schoolTutorialSeen = true;
            GameSaves.SaveNow();
        }

        public static bool TutorialReplayPending => tutorialReplayRequested;

        /// <summary>«?» в меню: открыть туториал в GameScene (Seen не трогаем).</summary>
        public static void RequestSchoolTutorialReplay()
        {
            tutorialReplayRequested = true;
        }

        public static bool ConsumeSchoolTutorialReplay()
        {
            if (!tutorialReplayRequested) return false;

            tutorialReplayRequested = false;
            return true;
        }

        /// <summary>После победы на N открывает N+1, не выше 20 и не ниже уже открытого.</summary>
        public static void UnlockAfterWin(int wonLevel)
        {
            int want = wonLevel >= SchoolLevelCatalog.TargetCount
                ? SchoolLevelCatalog.TargetCount
                : Mathf.Clamp(wonLevel + 1, 1, SchoolLevelCatalog.TargetCount);
            if (want <= SchoolUnlocked) return;

            GameSaves.Data.schoolUnlocked = want;
            GameSaves.SaveNow();
        }

        public static void SaveActiveLevel(int level)
        {
            if (IsSchool)
            {
                SchoolLevel = level;
                return;
            }

            int value = Mathf.Max(1, level);
            if (GameSaves.Data.campaignLevel == value) return;

            GameSaves.Data.campaignLevel = value;
            GameSaves.SaveNow();
        }
    }
}
