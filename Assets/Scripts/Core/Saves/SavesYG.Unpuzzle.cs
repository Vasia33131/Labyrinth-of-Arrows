using System.Collections.Generic;
using Unpuzzle;

namespace YG
{
    /// <summary>
    /// Игровая часть облачных сохранений. Плагин сериализует класс целиком, поэтому
    /// значения по умолчанию задаются прямо здесь: сейвы, записанные до появления поля,
    /// при загрузке получат именно их.
    /// </summary>
    public partial class SavesYG
    {
        /// <summary>Растёт с каждой записью. Ноль означает аккаунт, который игру ещё не открывал.</summary>
        public int unpuzzleRevision;

        /// <summary>Разовый импорт прогресса из PlayerPrefs уже выполнен.</summary>
        public bool unpuzzleImported;

        public string playMode = GameConstants.PlayModeNormal;

        /// <summary>Ноль означает, что кампанию ещё не начинали.</summary>
        public int campaignLevel;
        public int schoolLevel = 1;
        public int schoolUnlocked = 1;
        public bool schoolTutorialSeen;

        public int gold;
        public int hintsCount;
        public int undoCount;
        public int extraMoveCount;

        /// <summary>Дата последнего ежедневного пака в формате yyyy-MM-dd по времени сервера.</summary>
        public string hintsLastClaimDate = string.Empty;

        public int battlePassCompletedSchools;
        public bool battlePassPremium;
        public int battlePassClaimedFree;
        public int battlePassClaimedPremium;
        public bool battlePassSchoolSynced;

        public List<string> unlockedSkins = new List<string>();
        public string selectedSkin = GameConstants.DefaultSkinId;
        public List<string> unlockedBackgrounds = new List<string>();
        public string selectedBackground = GameConstants.DefaultBackgroundId;

        public bool soundOn = true;
        public bool musicOn = true;

        public bool hasPlayed;

        /// <summary>Уровень, за который золото уже выдано: без этого награду фармят перезагрузкой страницы.</summary>
        public int goldAwardedLevel;
    }
}
