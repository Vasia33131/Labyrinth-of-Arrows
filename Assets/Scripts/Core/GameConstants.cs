using UnityEngine;

namespace Unpuzzle
{
    public static class GameConstants
    {
        public const string ArrowLayerName = "Arrow";
        public const string ButtonLayerName = "ButtonSwitch";

        public const string LevelsResourcesPath = "Levels";
        public const string SchoolLevelsResourcesPath = "Levels/School";
        public const string GameSceneName = "GameScene";
        public const string MainMenuSceneName = "MainMenu";

        public const string PlayModeNormal = "Normal";
        public const string PlayModeSchool = "School";
        public const int BattlePassLevelCount = 20;
        public const int BattlePassFreeGoldPerLevel = 100;
        public const int BattlePassPremiumGoldPerLevel = 300;
        public const int BattlePassPremiumPriceGold = 2000;

        public const int DailyHintsAmount = 3;
        public const int DailyUndoAmount = 1;
        public const int DailyExtraMoveAmount = 1;
        public const int HintsPerRewardedAd = 1;
        public const int UndoPerRewardedAd = 1;
        public const int ExtraMovePerRewardedAd = 1;
        public const string DefaultSkinId = "default";
        public const string DefaultBackgroundId = "bg_default";
        public const int GoldPerWin = 100;
        public const int GoldPerRewardedAd = 300;

        /// <summary>
        /// Десктоп-sticky Яндекса ≈ 160×600. Резерв справа = эта ширина, потолок 15% экрана.
        /// Не доля «на всякий»: 14% на 1920 даёт дыру между игрой и баннером (п. 1.6.2.1).
        /// Телефон: доля экрана, пиксели — нижняя граница (devicePixelRatio).
        /// </summary>
        public const float MobileBannerHeight = 100f;
        public const float DesktopBannerWidth = 160f;
        public const float MobileBannerHeightFraction = 0.09f;
        /// <summary>Потолок по документации адаптивного sticky — 15% экрана.</summary>
        public const float BannerReserveMaxFraction = 0.15f;
        public const int DesktopFallbackMinWidth = 900;
        /// <summary>Ниже этого отношения ширины к высоте полоса баннера уходит вниз, а не вправо.</summary>
        public const float DesktopMinAspect = 1.2f;
        /// <summary>Разрешение, под которое нарисована вёрстка. Совпадает с CanvasScaler.</summary>
        public const float UiReferenceWidth = 1080f;
        public const float UiReferenceHeight = 1920f;
        /// <summary>Авторская пропорция вёрстки. Уже этого рамку не сужаем.</summary>
        public const float UiFrameAspect = UiReferenceWidth / UiReferenceHeight;
        /// <summary>
        /// Потолок рамки для меню, магазина и попапов. Игровой экран на широком ПК
        /// его не использует — там п. 1.6.2.1 / 1.6.2.2 и <see cref="UiPlayFieldMaxAspect"/>.
        /// </summary>
        public const float UiFrameMaxLogicalWidth = 1350f;
        /// <summary>
        /// П. 1.6.2.2: длинная сторона активного поля (доска + HUD) не больше короткой
        /// более чем в столько раз. На 16:9 / 16:10 после вычета sticky запас есть.
        /// </summary>
        public const float UiPlayFieldMaxAspect = 2f;
        /// <summary>
        /// Порог «телефон лёг на бок». Планшет в ландшафте это 4:3 (1.33) и высоты ему хватает,
        /// телефон — 16:9 и выше (1.78+), и там вёрстка 1080×1920 ужимается вдвое.
        /// 1.6 разделяет эти случаи, поэтому планшеты оверлеем поворота не накрываем.
        /// </summary>
        public const float MobileLandscapeAspect = 1.6f;
        public const string UiFontResourcePath = "Fonts/Roboto-Regular";
        /// <summary>Интервал fullscreen по п. 4.4: не чаще чем раз в полторы минуты.</summary>
        public const float InterstitialIntervalSeconds = 90f;
        /// <summary>П. 4.4: максимум 0,33 с от клика до вызова SDK, без своей заставки.</summary>
        public const float InterstitialClickDelayMax = 0.33f;
        public const int InterstitialSkipPlayThroughLevel = 2;

        /// <summary>Школьный idle: рука после паузы без хода. Не кампания.</summary>
        public const float SchoolIdleHintSeconds = 30f;

        public const float CellSize = 1f;
        public const float PathWidthInCells = 0.42f;
        public const float PathWidthMinInCells = 0.24f;
        public const float PathWidthMaxInCells = 0.52f;

        /// <summary>Наконечник заметно шире линии — иначе стрелка читается как труба.</summary>
        public const float ArrowHeadWidthInCells = 0.92f;
        public const float ArrowHeadLengthInCells = 0.72f;

        /// <summary>
        /// П. 1.8: на телефоне в портрете тело и наконечник шире, чтобы палец попадал.
        /// Сетка, cellSize и камера те же — иначе сожмутся книги, указка и портфель.
        /// Длина наконечника не растёт: встречные стрелки иначе слипаются.
        /// </summary>
        public const float MobileArrowVisualScale = 1.48f;

        /// <summary>Тёмный navy / deep purple фон поля #1A1433.</summary>
        public static readonly Color BoardBackground = new Color(0.10f, 0.08f, 0.20f, 1f);

        /// <summary>Тёмно-синий текст #1B2430.</summary>
        public static readonly Color NavyText = new Color(27f / 255f, 36f / 255f, 48f / 255f, 1f);

        /// <summary>Кнопка «Играть» #2F6BFF.</summary>
        public static readonly Color PlayBlue = new Color(47f / 255f, 107f / 255f, 255f / 255f, 1f);

        public static readonly Color HudDark = new Color(0.22f, 0.25f, 0.29f, 1f);
        public static readonly Color HudButton = new Color(1f, 1f, 1f, 1f);
        public static readonly Color HudButtonAlt = new Color(0.93f, 0.94f, 0.96f, 1f);

        /// <summary>Зелёная заливка слайдера кампании #38CC5C.</summary>
        public static readonly Color ProgressGreen = new Color(56f / 255f, 204f / 255f, 92f / 255f, 1f);
        public static readonly Color ProgressGreenPulse = new Color(0.55f, 0.95f, 0.62f, 1f);
        public static readonly Color ProgressTrack = new Color(0.10f, 0.14f, 0.12f, 0.94f);

        /// <summary>
        /// Ключи PlayerPrefs версии до облачных сохранений. Читаются ровно один раз —
        /// в GameSaves при импорте прогресса старого игрока. Ничего нового сюда писать нельзя.
        /// </summary>
        public const string PlayModePrefsKey = "Unpuzzle.PlayMode";
        public const string SchoolLevelPrefsKey = "Unpuzzle.SchoolLevel";
        public const string SchoolUnlockedPrefsKey = "Unpuzzle.SchoolUnlocked";
        public const string SchoolTutorialSeenPrefsKey = "Unpuzzle.SchoolTutorialSeen";
        public const string BattlePassCompletedSchoolPrefsKey = "Unpuzzle.BattlePass.CompletedSchool";
        public const string BattlePassPremiumPrefsKey = "Unpuzzle.BattlePass.Premium";
        public const string BattlePassClaimedFreePrefsKey = "Unpuzzle.BattlePass.ClaimedFree";
        public const string BattlePassClaimedPremiumPrefsKey = "Unpuzzle.BattlePass.ClaimedPremium";
        public const string BattlePassSchoolSyncedPrefsKey = "Unpuzzle.BattlePass.SchoolSynced";
        public const string SoundPrefsKey = "Unpuzzle.SoundOn";
        public const string MusicPrefsKey = "Unpuzzle.MusicOn";
        public const string LegacySoundPrefsKey = "Unpuzzle.Sound";
        public const string GoldPrefsKey = "Unpuzzle.Gold";
        public const string HintsCountPrefsKey = "Unpuzzle.HintsCount";
        public const string UndoCountPrefsKey = "Unpuzzle.UndoCount";
        public const string ExtraMoveCountPrefsKey = "Unpuzzle.ExtraMoveCount";
        public const string HintsLastClaimDatePrefsKey = "Unpuzzle.HintsLastClaimDate";
        public const string UnlockedSkinsPrefsKey = "Unpuzzle.UnlockedSkins";
        public const string SelectedSkinPrefsKey = "Unpuzzle.SelectedSkin";
        public const string UnlockedBackgroundsPrefsKey = "Unpuzzle.UnlockedBackgrounds";
        public const string SelectedBackgroundPrefsKey = "Unpuzzle.SelectedBackground";
        public const string HasPlayedPrefsKey = "Unpuzzle.HasPlayed";

        public static void SetLayerSafe(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        public static int LayerMaskByName(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
