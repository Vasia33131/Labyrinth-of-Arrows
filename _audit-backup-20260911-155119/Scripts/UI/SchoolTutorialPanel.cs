using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Модалка школьного обучения: страницы из массива, заглушки вместо арта.
    /// SceneCanvas не трогает. Пока открыта — тапы по полю не принимаются.
    /// </summary>
    public class SchoolTutorialPanel : MonoBehaviour
    {
        public const string OverlayName = "SchoolTutorialOverlay";
        public const string SchemeGoal = "Scheme_Goal";
        public const string SchemeKeyBook = "Scheme_KeyBook";
        public const string SchemeBigBook = "Scheme_BigBook";
        public const string SchemePointer = "Scheme_Pointer";
        public const string SchemeBriefcase = "Scheme_Briefcase";

        [Header("Оверлей")]
        public Button dimmerButton;
        public GameObject card;

        [Header("Страница")]
        public Text titleText;
        public Text bodyText;
        public Text pageIndexText;
        public Button nextButton;
        public Text nextButtonLabel;
        public RectTransform schemeRoot;

        [Header("Арт (null — квадрат-заглушка)")]
        public Sprite keyBookSprite;
        public Sprite bigBookSprite;
        public Sprite pointerSprite;
        public Sprite briefcaseSprite;

        public static bool IsOpen { get; private set; }

        private SchoolTutorialPage[] pages;
        private int pageIndex;
        private bool markSeenOnFinish;
        private Action onFinished;
        private bool built;

        private void Awake()
        {
            BindExisting();
            WireButtons();
        }

        private void OnEnable()
        {
            IsOpen = true;
            GameTexts.OnLanguageChanged += ApplyTexts;
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (IsOpen) IsOpen = false;
        }

        /// <summary>Страницы кэшируются, поэтому при смене языка собираем их заново.</summary>
        private void ApplyTexts()
        {
            pages = SchoolTutorialPages.CreateDefault();
            ShowCurrentPage();
        }

        /// <summary>Первый вход в школьный уровень: после «Понятно» пишет Seen и стартует уровень.</summary>
        public void OpenFirstRun(Action onFinished)
        {
            OpenInternal(true, onFinished);
        }

        /// <summary>«?» на экране школы: тот же набор страниц, Seen не трогаем.</summary>
        public void OpenReplay(Action onFinished = null)
        {
            OpenInternal(false, onFinished);
        }

        public void Close()
        {
            onFinished = null;
            gameObject.SetActive(false);
        }

        /// <summary>Показать панель в редакторе, не трогая якоря и размер карточки.</summary>
        public void ShowForEditor(int page)
        {
            BindExisting();
            WireButtons();
            if (pages == null || pages.Length == 0)
                pages = SchoolTutorialPages.CreateDefault();
            pageIndex = Mathf.Clamp(page, 0, pages.Length - 1);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowCurrentPage();
            ApplyAuthoredSprites();
        }

        public void AuthorForEditor()
        {
            EnsureHierarchy();
            BindExisting();
            WireButtons();

            if (card != null)
            {
                var cardRt = card.GetComponent<RectTransform>();
                if (cardRt != null) cardRt.sizeDelta = SettingsPanel.OverlayCardSize;
                var cardImage = card.GetComponent<Image>();
                Sprite cardSprite = ResolveCard();
                if (cardImage != null && cardSprite != null)
                {
                    cardImage.sprite = cardSprite;
                    cardImage.color = Color.white;
                    cardImage.preserveAspect = true;
                }
            }

            if (nextButton != null)
            {
                var face = nextButton.targetGraphic as Image ?? nextButton.GetComponent<Image>();
                Sprite btn = ResolveBtn();
                if (face != null && btn != null)
                {
                    face.sprite = btn;
                    face.color = Color.white;
                    face.preserveAspect = false;
                }
            }

            if (pages == null || pages.Length == 0)
                pages = SchoolTutorialPages.CreateDefault();
            pageIndex = 0;
            ShowCurrentPage();
            ApplyAuthoredSprites();
        }

        public static SchoolTutorialPanel EnsureOnCanvas(Transform canvas)
        {
            if (canvas == null) return null;

            SchoolTutorialPanel existing = FindOn(canvas);
            if (existing != null)
            {
                existing.BindExisting();
                existing.gameObject.SetActive(false);
                return existing;
            }

            if (canvas.gameObject.scene.name == GameConstants.MainMenuSceneName)
                return null;

            if (!AuthoredUi.CanBuild)
            {
                AuthoredUi.Missing(OverlayName);
                return null;
            }

            return CreateOverlay(canvas);
        }

        public static SchoolTutorialPanel FindOn(Transform canvas)
        {
            if (canvas == null) return null;

            Transform root = BannerSafeArea.ResolveContentRoot(canvas);
            Transform named = root != null ? root.Find(OverlayName) : null;
            if (named == null) named = canvas.Find(OverlayName);
            if (named == null) named = AuthoredUi.FindDeep(canvas, OverlayName);
            if (named != null)
                return AuthoredUi.ExistingComponent<SchoolTutorialPanel>(named.gameObject, OverlayName);

            return canvas.GetComponentInChildren<SchoolTutorialPanel>(true);
        }

        private static SchoolTutorialPanel CreateOverlay(Transform canvas)
        {
            var overlay = new GameObject(OverlayName, typeof(RectTransform));
            overlay.transform.SetParent(BannerSafeArea.ResolveContentRoot(canvas), false);
            Stretch(overlay.GetComponent<RectTransform>());

            var panel = overlay.AddComponent<SchoolTutorialPanel>();
            panel.EnsureHierarchy();
            panel.WireButtons();
            overlay.SetActive(false);
            return panel;
        }

        private void OpenInternal(bool markSeen, Action finished)
        {
            BindExisting();
            WireButtons();
            if (pages == null || pages.Length == 0)
                pages = SchoolTutorialPages.CreateDefault();

            markSeenOnFinish = markSeen;
            onFinished = finished;
            pageIndex = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowCurrentPage();
        }

        private void EnsureHierarchy()
        {
            if (!AuthoredUi.CanBuild)
            {
                BindExisting();
                return;
            }

            if (built && transform.Find("Card") != null)
            {
                BakeSchemeGroups();
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
                dimmerImage.color = dimmerSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.62f);
                dimmerImage.type = Image.Type.Simple;
                dimmerImage.raycastTarget = true;
                var dimmerBtn = dimmerGo.AddComponent<Button>();
                dimmerBtn.targetGraphic = dimmerImage;
                dimmerBtn.transition = Selectable.Transition.None;
                dimmerBtn.interactable = false;
                dimmerButton = dimmerBtn;
            }
            else if (dimmerButton == null)
            {
                Transform dimmer = transform.Find("Dimmer");
                dimmerButton = dimmer != null ? dimmer.GetComponent<Button>() : null;
            }

            Transform cardTf = transform.Find("Card");
            if (cardTf == null)
            {
                var cardGo = new GameObject("Card", typeof(RectTransform));
                cardGo.transform.SetParent(transform, false);
                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta = new Vector2(900f, 1320f);
                var cardImage = cardGo.AddComponent<Image>();
                Sprite cardSprite = ResolveCard();
                if (cardSprite != null) cardImage.sprite = cardSprite;
                cardImage.color = Color.white;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
                cardImage.raycastTarget = true;
                cardTf = cardGo.transform;
            }

            card = cardTf.gameObject;

            if (titleText == null)
            {
                Transform existing = cardTf.Find("Title");
                titleText = existing != null
                    ? existing.GetComponent<Text>()
                    : CreateLabel(cardTf, "Title", string.Empty,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(780f, 70f),
                        TextAnchor.MiddleCenter, 42, GameConstants.NavyText, FontStyle.Bold, false);
            }

            if (schemeRoot == null)
            {
                Transform existing = cardTf.Find("Scheme");
                if (existing != null)
                    schemeRoot = existing.GetComponent<RectTransform>();
                else
                {
                    var schemeGo = new GameObject("Scheme", typeof(RectTransform));
                    schemeGo.transform.SetParent(cardTf, false);
                    schemeRoot = schemeGo.GetComponent<RectTransform>();
                    schemeRoot.anchorMin = new Vector2(0.5f, 1f);
                    schemeRoot.anchorMax = new Vector2(0.5f, 1f);
                    schemeRoot.pivot = new Vector2(0.5f, 1f);
                    schemeRoot.anchoredPosition = new Vector2(0f, -130f);
                    schemeRoot.sizeDelta = new Vector2(720f, 180f);
                }
            }

            if (bodyText == null)
            {
                Transform existing = cardTf.Find("Body");
                bodyText = existing != null
                    ? existing.GetComponent<Text>()
                    : CreateLabel(cardTf, "Body", string.Empty,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(780f, 680f),
                        TextAnchor.UpperCenter, 28, GameConstants.NavyText, FontStyle.Normal, true);
            }

            if (pageIndexText == null)
            {
                Transform existing = cardTf.Find("PageIndex");
                pageIndexText = existing != null
                    ? existing.GetComponent<Text>()
                    : CreateLabel(cardTf, "PageIndex", "1 / 5",
                        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(220f, 40f),
                        TextAnchor.MiddleCenter, 24, GameConstants.NavyText, FontStyle.Bold, false);
            }

            if (nextButton == null)
            {
                Transform existing = cardTf.Find("NextButton");
                nextButton = existing != null ? existing.GetComponent<Button>() : null;
            }

            if (nextButton == null)
            {
                nextButton = CreateFaceButton(cardTf, "NextButton",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 40f), new Vector2(440f, 120f), ResolveBtn(), GameTexts.Next, 36);
            }

            if (nextButtonLabel == null && nextButton != null)
            {
                Transform label = nextButton.transform.Find("Label");
                nextButtonLabel = label != null ? label.GetComponent<Text>() : null;
            }

            built = true;
            FillFromLibrary();
            BakeSchemeGroups();
            PaintSchemeImages();
        }

        private void BindExisting()
        {
            Transform dimmer = transform.Find("Dimmer");
            if (dimmerButton == null && dimmer != null)
                dimmerButton = dimmer.GetComponent<Button>();

            Transform cardTf = transform.Find("Card");
            if (cardTf != null) card = cardTf.gameObject;

            if (titleText == null && cardTf != null)
            {
                Transform existing = cardTf.Find("Title");
                titleText = existing != null ? existing.GetComponent<Text>() : null;
            }

            if (schemeRoot == null && cardTf != null)
            {
                Transform existing = cardTf.Find("Scheme");
                if (existing != null) schemeRoot = existing.GetComponent<RectTransform>();
            }

            if (bodyText == null && cardTf != null)
            {
                Transform existing = cardTf.Find("Body");
                bodyText = existing != null ? existing.GetComponent<Text>() : null;
            }

            if (pageIndexText == null && cardTf != null)
            {
                Transform existing = cardTf.Find("PageIndex");
                pageIndexText = existing != null ? existing.GetComponent<Text>() : null;
            }

            if (nextButton == null && cardTf != null)
            {
                Transform existing = cardTf.Find("NextButton");
                nextButton = existing != null ? existing.GetComponent<Button>() : null;
            }

            if (nextButtonLabel == null && nextButton != null)
            {
                Transform label = nextButton.transform.Find("Label");
                nextButtonLabel = label != null ? label.GetComponent<Text>() : null;
            }

            built = cardTf != null;
            HideLockStubs(schemeRoot);
            ApplyAuthoredSprites();
            if (!Application.isPlaying) return;
            if (dimmer == null) AuthoredUi.Missing("SchoolTutorialOverlay/Dimmer");
            if (cardTf == null) AuthoredUi.Missing("SchoolTutorialOverlay/Card");
            if (schemeRoot == null) AuthoredUi.Missing("Scheme");
        }

        private void WireButtons()
        {
            if (nextButton == null) return;
            nextButton.onClick.RemoveListener(OnNextClicked);
            nextButton.onClick.AddListener(OnNextClicked);
        }

        private void OnNextClicked()
        {
            if (pages == null || pages.Length == 0)
            {
                Finish();
                return;
            }

            if (pageIndex >= pages.Length - 1)
            {
                Finish();
                return;
            }

            pageIndex++;
            GameAudio.Play(GameAudio.Sfx.SchoolTutorialPage);
            ShowCurrentPage();
        }

        private void Finish()
        {
            if (markSeenOnFinish)
                PlayProgress.MarkSchoolTutorialSeen();

            Action done = onFinished;
            onFinished = null;
            gameObject.SetActive(false);
            done?.Invoke();
        }

        private void ShowCurrentPage()
        {
            if (pages == null || pages.Length == 0) return;
            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
            SchoolTutorialPage page = pages[pageIndex];

            if (titleText != null)
                titleText.text = page != null && !string.IsNullOrEmpty(page.title)
                    ? page.title
                    : GameTexts.SchoolTitle;

            if (bodyText != null)
                bodyText.text = JoinLines(page);

            if (pageIndexText != null)
                pageIndexText.text = GameTexts.PageIndex(pageIndex + 1, pages.Length);

            bool last = pageIndex >= pages.Length - 1;
            if (nextButtonLabel != null)
                nextButtonLabel.text = last ? GameTexts.GotIt : GameTexts.Next;

            RebuildScheme(page);
        }

        private static string JoinLines(SchoolTutorialPage page)
        {
            if (page == null || page.lines == null || page.lines.Length == 0)
                return string.Empty;

            return string.Join("\n\n", page.lines);
        }

        private void RebuildScheme(SchoolTutorialPage page)
        {
            if (schemeRoot == null) return;

            string active = SchemeName(page != null ? page.scheme : SchoolTutorialScheme.Goal);
            bool found = false;
            for (int i = 0; i < schemeRoot.childCount; i++)
            {
                Transform child = schemeRoot.GetChild(i);
                bool on = child.name == active;
                if (on) found = true;
                child.gameObject.SetActive(on);
            }

            HideLockStubs(schemeRoot);

            if (!found)
            {
                if (AuthoredUi.CanBuild) BakeSchemeGroups();
                else AuthoredUi.Missing(active);
            }
        }

        private static string SchemeName(SchoolTutorialScheme scheme)
        {
            switch (scheme)
            {
                case SchoolTutorialScheme.KeyBook: return SchemeKeyBook;
                case SchoolTutorialScheme.BigBook: return SchemeBigBook;
                case SchoolTutorialScheme.Pointer: return SchemePointer;
                case SchoolTutorialScheme.Briefcase: return SchemeBriefcase;
                default: return SchemeGoal;
            }
        }

        /// <summary>Вешает школьный арт на поля и уже стоящие Image схем. Иерархию не пересобирает.</summary>
        public void ApplyAuthoredSprites()
        {
            if (schemeRoot == null)
            {
                Transform cardTf = transform.Find("Card");
                Transform scheme = cardTf != null ? cardTf.Find("Scheme") : null;
                if (scheme != null) schemeRoot = scheme.GetComponent<RectTransform>();
            }

            FillFromLibrary();
            PaintSchemeImages();
        }

        private void FillFromLibrary()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art == null) return;
            if (art.schoolBookIcon != null) keyBookSprite = art.schoolBookIcon;
            if (art.schoolBook != null) bigBookSprite = art.schoolBook;
            if (art.schoolPointerBody != null) pointerSprite = art.schoolPointerBody;
            if (art.schoolBackpack != null) briefcaseSprite = art.schoolBackpack;
        }

        private Sprite PointerPivotSprite
        {
            get
            {
                ArtLibrary art = ArtLibrary.Current;
                return art != null ? art.schoolPointerPivot : null;
            }
        }

        private void PaintSchemeImages()
        {
            if (schemeRoot == null) return;
            ArtLibrary art = ArtLibrary.Current;
            Sprite blueArrow = art != null ? art.ArrowForColor(ArrowColor.Blue) : null;
            Sprite yellowArrow = art != null ? art.ArrowForColor(ArrowColor.Yellow) : null;

            PaintNamed("KeyArrow", blueArrow, Color.white);
            PaintNamed("OtherArrow", yellowArrow, Color.white);
            PaintNamed("PathArrow", blueArrow, Color.white);
            PaintNamed("KeyBook", keyBookSprite, SchoolTutorialPages.KeyBook);
            PaintNamed("BigBook", bigBookSprite, SchoolTutorialPages.BigBook);
            PaintNamed("KeyMark", keyBookSprite, SchoolTutorialPages.KeyBook);
            PaintNamed("Pointer", pointerSprite, SchoolTutorialPages.Pointer);
            PaintNamed("Pivot", PointerPivotSprite, new Color(0.72f, 0.52f, 0.12f, 1f));
            PaintNamed("GoalKey", keyBookSprite, SchoolTutorialPages.KeyBook);
            PaintNamed("GoalBook", bigBookSprite, SchoolTutorialPages.BigBook);
            PaintNamed("GoalPointer", pointerSprite, SchoolTutorialPages.Pointer);
            PaintNamed("GoalCase", briefcaseSprite, SchoolTutorialPages.Briefcase);
            PaintBriefcasePair();
            HideRemainingSquares(schemeRoot);
        }

        private void PaintNamed(string name, Sprite authored, Color stubColor)
        {
            Transform tf = AuthoredUi.FindDeep(schemeRoot, name);
            if (tf == null) return;
            var image = tf.GetComponent<Image>();
            if (image == null) return;
            ApplyStubImage(image, authored, stubColor);
        }

        private void PaintBriefcasePair()
        {
            Transform caseA = AuthoredUi.FindDeep(schemeRoot, "CaseA");
            Transform caseB = AuthoredUi.FindDeep(schemeRoot, "CaseB");
            if (caseA == null) return;

            if (briefcaseSprite == null)
            {
                PaintNamed("CaseA", null, SchoolTutorialPages.Briefcase);
                PaintNamed("CaseB", null, SchoolTutorialPages.Briefcase);
                if (caseB != null) caseB.gameObject.SetActive(true);
                return;
            }

            var rtA = caseA as RectTransform;
            var rtB = caseB as RectTransform;
            if (rtA != null && rtB != null && caseB.gameObject.activeSelf)
            {
                Vector2 min = rtA.anchoredPosition - rtA.sizeDelta * 0.5f;
                Vector2 max = rtA.anchoredPosition + rtA.sizeDelta * 0.5f;
                Vector2 bMin = rtB.anchoredPosition - rtB.sizeDelta * 0.5f;
                Vector2 bMax = rtB.anchoredPosition + rtB.sizeDelta * 0.5f;
                min = Vector2.Min(min, bMin);
                max = Vector2.Max(max, bMax);
                rtA.anchoredPosition = (min + max) * 0.5f;
                rtA.sizeDelta = max - min;
            }

            ApplyStubImage(caseA.GetComponent<Image>(), briefcaseSprite, SchoolTutorialPages.Briefcase);
            if (caseB != null) caseB.gameObject.SetActive(false);
        }

        private static void ApplyStubImage(Image image, Sprite authored, Color stubColor)
        {
            if (image == null) return;
            if (authored == null)
            {
                image.enabled = false;
                return;
            }

            image.enabled = true;
            image.sprite = authored;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void HideRemainingSquares(Transform root)
        {
            if (root == null) return;
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || !IsRuntimeSquare(image.sprite)) continue;
                image.sprite = null;
                image.enabled = false;
                image.gameObject.SetActive(false);
            }
        }

        private static bool IsRuntimeSquare(Sprite sprite)
        {
            return sprite != null && sprite.name == "RuntimeSquare";
        }

        private void BakeSchemeGroups()
        {
            if (schemeRoot == null || !AuthoredUi.CanBuild) return;

            BakeGroup(SchemeGoal, BuildGoalScheme);
            BakeGroup(SchemeKeyBook, BuildKeyBookScheme);
            BakeGroup(SchemeBigBook, BuildBigBookScheme);
            BakeGroup(SchemePointer, BuildPointerScheme);
            BakeGroup(SchemeBriefcase, BuildBriefcaseScheme);
            HideLockStubs(schemeRoot);

            for (int i = 0; i < schemeRoot.childCount; i++)
                schemeRoot.GetChild(i).gameObject.SetActive(false);
        }

        private static void HideLockStubs(Transform root)
        {
            if (root == null) return;
            var doomed = new System.Collections.Generic.List<GameObject>();
            CollectLockStubs(root, doomed);
            for (int i = 0; i < doomed.Count; i++)
            {
                GameObject go = doomed[i];
                if (go == null) continue;
                go.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void CollectLockStubs(Transform root, System.Collections.Generic.List<GameObject> dst)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == "LockedArrow" || child.name == "LockMark")
                    dst.Add(child.gameObject);
                else
                    CollectLockStubs(child, dst);
            }
        }

        private void BakeGroup(string name, Action<Transform> build)
        {
            Transform group = schemeRoot.Find(name);
            if (group != null) return;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(schemeRoot, false);
            Stretch(go.GetComponent<RectTransform>());
            build(go.transform);
        }

        private void BuildKeyBookScheme(Transform parent)
        {
            AddStub(parent, "KeyArrow", new Vector2(-120f, 0f), new Vector2(140f, 140f), Color.white, BlueArrowSprite());
            AddStub(parent, "KeyBook", new Vector2(-70f, -36f), new Vector2(64f, 64f), SchoolTutorialPages.KeyBook, keyBookSprite);
            AddStub(parent, "OtherArrow", new Vector2(120f, 0f), new Vector2(140f, 140f), Color.white, YellowArrowSprite());
        }

        private void BuildBigBookScheme(Transform parent)
        {
            AddStub(parent, "PathArrow", new Vector2(-180f, 0f), new Vector2(120f, 120f), Color.white, BlueArrowSprite());
            AddStub(parent, "BigBook", new Vector2(40f, 0f), new Vector2(170f, 170f), SchoolTutorialPages.BigBook, bigBookSprite);
            AddStub(parent, "KeyMark", new Vector2(-180f, -70f), new Vector2(48f, 48f), SchoolTutorialPages.KeyBook, keyBookSprite);
        }

        private void BuildPointerScheme(Transform parent)
        {
            AddStub(parent, "Pointer", new Vector2(20f, 0f), new Vector2(420f, 64f), SchoolTutorialPages.Pointer, pointerSprite);
            AddStub(parent, "Pivot", new Vector2(-190f, 0f), new Vector2(72f, 72f), new Color(0.72f, 0.52f, 0.12f, 1f), PointerPivotSprite);
        }

        private void BuildBriefcaseScheme(Transform parent)
        {
            if (briefcaseSprite != null)
            {
                AddStub(parent, "CaseA", Vector2.zero, new Vector2(260f, 120f), SchoolTutorialPages.Briefcase, briefcaseSprite);
                AddStub(parent, "CaseB", new Vector2(70f, 0f), new Vector2(120f, 120f), SchoolTutorialPages.Briefcase, null);
                Transform caseB = parent.Find("CaseB");
                if (caseB != null) caseB.gameObject.SetActive(false);
            }
            else
            {
                AddStub(parent, "CaseA", new Vector2(-70f, 0f), new Vector2(120f, 120f), SchoolTutorialPages.Briefcase, null);
                AddStub(parent, "CaseB", new Vector2(70f, 0f), new Vector2(120f, 120f), SchoolTutorialPages.Briefcase, null);
            }
        }

        private void BuildGoalScheme(Transform parent)
        {
            AddStub(parent, "GoalKey", new Vector2(-240f, 0f), new Vector2(88f, 88f), SchoolTutorialPages.KeyBook, keyBookSprite);
            AddStub(parent, "GoalBook", new Vector2(-80f, 0f), new Vector2(110f, 110f), SchoolTutorialPages.BigBook, bigBookSprite);
            AddStub(parent, "GoalPointer", new Vector2(80f, 0f), new Vector2(140f, 48f), SchoolTutorialPages.Pointer, pointerSprite);
            AddStub(parent, "GoalCase", new Vector2(240f, 0f), new Vector2(88f, 88f), SchoolTutorialPages.Briefcase, briefcaseSprite);
        }

        private void AddStub(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite authored)
        {
            if (parent == null || !AuthoredUi.CanBuild) return;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var image = go.AddComponent<Image>();
            ApplyStubImage(image, authored, color);
        }

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

            CreateLabel(go.transform, "Label", label,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter, fontSize, GameConstants.NavyText, FontStyle.Bold, false);

            return button;
        }

        private static Text CreateLabel(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            TextAnchor alignment, int fontSize, Color color, FontStyle style, bool wrap)
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
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = wrap ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.lineSpacing = wrap ? 1.22f : 1f;
            text.resizeTextForBestFit = !wrap;
            if (!wrap)
            {
                text.resizeTextMinSize = Mathf.Max(14, fontSize - 16);
                text.resizeTextMaxSize = fontSize;
            }
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

        private static Sprite BlueArrowSprite()
        {
            ArtLibrary art = ArtLibrary.Current;
            return art != null ? art.ArrowForColor(ArrowColor.Blue) : null;
        }

        private static Sprite YellowArrowSprite()
        {
            ArtLibrary art = ArtLibrary.Current;
            return art != null ? art.ArrowForColor(ArrowColor.Yellow) : null;
        }

        private static Sprite ResolveBtn()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.btn != null) return art.btn;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.btn : null;
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
            if (art != null && art.uiCard != null) return art.uiCard;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.uiCard : null;
        }
    }
}
