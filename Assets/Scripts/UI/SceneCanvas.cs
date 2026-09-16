using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Хаб авторской сцены на Canvas. Позиции кнопок и спрайты берутся из инспектора,
    /// рантайм их не перезаписывает.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class SceneCanvas : MonoBehaviour
    {
        [Header("Авторский режим")]
        [Tooltip("Не двигать RectTransform с кнопок и зон — правь якоря в сцене.")]
        public bool lockAuthoredLayout = true;
        [Tooltip("Не подменять спрайты из ArtLibrary в рантайме. Меняй PNG на Image в иерархии; слоты ниже — для Context Menu Apply Sprites.")]
        public bool lockAuthoredArt = true;

        [Header("Разметка (применяется только по контекст-меню)")]
        public ScreenLayoutBuilder layoutBuilder;
        public ScreenLayoutConfig config = new ScreenLayoutConfig();

        [Header("Фоны")]
        public Image background;
        public Image winPanelImage;
        public Image losePanelImage;
        public Image settingsDimmer;
        public Image settingsCard;
        public Image levelBadge;
        public Sprite bgMenu;
        public Sprite bgBoard;
        public Sprite bgWin;
        public Sprite bgLose;
        public Sprite bgDimmer;
        public Sprite uiCard;
        public Sprite uiCapsule;

        [Header("Кнопки HUD")]
        public Button menuButton;
        public Button settingsButton;
        public Button hintButton;
        public Button undoButton;
        public Button extraMoveButton;
        public Button restartButton;
        public Button playButton;
        public Button nextLevelButton;
        public Button winRestartButton;
        public Button loseRestartButton;
        public Button loseExtraMoveButton;
        public Button settingsCloseButton;
        public Button settingsCloseXButton;

        [Header("Спрайты кнопок")]
        public Sprite btn;
        public Sprite btnHint;
        public Sprite btnUndo;
        public Sprite btnRestart;
        public Sprite iconBack;
        public Sprite iconGear;
        public Sprite iconHint;
        public Sprite iconUndo;
        public Sprite iconRestart;
        public Sprite iconClose;
        public Sprite iconExtraMove;
        public Sprite badgeCount;

        [Header("Персонаж")]
        public CharacterView character;
        public Image characterImage;
        public Sprite characterIdle;
        public Sprite characterHappy;
        public Sprite characterSad;

        [Header("Миска")]
        public CandyBoxView candyBox;
        public Image bowlImage;
        public Sprite bowlEmpty;
        public Sprite bowl25;
        public Sprite bowl50;
        public Sprite bowl75;
        public Sprite bowlFull;
        public Sprite candyIcon;

        [Header("Настройки")]
        public SettingsPanel settings;
        public Sprite iconSound;
        public Sprite iconSoundOff;
        public Sprite iconMusic;
        public Sprite iconMusicOff;

        [Header("Магазин")]
        public ShopPanel shop;
        public Button shopButton;
        public Button shopCloseXButton;
        public Image shopDimmer;
        public Image shopCard;

        public static SceneCanvas InScene => FindObjectOfType<SceneCanvas>();

        public static bool LayoutLocked
        {
            get
            {
                SceneCanvas canvas = InScene;
                return canvas == null || canvas.lockAuthoredLayout;
            }
        }

        public static bool ArtLocked
        {
            get
            {
                SceneCanvas canvas = InScene;
                return canvas == null || canvas.lockAuthoredArt;
            }
        }

        private void Awake()
        {
            // Спрайты и якоря уже стоят в сцене — Play их не пересобирает.
            SyncLayoutLock();
            // Полосу под sticky-баннер резервируем и в рантайме: иначе баннер ляжет на HUD.
            BannerSafeArea.Ensure(GetComponent<Canvas>());
            // Прозрачные поля капсулы не должны перехватывать тапы по соседней кнопке.
            UiTapGuard.Apply(this);
            OrientationGate.Ensure(GetComponent<Canvas>());
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Backgrounds.OnChanged += ApplySelectedBackground;
            ApplySelectedBackground();
        }

        private void OnDisable()
        {
            Backgrounds.OnChanged -= ApplySelectedBackground;
        }

        private void OnValidate()
        {
            SyncLayoutLock();
        }

        public void ApplySelectedBackground()
        {
            if (!Application.isPlaying) return;
            BackgroundVisual.Apply(this);
        }

        public void SyncLayoutLock()
        {
            if (layoutBuilder == null) layoutBuilder = GetComponentInChildren<ScreenLayoutBuilder>(true);
            if (layoutBuilder == null) return;

            layoutBuilder.applyRuntimeLayout = !Application.isPlaying && !lockAuthoredLayout;
            if (config != null) layoutBuilder.config = config;
        }

        [ContextMenu("Apply Sprites To Canvas")]
        public void ApplySprites()
        {
            if (Application.isPlaying)
            {
                if (character != null) character.ApplySelectedSkin();
                return;
            }
            SetSpriteIfEmpty(background, MenuOrBoardBackground(), false, false);
            SetSpriteIfEmpty(settingsDimmer, bgDimmer, false);
            SetSpriteIfEmpty(settingsCard, uiCard, true);
            SetSpriteIfEmpty(shopDimmer, bgDimmer, false);
            SetSpriteIfEmpty(shopCard, uiCard, true);

            if (character != null)
            {
                if (characterImage == null) characterImage = character.display;
                if (Application.isPlaying)
                {
                    character.ApplySelectedSkin();
                }
                else
                {
                    if (characterIdle != null) character.idleSprite = characterIdle;
                    if (characterHappy != null) character.happySprite = characterHappy;
                    if (characterSad != null) character.sadSprite = characterSad;
                    SetSpriteIfEmpty(characterImage, characterIdle, false);
                }
            }

            if (candyBox != null)
            {
                if (bowlEmpty != null) candyBox.bowlEmpty = bowlEmpty;
                if (bowl25 != null) candyBox.bowl25 = bowl25;
                if (bowl50 != null) candyBox.bowl50 = bowl50;
                if (bowl75 != null) candyBox.bowl75 = bowl75;
                if (bowlFull != null) candyBox.bowlFull = bowlFull;
                if (candyIcon != null) candyBox.candySprite = candyIcon;
                if (bowlImage == null) bowlImage = candyBox.bowlImage;
            }

            SetSpriteIfEmpty(bowlImage, bowlEmpty, false);

            if (settings != null)
            {
                if (iconSound != null) settings.soundOnSprite = iconSound;
                if (iconSoundOff != null) settings.soundOffSprite = iconSoundOff;
                if (iconMusic != null) settings.musicOnSprite = iconMusic;
                if (iconMusicOff != null) settings.musicOffSprite = iconMusicOff;
            }
        }

        [ContextMenu("Apply Layout Once")]
        public void ApplyLayoutOnce()
        {
            if (Application.isPlaying) return;
            SyncLayoutLock();
            if (layoutBuilder == null) return;

            bool keepLock = lockAuthoredLayout;
            lockAuthoredLayout = false;
            layoutBuilder.applyRuntimeLayout = true;
            layoutBuilder.Apply();
            lockAuthoredLayout = keepLock;
            layoutBuilder.applyRuntimeLayout = !keepLock;
        }

        private Sprite MenuOrBoardBackground()
        {
            if (playButton != null && bgMenu != null) return bgMenu;
            return bgBoard != null ? bgBoard : bgMenu;
        }

        private static void SetSpriteIfEmpty(Image image, Sprite sprite, bool sliced, bool preserveAspect = true)
        {
            if (image == null || sprite == null || image.sprite != null) return;
            image.sprite = sprite;
            image.color = Color.white;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = sliced ? false : preserveAspect;
        }
    }
}
