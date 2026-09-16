using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Оверлей настроек: звук / музыка в облачных сохранениях.
    /// Жёлтая кнопка с «Вкл / Выкл» и иконками on/off.
    /// Объект на сцене (MainMenu и GameScene), не префаб.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Оверлей")]
        public Button dimmerButton;
        public Button closeButton;
        public Button closeXButton;

        [Header("Тогглы")]
        public Toggle soundToggle;
        public Toggle musicToggle;
        public Text soundStateLabel;
        public Text musicStateLabel;

        [Header("Иконки")]
        public Image soundIcon;
        public Image musicIcon;
        public Sprite soundOnSprite;
        public Sprite soundOffSprite;
        public Sprite musicOnSprite;
        public Sprite musicOffSprite;

        [Header("Аудио")]
        public AudioSource musicSource;

        private bool suppressToggleEvents;

        public static readonly Vector2 OverlayCardSize = OverlayCardFit.CardSize;

        public static bool IsOpen { get; private set; }

        public static bool SoundOn => GameSaves.Data.soundOn;
        public static bool MusicOn => GameSaves.Data.musicOn;

        private void Awake()
        {
            if (!Application.isPlaying && !SceneCanvas.ArtLocked) BindArt();
            if (!SceneCanvas.LayoutLocked)
            {
                ApplyCardSize();
                EnsureCloseX();
                RepairExistingLayout();
            }
            BindToggleIcons();

            if (dimmerButton != null)
            {
                dimmerButton.transition = Selectable.Transition.None;
                dimmerButton.onClick.AddListener(Close);
            }

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (soundToggle != null) soundToggle.onValueChanged.AddListener(OnSoundChanged);
            if (musicToggle != null) musicToggle.onValueChanged.AddListener(OnMusicChanged);

            RefreshTogglesFromPrefs();
            ApplyAudio();
            ApplyTexts();
            BindOverlayCard();
        }

        private void Start()
        {
            RefreshTogglesFromPrefs();
            BindOverlayCard();
        }

        private void OnEnable()
        {
            IsOpen = true;
            GameTexts.OnLanguageChanged += ApplyTexts;
            RefreshTogglesFromPrefs();
            ApplyTexts();
            ApplyAudio();
            BindOverlayCard();
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (IsOpen) IsOpen = false;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (!SceneCanvas.LayoutLocked)
            {
                ApplyCardSize();
                EnsureCloseX();
                RepairExistingLayout();
            }
            RefreshTogglesFromPrefs();
            ApplyTexts();
            transform.SetAsLastSibling();
            BindOverlayCard();
        }

        /// <summary>
        /// Заголовок, подписи строк «Звук» / «Музыка» и «Закрыть» стоят в сцене,
        /// поэтому переписываются здесь. «Вкл / Выкл» идёт через RefreshToggleVisual.
        /// </summary>
        private void ApplyTexts()
        {
            Transform card = FindCard();
            if (card != null) UiLabel.SetNamed(card, "Title", GameTexts.Settings);

            if (soundToggle != null)
                UiLabel.SetNamed(soundToggle.transform, "Label", GameTexts.Sound);
            if (musicToggle != null)
                UiLabel.SetNamed(musicToggle.transform, "Label", GameTexts.Music);

            UiLabel.Set(closeButton, GameTexts.Close);
        }

        private void BindOverlayCard()
        {
            OverlayCardFitHost.Ensure(this, closeXButton);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void AuthorForEditor()
        {
            BindArt();
            EnsureAuthoredToggleIcons();
            RefreshTogglesFromPrefs();
        }

        public static void SetSoundOn(bool on)
        {
            if (GameSaves.Data.soundOn == on) return;

            GameSaves.Data.soundOn = on;
            GameSaves.SaveNow();
        }

        public static void ApplyAudio(AudioSource musicSource = null)
        {
            AudioListener.volume = SoundOn ? 1f : 0f;
            if (musicSource != null)
            {
                musicSource.mute = true;
                musicSource.volume = 0f;
            }

            GameAudio.ApplySettings();
        }

        public void ApplyAudio()
        {
            ApplyAudio(musicSource);
        }

        private void OnSoundChanged(bool on)
        {
            if (suppressToggleEvents) return;
            SetSoundOn(on);
            RefreshToggleVisual(soundToggle, soundStateLabel, soundIcon, soundOnSprite, soundOffSprite, on);
            ApplyAudio();
        }

        private void OnMusicChanged(bool on)
        {
            if (suppressToggleEvents) return;
            if (GameSaves.Data.musicOn != on)
            {
                GameSaves.Data.musicOn = on;
                GameSaves.SaveNow();
            }
            RefreshToggleVisual(musicToggle, musicStateLabel, musicIcon, musicOnSprite, musicOffSprite, on);
            ApplyAudio();
        }

        private void RefreshTogglesFromPrefs()
        {
            suppressToggleEvents = true;
            bool sound = SoundOn;
            bool music = MusicOn;
            if (soundToggle != null) soundToggle.SetIsOnWithoutNotify(sound);
            if (musicToggle != null) musicToggle.SetIsOnWithoutNotify(music);
            RefreshToggleVisual(soundToggle, soundStateLabel, soundIcon, soundOnSprite, soundOffSprite, sound);
            RefreshToggleVisual(musicToggle, musicStateLabel, musicIcon, musicOnSprite, musicOffSprite, music);
            suppressToggleEvents = false;
        }

        private static void RefreshToggleVisual(Toggle toggle, Text stateLabel, Image icon, Sprite onSprite, Sprite offSprite, bool on)
        {
            if (stateLabel != null)
            {
                stateLabel.text = GameTexts.OnOff(on);
                stateLabel.color = GameConstants.NavyText;
                stateLabel.raycastTarget = false;
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                Sprite sprite = on ? onSprite : offSprite;
                if (sprite != null) icon.sprite = sprite;
                icon.color = Color.white;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            if (toggle == null) return;

            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;

            var colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            toggle.colors = colors;

            var rowFace = toggle.GetComponent<Image>();
            if (rowFace != null)
            {
                rowFace.color = Color.white;
                rowFace.raycastTarget = true;
                rowFace.preserveAspect = false;
                toggle.targetGraphic = rowFace;
            }

            Image trackImage = FindTrackImage(toggle);
            if (trackImage != null)
            {
                trackImage.color = new Color(1f, 1f, 1f, 0f);
                trackImage.raycastTarget = false;
            }

            HideToggleCheckmark(toggle);
            HideOffLabel(toggle);
        }

        private static Image FindTrackImage(Toggle toggle)
        {
            if (toggle == null) return null;
            Transform track = toggle.transform.Find("Track");
            return track != null ? track.GetComponent<Image>() : null;
        }

        private static void HideToggleCheckmark(Toggle toggle)
        {
            Transform track = toggle != null ? toggle.transform.Find("Track") : null;
            Transform check = track != null ? track.Find("Checkmark") : null;
            var image = check != null ? check.GetComponent<Image>() : null;
            if (image == null) return;

            image.enabled = false;
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.canvasRenderer.SetAlpha(0f);
            image.CrossFadeAlpha(0f, 0f, true);
        }

        private static void HideOffLabel(Toggle toggle)
        {
            Transform track = toggle != null ? toggle.transform.Find("Track") : null;
            Transform off = track != null ? track.Find("OffLabel") : null;
            if (off != null) off.gameObject.SetActive(false);
        }

        private void BindArt()
        {
            ArtLibrary art = ArtLibrary.Current;
            SceneCanvas canvas = SceneCanvas.InScene;
            if (soundOnSprite == null)
                soundOnSprite = art != null ? art.iconSound : null;
            if (soundOnSprite == null && canvas != null) soundOnSprite = canvas.iconSound;
            if (soundOffSprite == null)
                soundOffSprite = art != null ? art.iconSoundOff : null;
            if (soundOffSprite == null && canvas != null) soundOffSprite = canvas.iconSoundOff;
            if (musicOnSprite == null)
                musicOnSprite = art != null ? art.iconMusic : null;
            if (musicOnSprite == null && canvas != null) musicOnSprite = canvas.iconMusic;
            if (musicOffSprite == null)
                musicOffSprite = art != null ? art.iconMusicOff : null;
            if (musicOffSprite == null && canvas != null) musicOffSprite = canvas.iconMusicOff;
        }

        public void EnsureAuthoredToggleIcons()
        {
            BindArt();
            if (soundIcon == null) soundIcon = EnsureToggleIcon(soundToggle);
            if (musicIcon == null) musicIcon = EnsureToggleIcon(musicToggle);
            ShowToggleIcon(soundIcon, soundOnSprite);
            ShowToggleIcon(musicIcon, musicOnSprite);
        }

        private void BindToggleIcons()
        {
            if (soundIcon == null)
                soundIcon = FindToggleIcon(soundToggle);
            if (musicIcon == null)
                musicIcon = FindToggleIcon(musicToggle);
            if (soundIcon != null) soundIcon.gameObject.SetActive(true);
            if (musicIcon != null) musicIcon.gameObject.SetActive(true);

            HideCloseXLabel();
            if (!Application.isPlaying && !SceneCanvas.LayoutLocked) RepairCloseX();
        }

        private static void ShowToggleIcon(Image icon, Sprite sprite)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(true);
            if (sprite != null && icon.sprite == null) icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;
        }

        private static Image FindToggleIcon(Toggle toggle)
        {
            if (toggle == null) return null;
            Transform existing = toggle.transform.Find("Icon");
            return existing != null ? existing.GetComponent<Image>() : null;
        }

        public static Image EnsureToggleIcon(Toggle toggle)
        {
            if (toggle == null) return null;

            Transform existing = toggle.transform.Find("Icon");
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                if (!AuthoredUi.CanBuild) return null;
                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(toggle.transform, false);
                go.transform.SetAsFirstSibling();
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(64f, 0f);
                rt.sizeDelta = new Vector2(100f, 100f);
                image = go.AddComponent<Image>();
            }

            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.gameObject.SetActive(true);

            return image;
        }

        private void RepairExistingLayout()
        {
            RepairTitle();
            RepairCloseX();
            RepairCloseButton();
            RepairToggleRow(soundToggle, soundStateLabel, 70f);
            RepairToggleRow(musicToggle, musicStateLabel, -80f);
        }

        private void RepairTitle()
        {
            Transform card = FindCard();
            if (card == null) return;
            var title = card.Find("Title") as RectTransform;
            if (title == null) return;
            title.anchorMin = new Vector2(0.5f, 1f);
            title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -150f);
            title.sizeDelta = new Vector2(560f, 72f);
        }

        private Transform FindCard()
        {
            Transform card = transform.Find("Card");
            if (card != null) return card;
            if (closeButton != null) return closeButton.transform.parent;
            if (soundToggle != null) return soundToggle.transform.parent;
            return null;
        }

        private void ApplyCardSize()
        {
            var card = FindCard() as RectTransform;
            if (card == null) return;
            card.sizeDelta = OverlayCardSize;
        }

        private void EnsureCloseX()
        {
            Transform card = FindCard();
            if (card == null) return;

            if (closeXButton == null)
            {
                Transform existing = card.Find("CloseXButton");
                closeXButton = existing != null ? existing.GetComponent<Button>() : null;
            }

            if (closeXButton == null)
            {
                var go = new GameObject("CloseXButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(card, false);
                var image = go.GetComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = true;
                closeXButton = go.GetComponent<Button>();
                closeXButton.targetGraphic = image;
            }

            closeXButton.transform.SetAsLastSibling();
            RepairCloseX();
            closeXButton.onClick.RemoveListener(Close);
            closeXButton.onClick.AddListener(Close);
        }

        private void RepairCloseX()
        {
            if (closeXButton == null) return;

            OverlayCardFit.PlaceCloseX(closeXButton.transform as RectTransform, FindCard() as RectTransform);

            ArtLibrary art = ArtLibrary.Current;
            Sprite close = art != null ? art.iconClose : null;
            var face = closeXButton.targetGraphic as Image ?? closeXButton.GetComponent<Image>();
            if (face != null)
            {
                if (close != null) face.sprite = close;
                face.type = Image.Type.Simple;
                face.preserveAspect = true;
                face.color = Color.white;
                face.raycastTarget = true;
            }

            HideCloseXLabel();
            Transform extraIcon = closeXButton.transform.Find("Icon");
            if (extraIcon != null) extraIcon.gameObject.SetActive(false);
        }

        private void RepairCloseButton()
        {
            if (closeButton == null) return;

            var rt = closeButton.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -250f);
            rt.sizeDelta = new Vector2(400f, 152f);
            rt.localScale = Vector3.one;

            ArtLibrary art = ArtLibrary.Current;
            var face = closeButton.targetGraphic as Image ?? closeButton.GetComponent<Image>();
            if (face != null)
            {
                if (art != null && art.btn != null) face.sprite = art.btn;
                face.type = Image.Type.Simple;
                face.preserveAspect = true;
                face.color = Color.white;
            }

            var label = closeButton.transform.Find("Label") as RectTransform;
            if (label == null) return;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(24f, 18f);
            label.offsetMax = new Vector2(-24f, -18f);
            label.anchoredPosition = Vector2.zero;
            var text = label.GetComponent<Text>();
            if (text != null)
            {
                text.text = GameTexts.Close;
                text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 28;
                text.resizeTextMaxSize = 44;
            }
        }

        private void RepairToggleRow(Toggle toggle, Text stateLabel, float y)
        {
            if (toggle == null) return;

            var row = toggle.transform as RectTransform;
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(680f, 148f);

            var rowImage = toggle.GetComponent<Image>();
            if (rowImage != null)
            {
                ArtLibrary art = ArtLibrary.Current;
                if (art != null && art.btn != null) rowImage.sprite = art.btn;
                rowImage.type = Image.Type.Simple;
                rowImage.preserveAspect = false;
                rowImage.color = Color.white;
                rowImage.raycastTarget = true;
            }

            var icon = row.Find("Icon") as RectTransform;
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                var iconImage = icon.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;
                    iconImage.color = Color.white;
                }
            }

            var label = row.Find("Label") as RectTransform;
            if (label != null)
            {
                label.anchorMin = new Vector2(0f, 0.5f);
                label.anchorMax = new Vector2(0f, 0.5f);
                label.pivot = new Vector2(0f, 0.5f);
                label.anchoredPosition = new Vector2(36f, 0f);
                label.sizeDelta = new Vector2(230f, 140f);
                var labelText = label.GetComponent<Text>();
                if (labelText != null)
                {
                    labelText.alignment = TextAnchor.MiddleLeft;
                    labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    labelText.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            var track = row.Find("Track") as RectTransform;
            if (track == null) return;
            track.anchorMin = new Vector2(1f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(1f, 0.5f);
            track.anchoredPosition = new Vector2(-8f, 0f);
            track.sizeDelta = new Vector2(360f, 138f);

            var trackImage = track.GetComponent<Image>();
            if (trackImage != null)
            {
                trackImage.color = new Color(1f, 1f, 1f, 0f);
                trackImage.raycastTarget = false;
            }

            if (rowImage != null) toggle.targetGraphic = rowImage;
            toggle.graphic = null;
            HideToggleCheckmark(toggle);

            var check = track.Find("Checkmark") as RectTransform;
            if (check != null)
            {
                check.anchorMin = Vector2.zero;
                check.anchorMax = Vector2.one;
                check.offsetMin = Vector2.zero;
                check.offsetMax = Vector2.zero;
            }

            HideOffLabel(toggle);

            if (stateLabel != null)
            {
                var stateRt = stateLabel.rectTransform;
                stateRt.anchorMin = Vector2.zero;
                stateRt.anchorMax = Vector2.one;
                stateRt.offsetMin = new Vector2(20f, 16f);
                stateRt.offsetMax = new Vector2(-20f, -16f);
                stateLabel.alignment = TextAnchor.MiddleCenter;
                stateLabel.color = GameConstants.NavyText;
                stateLabel.resizeTextForBestFit = true;
                stateLabel.resizeTextMinSize = 28;
                stateLabel.resizeTextMaxSize = 40;
            }
        }

        private void HideCloseXLabel()
        {
            if (closeXButton == null) return;
            Transform label = closeXButton.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
        }

    }
}
