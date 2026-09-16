using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Оверлей ежедневных подсказок поверх MainMenu.
    /// PlayModePanel — выбор режима, не перезаход в меню: панель не открывается при его Close.
    /// Возврат с уровня грузит MainMenu заново — поэтому один раз за сессию (статический флаг).
    /// Паузу звука/игры при рекламе делает YG2, здесь не дублируем.
    /// </summary>
    public class DailyHintsPanel : MonoBehaviour
    {
        public const string OverlayName = "DailyHintsOverlay";

        [Header("Оверлей")]
        public Button dimmerButton;
        public Button closeButton;
        public Button closeXButton;

        [Header("Текст")]
        public Text titleText;
        public Text messageText;
        public Text balanceText;

        [Header("Реклама")]
        public Button adButton;
        public Text adButtonLabel;

        public static bool IsOpen { get; private set; }
        public static bool WasShownThisSession { get; private set; }

        private bool adsEventsBound;
        private bool offeringAd;
        private BoosterKind offeringKind = BoosterKind.Hint;
        private bool pendingShowAfterAds;
        private bool pendingMenuShow;
        private bool hiddenForAds;
        private bool claimedNowForShow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            IsOpen = false;
            WasShownThisSession = false;
        }

        private void Awake()
        {
            BindExisting();
            WireButtons();
        }

        private void OnEnable()
        {
            IsOpen = true;
            GameTexts.OnLanguageChanged += ApplyTexts;
            BindAdsEvents();
            ApplyTexts();
            RefreshAdsUi();
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (IsOpen) IsOpen = false;
        }

        /// <summary>
        /// Перерисовать карточку на текущем языке YG2: «ОК» стоит в сцене, остальное
        /// собирается тем же состоянием, что и при открытии, — оффер или ежедневный бонус.
        /// </summary>
        private void ApplyTexts()
        {
            UiLabel.Set(closeButton, GameTexts.Ok);
            ApplyAdButtonText();

            if (offeringAd) ApplyOfferState();
            else ApplyState(claimedNowForShow);
        }

        private void OnDestroy()
        {
            UnbindAdsEvents();
        }

        /// <summary>Вход в главное меню: claim, затем A / B / C. Повторно за сессию не открывается.</summary>
        public void ShowOnMenuEnter()
        {
            if (WasShownThisSession) return;

            BindExisting();
            BindAdsEvents();
            claimedNowForShow = Hints.TryClaimDailyHints() || Hints.ClaimedDailyThisSession;
            ApplyState(claimedNowForShow);

            if (AdsShowing())
            {
                pendingShowAfterAds = true;
                pendingMenuShow = true;
                return;
            }

            WasShownThisSession = true;
            Open();
        }

        public void Open()
        {
            BindExisting();
            WireButtons();
            PaintFaces();
            BindAdsEvents();
            if (AdsShowing())
            {
                pendingShowAfterAds = true;
                return;
            }

            pendingShowAfterAds = false;
            hiddenForAds = false;
            GameAudio.BindUiClicks(transform);
            UiLabel.Set(closeButton, GameTexts.Ok);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// С уровня при нуле бустера: та же панель и оффер «реклама за +N».
        /// Не трогает флаг сессии меню и не пересобирает рекламу.
        /// </summary>
        public void ShowOutOfHintsOffer() => ShowOutOfStockOffer(BoosterKind.Hint);

        public void ShowOutOfStockOffer(BoosterKind kind)
        {
            BindExisting();
            BindAdsEvents();
            offeringKind = kind;
            ApplyOfferState();
            Open();
        }

        public void Close()
        {
            pendingShowAfterAds = false;
            pendingMenuShow = false;
            hiddenForAds = false;
            gameObject.SetActive(false);
        }

        public void AuthorForEditor()
        {
            EnsureHierarchy();
            BindExisting();
            WireButtons();
            PaintFaces();
            ApplyState(true);
            if (adButton != null) adButton.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }

        public static DailyHintsPanel EnsureOnCanvas(Transform canvas)
        {
            if (canvas == null) return null;

            DailyHintsPanel existing = FindOn(canvas);
            if (existing != null)
            {
                existing.EnsureHierarchy();
                existing.gameObject.SetActive(false);
                return existing;
            }

            return CreateOverlay(canvas);
        }

        public static DailyHintsPanel FindOn(Transform canvas)
        {
            if (canvas == null) return null;

            Transform root = BannerSafeArea.ResolveContentRoot(canvas);
            Transform named = root != null ? root.Find(OverlayName) : null;
            if (named == null) named = canvas.Find(OverlayName);
            if (named == null) named = AuthoredUi.FindDeep(canvas, OverlayName);
            if (named != null)
                return AuthoredUi.ExistingComponent<DailyHintsPanel>(named.gameObject, OverlayName);

            return canvas.GetComponentInChildren<DailyHintsPanel>(true);
        }

        private static DailyHintsPanel CreateOverlay(Transform canvas)
        {
            var overlay = new GameObject(OverlayName, typeof(RectTransform));
            overlay.transform.SetParent(BannerSafeArea.ResolveContentRoot(canvas), false);
            Stretch(overlay.GetComponent<RectTransform>());

            var panel = overlay.AddComponent<DailyHintsPanel>();
            panel.EnsureHierarchy();
            panel.WireButtons();
            overlay.SetActive(false);
            return panel;
        }

        private void ApplyState(bool claimedNow)
        {
            if (titleText != null) titleText.text = GameTexts.HintsTitle;

            if (claimedNow)
            {
                if (messageText != null)
                    messageText.text = DailyPackMessage();
                SetBalances();
                offeringAd = false;
                SetAdVisible(false);
                RefreshAdInteractable();
                return;
            }

            int hints = Hints.GetHintsCount();
            if (hints <= 0)
            {
                offeringKind = BoosterKind.Hint;
                ApplyOfferState();
                return;
            }

            if (messageText != null) messageText.text = GameTexts.DailyBonusAlreadyClaimed;
            SetBalances();
            offeringAd = false;
            SetAdVisible(false);
            RefreshAdInteractable();
        }

        private void ApplyOfferState()
        {
            offeringAd = true;
            if (titleText != null) titleText.text = OfferTitle(offeringKind);
            if (messageText != null) messageText.text = OfferEmptyMessage(offeringKind);
            SetBalances();
            SetAdVisible(true);
            ApplyAdButtonText();
            RefreshAdInteractable();
        }

        /// <summary>
        /// Требование 4.5.1: до клика кнопка называет и количество, и саму награду.
        /// Подпись собирается из тех же констант, что и выдача, чтобы не разъехаться.
        /// </summary>
        private void ApplyAdButtonText()
        {
            if (adButtonLabel == null && adButton != null)
            {
                Transform label = adButton.transform.Find("Label");
                adButtonLabel = label != null ? label.GetComponent<Text>() : null;
                if (adButtonLabel == null) adButtonLabel = adButton.GetComponentInChildren<Text>(true);
            }

            if (adButtonLabel != null) adButtonLabel.text = AdButtonText(offeringKind);
        }

        private static string AdButtonText(BoosterKind kind)
        {
            return GameTexts.WatchAdForBooster(kind, RewardedAds.RewardAmount(kind));
        }

        private static string DailyPackMessage()
        {
            return GameTexts.DailyBonus(
                GameConstants.DailyHintsAmount,
                GameConstants.DailyUndoAmount,
                GameConstants.DailyExtraMoveAmount);
        }

        private static string OfferTitle(BoosterKind kind) => GameTexts.BoosterTitle(kind);

        private static string OfferEmptyMessage(BoosterKind kind) => GameTexts.BoosterEmpty(kind);

        private void SetBalances()
        {
            if (balanceText == null) return;
            balanceText.gameObject.SetActive(true);
            balanceText.horizontalOverflow = HorizontalWrapMode.Wrap;
            balanceText.verticalOverflow = VerticalWrapMode.Overflow;
            balanceText.text = GameTexts.BoosterBalance(
                Hints.GetHintsCount(), UndoCharges.GetCount(), ExtraMoves.GetCount());
        }

        private void SetAdVisible(bool visible)
        {
            if (adButton != null) adButton.gameObject.SetActive(visible);
        }

        private static bool AdsShowing()
        {
            return RewardedAds.IsBusy || InterstitialAds.IsShowing || YG2.nowAdsShow;
        }

        private void BindAdsEvents()
        {
            if (adsEventsBound) return;
            adsEventsBound = true;
            RewardedAds.OnStateChanged += HandleAdsStateChanged;
            RewardedAds.OnFailed += HandleAdsFailed;
            Hints.OnChanged += HandleStockChanged;
            UndoCharges.OnChanged += HandleStockChanged;
            ExtraMoves.OnChanged += HandleStockChanged;
            YG2.onOpenAnyAdv += HandleAnyAdOpen;
            YG2.onCloseAnyAdv += HandleAnyAdClose;
        }

        private void UnbindAdsEvents()
        {
            if (!adsEventsBound) return;
            adsEventsBound = false;
            RewardedAds.OnStateChanged -= HandleAdsStateChanged;
            RewardedAds.OnFailed -= HandleAdsFailed;
            Hints.OnChanged -= HandleStockChanged;
            UndoCharges.OnChanged -= HandleStockChanged;
            ExtraMoves.OnChanged -= HandleStockChanged;
            YG2.onOpenAnyAdv -= HandleAnyAdOpen;
            YG2.onCloseAnyAdv -= HandleAnyAdClose;
        }

        private void HandleAnyAdOpen()
        {
            if (!gameObject.activeSelf) return;
            hiddenForAds = true;
            gameObject.SetActive(false);
        }

        private void HandleAnyAdClose()
        {
            TryShowAfterAds();
        }

        private void HandleAdsStateChanged()
        {
            if (!AdsShowing()) TryShowAfterAds();
            if (isActiveAndEnabled) RefreshAdsUi();
        }

        private void HandleAdsFailed(string id)
        {
            if (!offeringAd) return;
            if (!string.IsNullOrEmpty(id) && id != RewardedAds.IdFor(offeringKind)) return;
            if (messageText != null) messageText.text = RewardedAds.FailMessage;
            if (isActiveAndEnabled) RefreshAdsUi();
        }

        private void HandleStockChanged(int _)
        {
            if (isActiveAndEnabled) RefreshAdsUi();
        }

        public void FlushPendingAfterAds() => TryShowAfterAds();

        public bool HasPendingShow => pendingShowAfterAds || pendingMenuShow || hiddenForAds;

        private void TryShowAfterAds()
        {
            if (AdsShowing()) return;
            if (!pendingShowAfterAds && !hiddenForAds) return;

            bool menuShow = pendingMenuShow;
            bool showDaily = claimedNowForShow && !offeringAd;
            pendingShowAfterAds = false;
            pendingMenuShow = false;
            hiddenForAds = false;
            if (menuShow) WasShownThisSession = true;
            if (showDaily) ApplyState(true);
            Open();
            RefreshAdsUi();
        }

        private void RefreshAdsUi()
        {
            SetBalances();

            if (offeringAd)
            {
                int count = CountOf(offeringKind);
                SetAdVisible(count <= 0);
                ApplyAdButtonText();
                if (count > 0 && messageText != null)
                    messageText.text = GameTexts.BoosterReceived(
                        offeringKind, RewardedAds.RewardAmount(offeringKind));
            }

            RefreshAdInteractable();
        }

        private static int CountOf(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return UndoCharges.GetCount();
                case BoosterKind.ExtraMove: return ExtraMoves.GetCount();
                default: return Hints.GetHintsCount();
            }
        }

        private void RefreshAdInteractable()
        {
            if (adButton == null) return;
            adButton.interactable = adButton.gameObject.activeSelf && !RewardedAds.IsBusy;
        }

        private void OnAdClicked()
        {
            if (RewardedAds.IsBusy) return;
            RewardedAds.ShowBooster(offeringKind);
        }

        private void WireButtons()
        {
            if (dimmerButton != null)
            {
                dimmerButton.transition = Selectable.Transition.None;
                dimmerButton.onClick.RemoveListener(Close);
                dimmerButton.onClick.AddListener(Close);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (closeXButton != null)
            {
                closeXButton.onClick.RemoveListener(Close);
                closeXButton.onClick.AddListener(Close);
            }

            if (adButton != null)
            {
                adButton.onClick.RemoveListener(OnAdClicked);
                adButton.onClick.AddListener(OnAdClicked);
            }
        }

        private void EnsureHierarchy()
        {
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
                cardRt.sizeDelta = SettingsPanel.OverlayCardSize;
                var cardImage = cardGo.AddComponent<Image>();
                Sprite cardSprite = ResolveCard();
                if (cardSprite != null) cardImage.sprite = cardSprite;
                cardImage.color = Color.white;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
                cardImage.raycastTarget = true;
                card = cardGo.transform;
            }

            if (card.Find("Title") == null)
            {
                titleText = CreateLabel(card, "Title", GameTexts.HintsTitle,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(720f, 72f),
                    TextAnchor.MiddleCenter, 52, GameConstants.NavyText, FontStyle.Bold, false);
            }

            if (card.Find("Message") == null)
            {
                messageText = CreateLabel(card, "Message", DailyPackMessage(),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(800f, 160f),
                    TextAnchor.MiddleCenter, 40, GameConstants.NavyText, FontStyle.Bold, true);
            }

            if (card.Find("Balance") == null)
            {
                balanceText = CreateLabel(card, "Balance", GameTexts.BoosterBalance(0, 0, 0),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(800f, 140f),
                    TextAnchor.MiddleCenter, 36,
                    new Color(GameConstants.NavyText.r, GameConstants.NavyText.g, GameConstants.NavyText.b, 0.78f),
                    FontStyle.Bold, true);
            }

            if (adButton == null)
            {
                Transform existingAd = card.Find("AdButton");
                adButton = existingAd != null ? existingAd.GetComponent<Button>() : null;
            }

            if (adButton == null)
            {
                adButton = CreateFaceButton(card, "AdButton",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -90f), new Vector2(640f, 148f), ResolveBtn(),
                    AdButtonText(BoosterKind.Hint), 36);
            }

            Transform adLabel = adButton != null ? adButton.transform.Find("Label") : null;
            if (adLabel != null) adButtonLabel = adLabel.GetComponent<Text>();

            if (closeButton == null)
            {
                Transform existingClose = card.Find("CloseButton");
                closeButton = existingClose != null ? existingClose.GetComponent<Button>() : null;
            }

            if (closeButton == null)
            {
                closeButton = CreateFaceButton(card, "CloseButton",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -280f), new Vector2(400f, 152f), ResolveBtn(), GameTexts.Ok, 44);
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
                    new Vector2(-18f, -18f), new Vector2(110f, 110f), ResolveClose(), string.Empty, 1);
                Transform label = closeXButton.transform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }

            BindExisting();
            PaintFaces();
        }

        private void BindExisting()
        {
            Transform dimmer = transform.Find("Dimmer");
            if (dimmerButton == null && dimmer != null)
                dimmerButton = dimmer.GetComponent<Button>();

            Transform card = transform.Find("Card");
            if (card == null) return;

            if (titleText == null)
            {
                Transform title = card.Find("Title");
                titleText = title != null ? title.GetComponent<Text>() : null;
            }

            if (messageText == null)
            {
                Transform message = card.Find("Message");
                messageText = message != null ? message.GetComponent<Text>() : null;
            }

            if (balanceText == null)
            {
                Transform balance = card.Find("Balance");
                balanceText = balance != null ? balance.GetComponent<Text>() : null;
            }

            if (adButton == null)
            {
                Transform ad = card.Find("AdButton");
                adButton = ad != null ? ad.GetComponent<Button>() : null;
            }

            if (adButtonLabel == null && adButton != null)
            {
                Transform label = adButton.transform.Find("Label");
                adButtonLabel = label != null ? label.GetComponent<Text>() : null;
            }

            if (closeButton == null)
            {
                Transform close = card.Find("CloseButton");
                closeButton = close != null ? close.GetComponent<Button>() : null;
            }

            if (closeXButton == null)
            {
                Transform closeX = card.Find("CloseXButton");
                closeXButton = closeX != null ? closeX.GetComponent<Button>() : null;
            }
        }

        private void PaintFaces()
        {
            Transform dimmer = transform.Find("Dimmer");
            var dimmerImage = dimmer != null ? dimmer.GetComponent<Image>() : null;
            Sprite dimmerSprite = ResolveDimmer();
            if (dimmerImage != null && dimmerSprite != null)
            {
                dimmerImage.sprite = dimmerSprite;
                dimmerImage.color = Color.white;
            }

            Transform card = transform.Find("Card");
            var cardImage = card != null ? card.GetComponent<Image>() : null;
            Sprite cardSprite = ResolveCard();
            if (cardImage != null && cardSprite != null)
            {
                cardImage.sprite = cardSprite;
                cardImage.color = Color.white;
                cardImage.preserveAspect = true;
            }

            PaintButton(closeButton, ResolveBtn(), false);
            PaintButton(adButton, ResolveBtn(), false);
            PaintButton(closeXButton, ResolveClose(), true);

            if (closeXButton != null)
            {
                Transform label = closeXButton.transform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }
        }

        private static void PaintButton(Button button, Sprite face, bool preserveAspect)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null) return;
            if (face != null) image.sprite = face;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.color = Color.white;
            image.raycastTarget = true;
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
            image.preserveAspect = string.IsNullOrEmpty(label);
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            if (!string.IsNullOrEmpty(label))
            {
                CreateLabel(go.transform, "Label", label,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    TextAnchor.MiddleCenter, fontSize, GameConstants.NavyText, FontStyle.Bold, false);
            }

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
                rt.offsetMin = new Vector2(24f, 18f);
                rt.offsetMax = new Vector2(-24f, -18f);
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(18, fontSize - 16);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void PlaceRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
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
            if (art != null && art.uiCard != null) return art.uiCard;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.uiCard : null;
        }
    }
}
