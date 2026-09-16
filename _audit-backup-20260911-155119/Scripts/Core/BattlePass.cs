using UnityEngine;

namespace Unpuzzle
{
    /// <summary>Данные боевого пропуска, сохранённые независимо от прогресса открытия школы.</summary>
    public static class BattlePass
    {
        public const int LevelCount = GameConstants.BattlePassLevelCount;
        public const int FreeGoldPerLevel = GameConstants.BattlePassFreeGoldPerLevel;
        public const int PremiumGoldPerLevel = GameConstants.BattlePassPremiumGoldPerLevel;
        public const int PremiumPriceGold = GameConstants.BattlePassPremiumPriceGold;

        private const int CompletedMask = (1 << LevelCount) - 1;

        /// <summary>Число уникальных пройденных школ, clamp 0..20. Источник правды — биты, не отдельный счётчик.</summary>
        public static int CurrentLevel
        {
            get
            {
                SyncFromSchoolProgress();
                return CountCompletedSchools();
            }
        }

        public static bool PremiumUnlocked
        {
            get => GameSaves.Data.battlePassPremium;
            set
            {
                if (GameSaves.Data.battlePassPremium == value) return;

                GameSaves.Data.battlePassPremium = value;
                GameSaves.RequestSave();
            }
        }

        public static void RevokePremium()
        {
            if (GameSaves.Data.battlePassPremiumRevoked) return;

            GameSaves.Data.battlePassPremium = false;
            GameSaves.Data.battlePassPremiumRevoked = true;
            GameSaves.RequestSave();
        }

        public static bool TryBuyPremium()
        {
            if (PremiumUnlocked) return false;
            if (!Wallet.TrySpend(PremiumPriceGold)) return false;

            PremiumUnlocked = true;
            GameSaves.Flush();
            return true;
        }

        /// <summary>
        /// Один раз переносит уже открытую школу в биты БП.
        /// Unlocked 1 → 0 школ; 2..19 → школы 1..(unlocked-1); 20 → 1..19,
        /// 20-ю только если бит уже стоит. Золото не начисляет.
        /// </summary>
        public static void SyncFromSchoolProgress()
        {
            if (GameSaves.Data.battlePassSchoolSynced) return;

            int completed = ReadCompletedMask();
            int unlocked = PlayProgress.SchoolUnlocked;
            int through;
            if (unlocked <= 1)
                through = 0;
            else if (unlocked >= SchoolLevelCatalog.TargetCount)
                through = SchoolLevelCatalog.TargetCount - 1;
            else
                through = unlocked - 1;

            for (int schoolId = 1; schoolId <= through; schoolId++)
                completed |= BitFor(schoolId);

            GameSaves.Data.battlePassCompletedSchools = completed;
            GameSaves.Data.battlePassSchoolSynced = true;
            GameSaves.RequestSave();
        }

        public static void NotifySchoolWon(int schoolId)
        {
            if (schoolId < 1 || schoolId > SchoolLevelCatalog.TargetCount) return;

            SyncFromSchoolProgress();

            int completed = ReadCompletedMask();
            int schoolBit = BitFor(schoolId);
            if ((completed & schoolBit) != 0) return;

            GameSaves.Data.battlePassCompletedSchools = completed | schoolBit;
            GameSaves.RequestSave();
        }

        public static bool IsSchoolCompleted(int schoolId)
        {
            if (schoolId < 1 || schoolId > SchoolLevelCatalog.TargetCount) return false;

            SyncFromSchoolProgress();
            return (ReadCompletedMask() & BitFor(schoolId)) != 0;
        }

        public static bool IsFreeClaimed(int level) => IsClaimed(false, level);

        public static bool IsPremiumClaimed(int level) => IsClaimed(true, level);

        public static bool ClaimFree(int level)
        {
            if (!CanClaim(level) || IsFreeClaimed(level)) return false;

            if (IsStubFinal(level))
                Backgrounds.UnlockBackground(BackgroundCatalog.ClassroomId);
            else
                Wallet.Add(GetFreeGold(level));

            MarkClaimed(false, level);
            GameSaves.Flush();
            return true;
        }

        public static bool ClaimPremium(int level)
        {
            if (!PremiumUnlocked || !CanClaim(level) || IsPremiumClaimed(level)) return false;

            if (IsStubFinal(level))
                CharacterSkins.UnlockSkin(CharacterSkinCatalog.TeacherId);
            else
                Wallet.Add(GetPremiumGold(level));

            MarkClaimed(true, level);
            GameSaves.Flush();
            return true;
        }

        /// <summary>Финальный слот (ур. 20) — только для UI, не блокирует claim.</summary>
        public static bool IsStubFinal(int level) => level == LevelCount;

        public static int GetFreeGold(int level) =>
            level >= 1 && level < LevelCount ? FreeGoldPerLevel : 0;

        public static int GetPremiumGold(int level) =>
            level >= 1 && level < LevelCount ? PremiumGoldPerLevel : 0;

        private static bool CanClaim(int level) =>
            level >= 1 && level <= CurrentLevel;

        private static int ReadCompletedMask() =>
            GameSaves.Data.battlePassCompletedSchools & CompletedMask;

        private static int CountCompletedSchools() => CountBits(ReadCompletedMask());

        private static int CountBits(int mask)
        {
            int count = 0;
            int bits = mask & CompletedMask;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }

            return Mathf.Clamp(count, 0, LevelCount);
        }

        private static bool IsClaimed(bool premium, int level)
        {
            if (level < 1 || level > LevelCount) return false;

            int claimed = premium
                ? GameSaves.Data.battlePassClaimedPremium
                : GameSaves.Data.battlePassClaimedFree;
            return (claimed & BitFor(level)) != 0;
        }

        private static void MarkClaimed(bool premium, int level)
        {
            if (premium)
                GameSaves.Data.battlePassClaimedPremium |= BitFor(level);
            else
                GameSaves.Data.battlePassClaimedFree |= BitFor(level);

            GameSaves.RequestSave();
        }

        private static int BitFor(int oneBasedIndex) => 1 << (oneBasedIndex - 1);
    }
}
