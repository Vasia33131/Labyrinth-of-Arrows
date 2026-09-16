using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Показывает школьный туториал из сцены. Вёрстку, спрайты и иерархию не трогает —
    /// только листает уже стоящие страницы и схемы.
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

        [Header("Арт (только ссылки, скрипт их не ставит)")]
        public Sprite keyBookSprite;
        public Sprite bigBookSprite;
        public Sprite pointerSprite;
        public Sprite briefcaseSprite;

        public static bool IsOpen { get; private set; }

        private SchoolTutorialPage[] pages;
        private int pageIndex;
        private bool markSeenOnFinish;
        private Action onFinished;

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

        /// <summary>Показать панель в редакторе, не меняя якоря, спрайты и размер карточки.</summary>
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
        }

        public static SchoolTutorialPanel EnsureOnCanvas(Transform canvas)
        {
            SchoolTutorialPanel existing = FindOn(canvas);
            if (existing != null)
            {
                existing.BindExisting();
                existing.gameObject.SetActive(false);
                return existing;
            }

            AuthoredUi.Missing(OverlayName);
            return null;
        }

        public static SchoolTutorialPanel FindOn(Transform canvas)
        {
            if (canvas == null) return null;

            Transform root = BannerSafeArea.ResolveOverlayRoot(canvas);
            Transform named = root != null ? root.Find(OverlayName) : null;
            if (named == null) named = canvas.Find(OverlayName);
            if (named == null) named = AuthoredUi.FindDeep(canvas, OverlayName);
            if (named != null)
                return AuthoredUi.ExistingComponent<SchoolTutorialPanel>(named.gameObject, OverlayName);

            return canvas.GetComponentInChildren<SchoolTutorialPanel>(true);
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

        private void BindExisting()
        {
            Transform dimmer = transform.Find("Dimmer");
            if (dimmerButton == null && dimmer != null)
                dimmerButton = dimmer.GetComponent<Button>();

            Transform cardTf = transform.Find("Card");
            if (cardTf != null) card = cardTf.gameObject;
            if (Application.isPlaying)
                OverlayCardFitHost.Ensure(this);

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

            ShowScheme(page);
        }

        private static string JoinLines(SchoolTutorialPage page)
        {
            if (page == null || page.lines == null || page.lines.Length == 0)
                return string.Empty;

            return string.Join("\n\n", page.lines);
        }

        private void ShowScheme(SchoolTutorialPage page)
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

            if (!found)
                AuthoredUi.Missing(active);
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
    }
}
