using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Ссылки на арт из Assets/Art/Sprites. Ресурс: Resources/ArtLibrary.
    /// </summary>
    [CreateAssetMenu(fileName = "ArtLibrary", menuName = "Unpuzzle/Art Library")]
    public class ArtLibrary : ScriptableObject
    {
        public const string ResourceName = "ArtLibrary";

        private static ArtLibrary cached;

        public static ArtLibrary Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<ArtLibrary>(ResourceName);
                return cached;
            }
        }

        [Header("Фоны")]
        public Sprite bgMenu;
        public Sprite bgBoard;
        public Sprite bgDimmer;
        public Sprite bgWin;
        public Sprite bgLose;

        [Header("Кнопки")]
        public Sprite btn;
        public Sprite btnHint;
        public Sprite btnUndo;
        public Sprite btnRestart;

        [Header("Иконки")]
        public Sprite iconBack;
        public Sprite iconGear;
        public Sprite iconHint;
        public Sprite iconUndo;
        public Sprite iconRestart;
        public Sprite iconClose;
        public Sprite iconSound;
        public Sprite iconSoundOff;
        public Sprite iconMusic;
        public Sprite iconMusicOff;
        public Sprite iconExtraMove;
        public Sprite badgeCount;

        [Header("UI")]
        public Sprite uiRounded;
        public Sprite uiCapsule;
        public Sprite uiCard;
        public Sprite sliderTrack;
        public Sprite sliderFill;
        public Sprite sliderKnob;
        public Sprite frameCharacter;

        [Header("Миска / конфеты")]
        public Sprite bowl0;
        public Sprite bowl25;
        public Sprite bowl50;
        public Sprite bowl75;
        public Sprite bowl100;
        public Sprite candyIcon;

        [Header("Персонаж")]
        public Sprite characterIdle;
        public Sprite characterHappy;
        public Sprite characterSad;
        public Sprite characterFace;

        [Header("Стрелки")]
        public Sprite arrowHead;
        public Sprite arrowRed;
        public Sprite arrowBlue;
        public Sprite arrowNavy;
        public Sprite arrowGreen;
        public Sprite arrowYellow;
        public Sprite arrowOrange;
        public Sprite arrowGrey;

        [Header("Боевой пропуск")]
        public Sprite bpStripBg;
        public Sprite bpSlotFree;
        public Sprite bpSlotPremium;
        public Sprite bpSlotLocked;
        public Sprite iconCheck;
        public Sprite iconCoin;
        public Sprite iconCoinsStack;

        [Header("Магазин")]
        public Sprite slotShopSelected;

        [Header("Школа")]
        public Sprite schoolBook;
        public Sprite schoolBookIcon;
        public Sprite schoolPointerPivot;
        public Sprite schoolPointerBody;
        public Sprite schoolBackpack;
        public Sprite schoolLevelOpen;
        public Sprite schoolLevelLocked;
        [Tooltip("Необязательно. Пусто — простой UI-палец школьной idle-подсказки.")]
        public Sprite schoolIdleHand;

        [Header("Панель режима")]
        public Sprite playModeCard;

        public Sprite BowlForFill(float percent01)
        {
            float p = Mathf.Clamp01(percent01);
            if (p <= 0f) return bowl0;
            if (p < 0.25f) return bowl25;
            if (p < 0.50f) return bowl50;
            if (p < 0.75f) return bowl75;
            return bowl100;
        }

        public Sprite ArrowForColor(ArrowColor color)
        {
            Sprite sprite;
            switch (color)
            {
                case ArrowColor.Red: sprite = arrowRed; break;
                case ArrowColor.Blue: sprite = arrowBlue; break;
                case ArrowColor.DarkBlue: sprite = arrowNavy; break;
                case ArrowColor.Green: sprite = arrowGreen; break;
                case ArrowColor.Yellow: sprite = arrowYellow; break;
                case ArrowColor.Orange: sprite = arrowOrange; break;
                default: sprite = arrowGrey; break;
            }

            return sprite != null ? sprite : arrowHead;
        }
    }
}
