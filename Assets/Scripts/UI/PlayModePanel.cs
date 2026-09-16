using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Оверлей выбора режима поверх MainMenu: затемнение + карточка.
    /// Страница режимов и сетка школы 1..20. SceneCanvas не трогает.
    /// </summary>
    public class PlayModePanel : MonoBehaviour
    {
        public const string OverlayName = "PlayModeOverlay";

        [Header("Оверлей")]
        public Button dimmerButton;
        public Button closeXButton;

        [Header("Режим")]
        public GameObject modePage;
        public Button normalButton;
        public Button schoolButton;

        [Header("Школа")]
        public GameObject schoolPage;
        public Button backButton;
        public Button helpButton;
        public Transform levelGrid;
        public Sprite lockSprite;

        public static bool IsOpen { get; private set; }

        private static readonly Vector2 SchoolHelpSize = OverlayCardFit.CloseSize;
        private static readonly Vector2 SchoolHelpOffset = new Vector2(-OverlayCardFit.CloseOffset.x, OverlayCardFit.CloseOffset.y);

        private readonly Button[] schoolSlots = new Button[SchoolLevelCatalog.TargetCount];
        private readonly GameObject[] schoolLocks = new GameObject[SchoolLevelCatalog.TargetCount];
        private readonly GameObject[] schoolChecks = new GameObject[SchoolLevelCatalog.TargetCount];
        private bool slotsBuilt;
        private int lastSchoolHelpWidth;
        private int lastSchoolHelpHeight;
        private float lastSchoolHelpFrameWidth;

        private void Awake()
        {
            BindExisting();
            if (!SceneCanvas.LayoutLocked) ApplyPlayVisuals();
            else
            {
                PaintFaces();
                EnsureSchoolDecorations();
            }
            WireButtons();
            BindOverlayCard();
        }

        private void OnEnable()
        {
            IsOpen = true;
            GameTexts.OnLanguageChanged += ApplyTexts;
            ApplyTexts();
            BindOverlayCard();
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (IsOpen) IsOpen = false;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (schoolPage == null || !schoolPage.activeInHierarchy) return;
            RepairSchoolHelpButton(false);
        }

        /// <summary>Заголовки страниц и подписи кнопок собраны в сцене — переписываем на языке YG2.</summary>
        private void ApplyTexts()
        {
            if (modePage != null)
                UiLabel.SetNamed(modePage.transform, "Title", GameTexts.ModeTitle);
            if (schoolPage != null)
                UiLabel.SetNamed(schoolPage.transform, "Title", GameTexts.SchoolTitle);

            UiLabel.Set(normalButton, GameTexts.NormalGame);
            UiLabel.Set(schoolButton, GameTexts.SchoolTitle);
        }

        public void Open()
        {
            BindExisting();
            if (!SceneCanvas.LayoutLocked) ApplyPlayVisuals();
            else
            {
                PaintFaces();
                EnsureSchoolDecorations();
            }
            WireButtons();
            ApplyTexts();
            ShowModePage();
            GameAudio.BindUiClicks(transform);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindOverlayCard();
        }

        public void OpenSchool()
        {
            BindExisting();
            if (!SceneCanvas.LayoutLocked) ApplyPlayVisuals();
            else
            {
                PaintFaces();
                EnsureSchoolDecorations();
            }
            WireButtons();
            ApplyTexts();
            ShowSchoolPage();
            GameAudio.BindUiClicks(transform);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BindOverlayCard();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void BindOverlayCard()
        {
            OverlayCardFitHost.Ensure(this, closeXButton);
            HideSchoolBackButton();
            RepairSchoolHelpButton(true);
        }

        public void AuthorForEditor()
        {
            BindExisting();
            ApplyPlayVisuals();
            ShowSchoolChecksForEdit();
            if (modePage != null) modePage.SetActive(true);
            if (schoolPage != null) schoolPage.SetActive(true);
        }

        public void ShowModePage()
        {
            if (modePage != null) modePage.SetActive(true);
            if (schoolPage != null) schoolPage.SetActive(false);
            if (helpButton != null) helpButton.gameObject.SetActive(false);
        }

        public void ShowSchoolPage()
        {
            if (modePage != null) modePage.SetActive(false);
            if (schoolPage != null) schoolPage.SetActive(true);
            if (helpButton != null) helpButton.gameObject.SetActive(true);
            RefreshSchoolSlots();
            HideSchoolBackButton();
            RepairSchoolHelpButton(true);
        }

        public static PlayModePanel EnsureOnCanvas(Transform canvas)
        {
            if (canvas == null) return null;

            PlayModePanel existing = FindOn(canvas);
            if (existing != null)
            {
                if (AuthoredUi.CanBuild) existing.EnsureHierarchy();
                else existing.BindExisting();
                existing.gameObject.SetActive(false);
                return existing;
            }

            if (!AuthoredUi.CanBuild)
            {
                AuthoredUi.Missing(OverlayName);
                return null;
            }

            return CreateOverlay(canvas);
        }

        public static PlayModePanel FindOn(Transform canvas)
        {
            if (canvas == null) return null;

            Transform root = BannerSafeArea.ResolveOverlayRoot(canvas);
            Transform named = root != null ? root.Find(OverlayName) : null;
            if (named == null) named = canvas.Find(OverlayName);
            if (named == null) named = AuthoredUi.FindDeep(canvas, OverlayName);
            if (named != null)
                return AuthoredUi.ExistingComponent<PlayModePanel>(named.gameObject, OverlayName);

            return canvas.GetComponentInChildren<PlayModePanel>(true);
        }

        private static PlayModePanel CreateOverlay(Transform canvas)
        {
            var overlay = new GameObject(OverlayName, typeof(RectTransform));
            overlay.transform.SetParent(BannerSafeArea.ResolveOverlayRoot(canvas), false);
            Stretch(overlay.GetComponent<RectTransform>());

            var panel = overlay.AddComponent<PlayModePanel>();
            panel.EnsureHierarchy();
            panel.WireButtons();
            overlay.SetActive(false);
            return panel;
        }

        private void EnsureHierarchy()
        {
            if (!AuthoredUi.CanBuild)
            {
                BindExisting();
                return;
            }
            if (transform.Find("Dimmer") == null)
            {
                var dimmerGo = new GameObject("Dimmer", typeof(RectTransform));
                dimmerGo.transform.SetParent(transform, false);
                Stretch(dimmerGo.GetComponent<RectTransform>());
                var dimmerImage = dimmerGo.AddComponent<Image>();
                Sprite dimmerSprite = ResolveDimmer();
                if (dimmerSprite != null) dimmerImage.sprite = dimmerSprite;
                dimmerImage.color = dimmerSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.55f);
                dimmerImage.type = Image.Type.Simple;
                dimmerImage.raycastTarget = true;
                var dimmerBtn = dimmerGo.AddComponent<Button>();
                dimmerBtn.targetGraphic = dimmerImage;
                dimmerBtn.transition = Selectable.Transition.None;
                dimmerButton = dimmerBtn;
            }
            else if (dimmerButton == null)
            {
                Transform dimmer = transform.Find("Dimmer");
                dimmerButton = dimmer != null ? dimmer.GetComponent<Button>() : null;
            }

            Transform card = transform.Find("Card");
            if (card == null)
            {
                var cardGo = new GameObject("Card", typeof(RectTransform));
                cardGo.transform.SetParent(transform, false);
                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta = OverlayCardFit.CardSize;
                var cardImage = cardGo.AddComponent<Image>();
                Sprite cardSprite = ResolveCard();
                if (cardSprite != null) cardImage.sprite = cardSprite;
                cardImage.color = Color.white;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
                cardImage.raycastTarget = true;
                card = cardGo.transform;
            }

            if (closeXButton == null)
            {
                Transform existingX = card.Find("CloseXButton");
                closeXButton = existingX != null ? existingX.GetComponent<Button>() : null;
            }

            if (closeXButton == null)
            {
                closeXButton = CreateFaceButton(card, "CloseXButton",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    OverlayCardFit.CloseOffset, OverlayCardFit.CloseSize, ResolveClose(), string.Empty, 1);
                Transform label = closeXButton.transform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }

            if (modePage == null)
            {
                Transform existing = card.Find("ModePage");
                modePage = existing != null ? existing.gameObject : CreateModePage(card);
            }

            if (schoolPage == null)
            {
                Transform existing = card.Find("SchoolPage");
                schoolPage = existing != null ? existing.gameObject : CreateSchoolPage(card);
            }

            BindPageRefs();
            EnsureSchoolSlots();
            ApplyPlayVisuals();
            BindOverlayCard();
        }

        private void BindExisting()
        {
            Transform dimmer = transform.Find("Dimmer");
            if (dimmerButton == null && dimmer != null)
                dimmerButton = dimmer.GetComponent<Button>();

            Transform card = transform.Find("Card");
            if (closeXButton == null && card != null)
            {
                Transform existingX = card.Find("CloseXButton");
                closeXButton = existingX != null ? existingX.GetComponent<Button>() : null;
            }

            if (modePage == null && card != null)
            {
                Transform existing = card.Find("ModePage");
                if (existing != null) modePage = existing.gameObject;
            }

            if (schoolPage == null && card != null)
            {
                Transform existing = card.Find("SchoolPage");
                if (existing != null) schoolPage = existing.gameObject;
            }

            BindPageRefs();
            BindSchoolSlots();
            if (!Application.isPlaying) return;

            if (dimmer == null) AuthoredUi.Missing("PlayModeOverlay/Dimmer");
            if (card == null) AuthoredUi.Missing("PlayModeOverlay/Card");
            if (modePage == null) AuthoredUi.Missing("ModePage");
            if (schoolPage == null) AuthoredUi.Missing("SchoolPage");
            if (levelGrid == null) AuthoredUi.Missing("LevelGrid");
        }

        private GameObject CreateModePage(Transform card)
        {
            var go = new GameObject("ModePage", typeof(RectTransform));
            go.transform.SetParent(card, false);
            Stretch(go.GetComponent<RectTransform>());

            CreateLabel(go.transform, "Title", GameTexts.ModeTitle,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(560f, 72f),
                TextAnchor.MiddleCenter, 52, GameConstants.NavyText, FontStyle.Bold);

            normalButton = CreateFaceButton(go.transform, "NormalButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f), new Vector2(640f, 148f), ResolveBtn(), GameTexts.NormalGame, 40);

            schoolButton = CreateFaceButton(go.transform, "SchoolButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -110f), new Vector2(640f, 148f), ResolveBtn(), GameTexts.SchoolTitle, 40);

            return go;
        }

        private GameObject CreateSchoolPage(Transform card)
        {
            var go = new GameObject("SchoolPage", typeof(RectTransform));
            go.transform.SetParent(card, false);
            Stretch(go.GetComponent<RectTransform>());
            go.SetActive(false);

            CreateLabel(go.transform, "Title", GameTexts.SchoolTitle,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(560f, 64f),
                TextAnchor.MiddleCenter, 48, GameConstants.NavyText, FontStyle.Bold);

            helpButton = CreateFaceButton(go.transform, "HelpButton",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                SchoolHelpOffset, SchoolHelpSize, ResolveBtn(), "?", 40);

            var gridGo = new GameObject("LevelGrid", typeof(RectTransform));
            gridGo.transform.SetParent(go.transform, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.5f, 0.5f);
            gridRt.anchorMax = new Vector2(0.5f, 0.5f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = new Vector2(0f, 20f);
            gridRt.sizeDelta = new Vector2(720f, 730f);

            var layout = gridGo.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.cellSize = new Vector2(160f, 130f);
            layout.spacing = new Vector2(12f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            levelGrid = gridGo.transform;

            return go;
        }

        private void BindPageRefs()
        {
            if (modePage != null)
            {
                if (normalButton == null)
                {
                    Transform t = modePage.transform.Find("NormalButton");
                    if (t != null) normalButton = t.GetComponent<Button>();
                }

                if (schoolButton == null)
                {
                    Transform t = modePage.transform.Find("SchoolButton");
                    if (t != null) schoolButton = t.GetComponent<Button>();
                }
            }

            if (schoolPage != null)
            {
                HideSchoolBackButton();

                if (levelGrid == null)
                {
                    Transform t = schoolPage.transform.Find("LevelGrid");
                    if (t != null) levelGrid = t;
                }

                if (helpButton == null)
                {
                    Transform t = schoolPage.transform.Find("HelpButton");
                    helpButton = t != null ? t.GetComponent<Button>() : null;
                }

                if (helpButton == null && AuthoredUi.CanBuild)
                {
                    helpButton = CreateFaceButton(schoolPage.transform, "HelpButton",
                        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        SchoolHelpOffset, SchoolHelpSize, ResolveBtn(), "?", 40);
                }
            }
        }

        private void BindSchoolSlots()
        {
            if (levelGrid == null) return;

            int count = Mathf.Min(levelGrid.childCount, SchoolLevelCatalog.TargetCount);
            for (int i = 0; i < count; i++)
            {
                Transform child = levelGrid.GetChild(i);
                schoolSlots[i] = child.GetComponent<Button>();
                Transform lockTf = child.Find("Lock");
                schoolLocks[i] = lockTf != null ? lockTf.gameObject : null;
                Transform checkTf = child.Find("Check");
                schoolChecks[i] = checkTf != null ? checkTf.gameObject : null;
            }

            if (count >= SchoolLevelCatalog.TargetCount)
                slotsBuilt = true;
            else if (Application.isPlaying)
            {
                AuthoredUi.Missing("SchoolLevel_01");
                slotsBuilt = true;
            }
        }

        private void EnsureSchoolSlots()
        {
            if (levelGrid == null) return;
            BindSchoolSlots();
            if (slotsBuilt || !AuthoredUi.CanBuild) return;

            Sprite face = ResolveBtn();
            for (int i = levelGrid.childCount; i < SchoolLevelCatalog.TargetCount; i++)
            {
                int level = i + 1;
                Button button = CreateFaceButton(levelGrid, $"SchoolLevel_{level:D2}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(160f, 130f), face, level.ToString(), 36);
                schoolSlots[i] = button;
                schoolLocks[i] = CreateLockMark(button.transform);
                schoolChecks[i] = CreateCheckMark(button.transform);
            }

            slotsBuilt = true;
        }

        private GameObject CreateLockMark(Transform slot)
        {
            var go = new GameObject("Lock", typeof(RectTransform));
            go.transform.SetParent(slot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(36f, 36f);

            var image = go.AddComponent<Image>();
            Sprite sprite = lockSprite != null ? lockSprite : ArrowSpriteFactory.GetSquare();
            if (sprite != null) image.sprite = sprite;
            image.color = new Color(0.22f, 0.25f, 0.29f, 0.92f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.SetActive(false);
            return go;
        }

        private GameObject CreateCheckMark(Transform slot)
        {
            Transform existing = slot.Find("Check");
            if (existing != null) return existing.gameObject;

            var go = new GameObject("Check", typeof(RectTransform));
            go.transform.SetParent(slot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(44f, 44f);

            var image = go.AddComponent<Image>();
            Sprite sprite = ResolveCheck();
            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.SetActive(false);
            return go;
        }

        private void EnsureSchoolDecorations()
        {
            BindSchoolSlots();
            for (int i = 0; i < schoolSlots.Length; i++)
            {
                Button button = schoolSlots[i];
                if (button == null) continue;

                if (schoolLocks[i] == null)
                {
                    Transform lockTf = button.transform.Find("Lock");
                    schoolLocks[i] = lockTf != null ? lockTf.gameObject : CreateLockMark(button.transform);
                }

                if (schoolChecks[i] == null)
                    schoolChecks[i] = CreateCheckMark(button.transform);
            }
        }

        private void RefreshSchoolSlots()
        {
            EnsureSchoolSlots();
            EnsureSchoolDecorations();
            ArtLibrary art = ArtLibrary.Current;
            Sprite openFace = art != null && art.schoolLevelOpen != null ? art.schoolLevelOpen : ResolveBtn();
            Sprite lockedFace = art != null && art.schoolLevelLocked != null ? art.schoolLevelLocked : openFace;
            bool customTiles = art != null && art.schoolLevelOpen != null;
            int unlocked = PlayProgress.SchoolUnlocked;
            for (int i = 0; i < schoolSlots.Length; i++)
            {
                Button button = schoolSlots[i];
                if (button == null) continue;

                int level = i + 1;
                bool locked = level > unlocked;
                bool completed = BattlePass.IsSchoolCompleted(level) || level < unlocked;
                button.interactable = !locked;

                var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image != null)
                {
                    Sprite face = locked ? lockedFace : openFace;
                    if (face != null) image.sprite = face;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = customTiles;
                    image.color = locked && !customTiles
                        ? new Color(0.78f, 0.79f, 0.82f, 1f)
                        : Color.white;
                }

                if (schoolLocks[i] != null)
                    schoolLocks[i].SetActive(locked);
                if (schoolChecks[i] != null)
                    schoolChecks[i].SetActive(completed && !locked);
            }
        }

        private void ApplyPlayVisuals()
        {
            Transform card = transform.Find("Card");
            if (card != null)
            {
                var cardRt = card as RectTransform;
                cardRt.sizeDelta = OverlayCardFit.CardSize;
                var cardImage = card.GetComponent<Image>();
                Sprite cardSprite = ResolveCard();
                if (cardImage != null && cardSprite != null)
                {
                    cardImage.sprite = cardSprite;
                    cardImage.color = Color.white;
                    cardImage.type = Image.Type.Simple;
                    cardImage.preserveAspect = true;
                }
            }

            RepairCloseX();
            RepairModeTitle();
            RepairSchoolTitle();
            HideSchoolBackButton();
            RepairSchoolHelpButton(true);
            PaintFaces();
            EnsureSchoolDecorations();
            PaintSchoolSlotFaces();
        }

        private void PaintSchoolSlotFaces()
        {
            ArtLibrary art = ArtLibrary.Current;
            Sprite openFace = art != null && art.schoolLevelOpen != null ? art.schoolLevelOpen : ResolveBtn();
            bool customTiles = art != null && art.schoolLevelOpen != null;
            for (int i = 0; i < schoolSlots.Length; i++)
            {
                Button button = schoolSlots[i];
                if (button == null) continue;
                var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image == null) continue;
                if (openFace != null) image.sprite = openFace;
                image.type = Image.Type.Simple;
                image.preserveAspect = customTiles;
                image.color = Color.white;
            }
        }

        private void ShowSchoolChecksForEdit()
        {
            Sprite checkSprite = ResolveCheck();
            for (int i = 0; i < schoolChecks.Length; i++)
            {
                GameObject mark = schoolChecks[i];
                if (mark == null) continue;
                mark.SetActive(true);
                var image = mark.GetComponent<Image>();
                if (image == null || checkSprite == null) continue;
                image.sprite = checkSprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
        }

        private void RepairCloseX()
        {
            Transform card = transform.Find("Card");
            if (closeXButton == null && card != null)
            {
                Transform existingX = card.Find("CloseXButton");
                closeXButton = existingX != null ? existingX.GetComponent<Button>() : null;
            }

            if (closeXButton == null) return;

            OverlayCardFit.PlaceCloseX(closeXButton.transform as RectTransform, card as RectTransform);

            var face = closeXButton.targetGraphic as Image ?? closeXButton.GetComponent<Image>();
            Sprite close = ResolveClose();
            if (face != null)
            {
                if (close != null) face.sprite = close;
                face.type = Image.Type.Simple;
                face.preserveAspect = true;
                face.color = Color.white;
                face.raycastTarget = true;
            }

            Transform label = closeXButton.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            Transform extraIcon = closeXButton.transform.Find("Icon");
            if (extraIcon != null) extraIcon.gameObject.SetActive(false);
        }

        private void RepairModeTitle()
        {
            if (modePage == null) return;
            var title = modePage.transform.Find("Title") as RectTransform;
            if (title == null) return;
            title.anchorMin = new Vector2(0.5f, 1f);
            title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -300f);
            title.sizeDelta = new Vector2(560f, 72f);
        }

        private void RepairSchoolTitle()
        {
            if (schoolPage == null) return;
            var title = schoolPage.transform.Find("Title") as RectTransform;
            if (title == null) return;
            title.anchorMin = new Vector2(0.5f, 1f);
            title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -240f);
            title.sizeDelta = new Vector2(560f, 64f);
        }

        private void HideSchoolBackButton()
        {
            if (backButton != null)
                backButton.gameObject.SetActive(false);
            else if (schoolPage != null)
            {
                Transform t = schoolPage.transform.Find("BackButton");
                if (t != null) t.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// «?» слева от банта, напротив крестика: тот же размер и тот же Y.
        /// </summary>
        private void RepairSchoolHelpButton(bool force)
        {
            if (helpButton == null) return;
            bool schoolOpen = schoolPage != null && schoolPage.activeSelf;
            helpButton.gameObject.SetActive(schoolOpen);
            if (!schoolOpen && !force) return;

            var buttonRt = helpButton.transform as RectTransform;
            var cardRt = transform.Find("Card") as RectTransform;
            if (buttonRt == null || cardRt == null) return;

            float frameWidth = ResolveFrameWidth();
            if (!force
                && Screen.width == lastSchoolHelpWidth
                && Screen.height == lastSchoolHelpHeight
                && Mathf.Approximately(frameWidth, lastSchoolHelpFrameWidth))
                return;

            lastSchoolHelpWidth = Screen.width;
            lastSchoolHelpHeight = Screen.height;
            lastSchoolHelpFrameWidth = frameWidth;

            OverlayCardFit.Apply(cardRt, closeXButton != null ? closeXButton.transform as RectTransform : null);
            OverlayCardFit.PlaceSchoolHelp(buttonRt, cardRt);
            if (closeXButton != null)
                closeXButton.transform.SetAsLastSibling();
        }

        private float ResolveFrameWidth()
        {
            BannerSafeArea area = BannerSafeArea.For(transform);
            if (area != null && area.FrameRoot != null)
                return Mathf.Max(area.FrameRoot.rect.width, area.FrameRoot.sizeDelta.x);

            var overlay = transform as RectTransform;
            return overlay != null ? overlay.rect.width : 0f;
        }

        private void PaintFaces()
        {
            Sprite btn = ResolveBtn();
            PaintButtonFace(normalButton, btn, false);
            PaintButtonFace(schoolButton, btn, false);
            PaintButtonFace(backButton, btn, false);
            PaintButtonFace(helpButton, btn, true);
            PaintButtonFace(closeXButton, ResolveClose(), true);
        }

        private static void PaintButtonFace(Button button, Sprite sprite, bool preserveAspect)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null) return;
            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = true;
        }

        private void WireButtons()
        {
            if (dimmerButton != null)
            {
                dimmerButton.transition = Selectable.Transition.None;
                dimmerButton.onClick.RemoveListener(Close);
                dimmerButton.onClick.AddListener(Close);
            }

            if (closeXButton != null)
            {
                closeXButton.onClick.RemoveListener(Close);
                closeXButton.onClick.AddListener(Close);
            }

            if (normalButton != null)
            {
                normalButton.onClick.RemoveListener(OnNormalClicked);
                normalButton.onClick.AddListener(OnNormalClicked);
            }

            if (schoolButton != null)
            {
                schoolButton.onClick.RemoveListener(ShowSchoolPage);
                schoolButton.onClick.AddListener(ShowSchoolPage);
            }

            if (helpButton != null)
            {
                helpButton.onClick.RemoveListener(OnHelpClicked);
                helpButton.onClick.AddListener(OnHelpClicked);
            }

            WireSchoolSlots();
        }

        private void WireSchoolSlots()
        {
            EnsureSchoolSlots();
            for (int i = 0; i < schoolSlots.Length; i++)
            {
                Button button = schoolSlots[i];
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                int level = i + 1;
                button.onClick.AddListener(() => OnSchoolLevelClicked(level));
            }
        }

        private void OnNormalClicked()
        {
            PlayProgress.SetNormal();
            LoadGame();
        }

        private void OnSchoolLevelClicked(int level)
        {
            if (level < 1 || level > SchoolLevelCatalog.TargetCount) return;
            if (level > PlayProgress.SchoolUnlocked) return;

            PlayProgress.SetSchool(level);
            LoadGame();
        }

        private void OnHelpClicked()
        {
            SchoolTutorialPanel tutorial = ResolveTutorial();
            if (tutorial != null)
            {
                tutorial.OpenReplay();
                return;
            }

            // Туториал лежит только в GameScene — «?» из меню ведёт туда.
            PlayProgress.RequestSchoolTutorialReplay();
            PlayProgress.SetSchool(PlayProgress.SchoolLevel);
            LoadGame();
        }

        private SchoolTutorialPanel ResolveTutorial()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform host = canvas != null ? canvas.transform : transform.parent;
            return SchoolTutorialPanel.FindOn(host);
        }

        private static void LoadGame()
        {
            InterstitialAds.TryShowThen(InterstitialGate.Play, LoadGameScene);
        }

        private static void LoadGameScene() => SceneManager.LoadScene(GameConstants.GameSceneName);

        private static Button CreateFaceButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
            Sprite face, string label, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            PlaceRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, pos, size);

            var image = go.AddComponent<Image>();
            if (face != null) image.sprite = face;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            if (!string.IsNullOrEmpty(label))
            {
                CreateLabel(go.transform, "Label", label,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    TextAnchor.MiddleCenter, fontSize, GameConstants.NavyText, FontStyle.Bold);
            }

            return button;
        }

        private static Text CreateLabel(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            TextAnchor alignment, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (Mathf.Approximately(anchorMin.x, 0.5f) && Mathf.Approximately(anchorMax.x, 0.5f)
                && Mathf.Approximately(anchorMin.y, 1f))
                rt.pivot = new Vector2(0.5f, 1f);
            if (Mathf.Approximately(anchorMin.x, 0f) && Mathf.Approximately(anchorMax.x, 1f)
                && Mathf.Approximately(anchorMin.y, 0f) && Mathf.Approximately(anchorMax.y, 1f))
            {
                rt.offsetMin = new Vector2(8f, 6f);
                rt.offsetMax = new Vector2(-8f, -6f);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = GoldCounterView.ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, fontSize - 16);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void PlaceRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Sprite ResolveBtn()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.btn != null) return art.btn;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.btn : null;
        }

        private static Sprite ResolveClose()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.iconClose != null) return art.iconClose;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.iconClose : null;
        }

        private static Sprite ResolveDimmer()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.bgDimmer != null) return art.bgDimmer;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.bgDimmer : null;
        }

        private static Sprite ResolveCard()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.playModeCard != null) return art.playModeCard;
            if (art != null && art.uiCard != null) return art.uiCard;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.uiCard : null;
        }

        private static Sprite ResolveCheck()
        {
            ArtLibrary art = ArtLibrary.Current;
            return art != null ? art.iconCheck : null;
        }
    }
}
