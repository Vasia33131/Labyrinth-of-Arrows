using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Оверлей магазина: затемнение + карточка, скины, фоны, золото за рекламу.
    /// Объект на сцене (MainMenu и GameScene), не префаб.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        public const string OverlayName = "ShopOverlay";

        /// <summary>
        /// Минимум 130 логических единиц ≈ 45 CSS px на телефоне 412 — крестик и табы
        /// раньше давали 26–39 px и не проходили по п. 1.8.
        /// </summary>
        public static readonly Vector2 CloseButtonSize = OverlayCardFit.CloseSize;
        public const float TabRowHeight = 130f;
        public const string ButtonName = "ShopButton";

        /// <summary>
        /// RectMask2D.padding.y подрезает низ вьюпорта, а ScrollRect этого не знает.
        /// Нижний pad — ровно маска плюс маленький зазор, без пустой прокрутки.
        /// </summary>
        private const float ShopListMaskBottom = 36f;
        private const int ShopListBottomPadding = 40;

        private float lastCharacterPageHeight;
        private float lastBackgroundPageHeight;

        public enum Tab
        {
            Character = 0,
            Background = 1,
            Gold = 2
        }

        [Header("Оверлей")]
        public Button dimmerButton;
        public Button closeXButton;

        [Header("Шапка")]
        public Text titleText;
        public Text goldText;
        public GoldCounterView goldCounter;

        [Header("Вкладки")]
        public Button characterTab;
        public Button backgroundTab;
        public Button goldTab;
        public GameObject characterPage;
        public GameObject backgroundPage;
        public GameObject goldPage;
        public Text placeholderText;

        [Header("Скины")]
        public Transform skinList;
        public Text shopMessage;

        [Header("Фоны")]
        public Transform backgroundList;
        public Text backgroundMessage;

        [Header("Золото")]
        public Button goldAdButton;
        public Text goldMessage;

        public static bool IsOpen { get; private set; }

        private Tab currentTab = Tab.Character;
        private readonly List<CharacterSkinSlotView> skinSlots = new List<CharacterSkinSlotView>();
        private readonly List<BackgroundSlotView> backgroundSlots = new List<BackgroundSlotView>();
        private Coroutine shopMessageRoutine;
        private bool adsEventsBound;

        private void Awake()
        {
            BindRuntime();
            WireButtons();
            BindAdsEvents();
            SelectTab(Tab.Character, true);
            RefreshGold();
            ApplyTexts();
            BindOverlayCard();
        }

        private void OnEnable()
        {
            IsOpen = true;
            Wallet.OnChanged += HandleGoldChanged;
            CharacterSkins.OnChanged += HandleSkinsChanged;
            Backgrounds.OnChanged += HandleBackgroundsChanged;
            GameTexts.OnLanguageChanged += ApplyTexts;
            BindAdsEvents();
            RefreshGold();
            ApplyPlayOrAuthoredLayout();
            BindArt();
            ApplyTexts();
            SelectTab(currentTab, true);
            RefreshSkinSlots();
            RefreshBackgroundSlots();
            RefreshGoldAdButton();
            BindOverlayCard();
        }

        private void OnDisable()
        {
            Wallet.OnChanged -= HandleGoldChanged;
            CharacterSkins.OnChanged -= HandleSkinsChanged;
            Backgrounds.OnChanged -= HandleBackgroundsChanged;
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (IsOpen) IsOpen = false;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            RefitShopListIfPageChanged(characterPage, true, ref lastCharacterPageHeight);
            RefitShopListIfPageChanged(backgroundPage, false, ref lastBackgroundPageHeight);
        }

        /// <summary>
        /// Заголовок, вкладки и кнопка rewarded стоят в сцене — переписываем текст
        /// на языке YG2. Кнопка рекламы называет и рекламу, и награду (п. 4.5.1).
        /// </summary>
        private void ApplyTexts()
        {
            UiLabel.Set(titleText, GameTexts.Shop);
            UiLabel.Set(characterTab, GameTexts.TabCharacter);
            UiLabel.Set(backgroundTab, GameTexts.TabBackground);
            UiLabel.Set(goldTab, GameTexts.TabGold);
            UiLabel.Set(goldAdButton, GameTexts.WatchAdForGold());
            RefreshGold();
            RefreshSkinSlots();
            RefreshBackgroundSlots();
        }

        private void OnDestroy()
        {
            UnbindAdsEvents();
            if (IsOpen) IsOpen = false;
        }

        private void BindOverlayCard()
        {
            OverlayCardFitHost.Ensure(this, closeXButton);
            HideCloseXLabel();
        }

        public void Open()
        {
            BindRuntime();
            GameAudio.BindUiClicks(transform);
            gameObject.SetActive(true);
            ApplyPlayOrAuthoredLayout();
            ApplyTexts();
            RefreshGold();
            RefreshSkinSlots();
            RefreshBackgroundSlots();
            RefreshGoldAdButton();
            RefreshTabVisuals();
            transform.SetAsLastSibling();
            BindOverlayCard();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void ShowCharacterTab() => SelectTab(Tab.Character, false);
        public void ShowBackgroundTab() => SelectTab(Tab.Background, false);
        public void ShowGoldTab() => SelectTab(Tab.Gold, false);

        public void SelectTab(Tab tab, bool force)
        {
            if (!force && currentTab == tab)
            {
                RefreshTabVisuals();
                return;
            }

            currentTab = tab;
            if (characterPage != null) characterPage.SetActive(tab == Tab.Character);
            if (backgroundPage != null) backgroundPage.SetActive(tab == Tab.Background);
            if (goldPage != null) goldPage.SetActive(tab == Tab.Gold);
            RefreshTabVisuals();
            if (tab == Tab.Character)
            {
                RefreshSkinSlots();
                EnsureCharacterSkinScroll();
                ResetCharacterSkinScroll();
            }
            if (tab == Tab.Background)
            {
                RefreshBackgroundSlots();
                EnsureBackgroundScroll();
                ResetBackgroundScroll();
            }
            if (tab == Tab.Gold) RefreshGoldAdButton();
        }

        public void RefreshGold()
        {
            if (goldCounter != null)
            {
                goldCounter.Refresh();
                return;
            }

            if (goldText != null) goldText.text = GameTexts.Gold(Wallet.Gold);
        }

        private void HandleGoldChanged(int _)
        {
            RefreshGold();
            RefreshSkinSlots();
            RefreshBackgroundSlots();
        }

        private void HandleSkinsChanged()
        {
            RefreshSkinSlots();
        }

        private void HandleBackgroundsChanged()
        {
            RefreshBackgroundSlots();
        }

        public void RefreshSkinSlots()
        {
            CollectSkinSlots();
            if (Application.isPlaying && (skinList == null || skinSlots.Count == 0))
                AuthoredUi.Missing("SkinSlot_*");

            IReadOnlyList<CharacterSkinData> skins = CharacterSkinCatalog.Current.Skins;
            for (int i = 0; i < skins.Count; i++)
            {
                CharacterSkinData skin = skins[i];
                if (skin == null) continue;

                CharacterSkinSlotView slot = FindSkinSlot(skin.id);
                if (slot == null) continue;

                string id = skin.id;
                slot.gameObject.SetActive(true);
                slot.Bind(skin, CharacterSkins.IsUnlocked(id), CharacterSkins.IsSelected(id),
                    () => HandleSkinClicked(id));
            }
        }

        public void RefreshBackgroundSlots()
        {
            CollectBackgroundSlots();
            if (Application.isPlaying && (backgroundList == null || backgroundSlots.Count == 0))
                AuthoredUi.Missing("BackgroundSlot_*");
            IReadOnlyList<BackgroundData> items = BackgroundCatalog.Current.Backgrounds;
            for (int i = 0; i < backgroundSlots.Count && i < items.Count; i++)
            {
                BackgroundData background = items[i];
                BackgroundSlotView slot = backgroundSlots[i];
                if (slot == null || background == null) continue;

                string id = background.id;
                slot.Bind(background, Backgrounds.IsUnlocked(id), Backgrounds.IsSelected(id),
                    () => HandleBackgroundClicked(id));
            }
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

            if (characterTab != null)
            {
                characterTab.onClick.RemoveListener(ShowCharacterTab);
                characterTab.onClick.AddListener(ShowCharacterTab);
            }

            if (backgroundTab != null)
            {
                backgroundTab.onClick.RemoveListener(ShowBackgroundTab);
                backgroundTab.onClick.AddListener(ShowBackgroundTab);
            }

            if (goldTab != null)
            {
                goldTab.onClick.RemoveListener(ShowGoldTab);
                goldTab.onClick.AddListener(ShowGoldTab);
            }

            WireGoldAdButton();
        }

        private void WireGoldAdButton()
        {
            if (goldAdButton == null) return;
            goldAdButton.onClick.RemoveListener(HandleGoldAdClicked);
            goldAdButton.onClick.AddListener(HandleGoldAdClicked);
            RefreshGoldAdButton();
        }

        private void BindAdsEvents()
        {
            if (adsEventsBound) return;
            adsEventsBound = true;
            RewardedAds.OnStateChanged += HandleAdsStateChanged;
            RewardedAds.OnFailed += HandleAdsFailed;
        }

        private void UnbindAdsEvents()
        {
            if (!adsEventsBound) return;
            adsEventsBound = false;
            RewardedAds.OnStateChanged -= HandleAdsStateChanged;
            RewardedAds.OnFailed -= HandleAdsFailed;
        }

        private void HandleAdsStateChanged()
        {
            if (!isActiveAndEnabled) return;
            RefreshSkinSlots();
            RefreshGoldAdButton();
        }

        private void HandleAdsFailed(string _)
        {
            ShowShopMessage(RewardedAds.FailMessage);
        }

        private void HandleGoldAdClicked()
        {
            if (RewardedAds.IsBusy) return;
            RewardedAds.ShowGold300();
        }

        public void RefreshGoldAdButton()
        {
            if (goldAdButton == null) return;
            goldAdButton.interactable = !RewardedAds.IsBusy;
        }

        private void RefreshTabVisuals()
        {
            if (Application.isPlaying && SceneCanvas.LayoutLocked)
            {
                PaintTabChrome(characterTab, currentTab == Tab.Character, false);
                PaintTabChrome(backgroundTab, currentTab == Tab.Background, false);
                PaintTabChrome(goldTab, currentTab == Tab.Gold, false);
                return;
            }

            LayoutTabs();
        }

        private void LayoutTabs()
        {
            const float gap = 8f;
            StretchTab(characterTab, 0, currentTab == Tab.Character, gap);
            StretchTab(backgroundTab, 1, currentTab == Tab.Background, gap);
            StretchTab(goldTab, 2, currentTab == Tab.Gold, gap);
        }

        private static void StretchTab(Button button, int index, bool selected, float gap)
        {
            if (button == null) return;

            var rt = button.transform as RectTransform;
            if (rt != null)
            {
                float pad = gap * 0.5f;
                rt.anchorMin = new Vector2(index / 3f, 0f);
                rt.anchorMax = new Vector2((index + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(index == 0 ? 0f : pad, 0f);
                rt.offsetMax = new Vector2(index == 2 ? 0f : -pad, 0f);
                rt.localScale = Vector3.one;
            }

            PaintTabChrome(button, selected, true);
        }

        private static void PaintTabChrome(Button button, bool selected, bool stretchFace)
        {
            if (button == null) return;

            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                if (stretchFace)
                {
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                }

                image.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.82f);
            }

            var label = button.transform.Find("Label");
            var text = label != null ? label.GetComponent<Text>() : null;
            if (text == null) text = button.GetComponentInChildren<Text>(true);
            if (text == null) return;

            int targetFont = selected ? 28 : 24;
            text.fontSize = targetFont;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = selected ? 16 : 14;
            text.resizeTextMaxSize = targetFont;
            text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            Color navy = GameConstants.NavyText;
            Color dim = new Color(navy.r, navy.g, navy.b, 0.72f);
            text.color = selected ? navy : dim;
        }

        private void BindArt()
        {
            if (Application.isPlaying && SceneCanvas.ArtLocked)
            {
                FillEmptyShopButtons();
                return;
            }

            Sprite btn = ResolveBtn();
            Sprite close = ResolveClose();
            Sprite dimmer = ResolveDimmer();
            Sprite card = ResolveCard();

            PaintImage(FindImage("Dimmer"), dimmer, false, true);
            PaintImage(FindImage("Card"), card, true, true);
            PaintButtonFace(closeXButton, close, true);
            PaintButtonFace(characterTab, btn, false);
            PaintButtonFace(backgroundTab, btn, false);
            PaintButtonFace(goldTab, btn, false);
            PaintButtonFace(goldAdButton, btn, false);
            PaintChildActionButtons(btn);
            HideCloseXLabel();
        }

        private void FillEmptyShopButtons()
        {
            Sprite btn = ResolveBtn();
            Sprite close = ResolveClose();
            PaintButtonFaceIfEmpty(closeXButton, close, true);
            PaintButtonFaceIfEmpty(characterTab, btn, false);
            PaintButtonFaceIfEmpty(backgroundTab, btn, false);
            PaintButtonFaceIfEmpty(goldTab, btn, false);
            PaintButtonFaceIfEmpty(goldAdButton, btn, false);

            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button == dimmerButton || button == closeXButton) continue;
                if (button == characterTab || button == backgroundTab || button == goldTab || button == goldAdButton)
                    continue;
                PaintButtonFaceIfEmpty(button, btn, false);
            }

            HideCloseXLabel();
        }

        private static void PaintButtonFaceIfEmpty(Button button, Sprite sprite, bool preserveAspect)
        {
            if (button == null || sprite == null) return;
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null || image.sprite != null) return;
            PaintButtonFace(button, sprite, preserveAspect);
        }

        private void PaintChildActionButtons(Sprite btn)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button == dimmerButton || button == closeXButton) continue;
                if (button == characterTab || button == backgroundTab || button == goldTab || button == goldAdButton)
                    continue;
                PaintButtonFace(button, btn, false);
            }
        }

        private Image FindImage(string name)
        {
            Transform found = transform.Find(name);
            return found != null ? found.GetComponent<Image>() : null;
        }

        private static void PaintImage(Image image, Sprite sprite, bool preserveAspect, bool raycast)
        {
            if (image == null) return;
            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycast;
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

        private void HideCloseXLabel()
        {
            if (closeXButton == null) return;
            Transform label = closeXButton.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
        }

        private void BindRuntime()
        {
            BindExistingRefs();
            if (goldCounter == null && goldText != null)
                goldCounter = goldText.GetComponent<GoldCounterView>();
            CollectSkinSlots();
            CollectBackgroundSlots();
            BindArt();
            if (!Application.isPlaying) return;
            ApplyPlayOrAuthoredLayout();

            if (transform.Find("Dimmer") == null) AuthoredUi.Missing("Dimmer");
            if (FindCard() == null) AuthoredUi.Missing("Card");
            if (skinList == null) AuthoredUi.Missing("SkinList");
            if (backgroundList == null) AuthoredUi.Missing("BackgroundList");
            if (goldPage == null) AuthoredUi.Missing("GoldPage");
        }

        private void EnsureChildren()
        {
            if (!AuthoredUi.CanBuild)
            {
                BindRuntime();
                return;
            }

            if (transform.Find("Dimmer") == null || transform.Find("Card") == null)
                BuildMissingHierarchy();

            BindExistingRefs();
            RepairAuthoredHierarchy();
            BindExistingRefs();
            if (goldCounter == null && goldText != null)
                goldCounter = GoldCounterView.Bind(goldText);
            EnsureSkinPage();
            EnsureBackgroundPage();
            EnsureGoldPage();
            BindArt();
            if (!SceneCanvas.LayoutLocked) ApplyShopLayout();
        }

        private void BindExistingRefs()
        {
            Transform dimmer = transform.Find("Dimmer");
            if (dimmerButton == null && dimmer != null)
                dimmerButton = dimmer.GetComponent<Button>();

            Transform card = FindCard();
            if (card == null) return;

            if (titleText == null)
            {
                Transform title = card.Find("Title");
                if (title != null) titleText = title.GetComponent<Text>();
            }

            if (goldText == null)
            {
                Transform gold = card.Find("ShopGoldText");
                if (gold == null) gold = card.Find("GoldText");
                if (gold != null) goldText = gold.GetComponent<Text>();
            }

            if (closeXButton == null)
            {
                Transform close = card.Find("CloseXButton");
                if (close != null) closeXButton = close.GetComponent<Button>();
            }

            Transform tabs = card.Find("Tabs");
            if (characterTab == null && tabs != null)
            {
                Transform tab = tabs.Find("CharacterTab");
                if (tab != null) characterTab = tab.GetComponent<Button>();
            }

            if (backgroundTab == null && tabs != null)
            {
                Transform tab = tabs.Find("BackgroundTab");
                if (tab != null) backgroundTab = tab.GetComponent<Button>();
            }

            if (goldTab == null && tabs != null)
            {
                Transform tab = tabs.Find("GoldTab");
                if (tab != null) goldTab = tab.GetComponent<Button>();
            }

            Transform pages = card.Find("Pages");
            if (characterPage == null && pages != null)
            {
                Transform page = pages.Find("CharacterPage");
                if (page != null) characterPage = page.gameObject;
            }

            if (backgroundPage == null && pages != null)
            {
                Transform page = pages.Find("BackgroundPage");
                if (page != null) backgroundPage = page.gameObject;
            }

            if (goldPage == null && pages != null)
            {
                Transform page = pages.Find("GoldPage");
                if (page != null) goldPage = page.gameObject;
            }

            if (placeholderText == null)
            {
                Transform placeholder = characterPage != null ? characterPage.transform.Find("Placeholder") : null;
                if (placeholder != null) placeholderText = placeholder.GetComponent<Text>();
            }

            if (skinList == null)
            {
                Transform list = characterPage != null ? characterPage.transform.Find("SkinList") : null;
                if (list == null) list = AuthoredUi.FindDeep(transform, "SkinList");
                if (list != null) skinList = list;
            }

            if (shopMessage == null && characterPage != null)
            {
                Transform message = characterPage.transform.Find("ShopMessage");
                if (message != null) shopMessage = message.GetComponent<Text>();
            }

            if (backgroundList == null && backgroundPage != null)
            {
                Transform list = backgroundPage.transform.Find("BackgroundList");
                if (list != null) backgroundList = list;
            }

            if (backgroundMessage == null && backgroundPage != null)
            {
                Transform message = backgroundPage.transform.Find("ShopMessage");
                if (message != null) backgroundMessage = message.GetComponent<Text>();
            }

            if (goldAdButton == null && goldPage != null)
            {
                Transform button = goldPage.transform.Find("WatchAdGoldButton");
                if (button != null) goldAdButton = button.GetComponent<Button>();
            }

            if (goldMessage == null && goldPage != null)
            {
                Transform message = goldPage.transform.Find("ShopMessage");
                if (message != null) goldMessage = message.GetComponent<Text>();
            }
        }

        private Transform FindCard()
        {
            Transform card = transform.Find("Card");
            if (card != null) return card;
            if (closeXButton != null) return closeXButton.transform.parent;
            if (titleText != null) return titleText.transform.parent;
            return null;
        }

        /// <summary>
        /// Слоты и страницы должны жить внутри Card: иначе после правок в Unity
        /// SkinList пустеет, а SkinSlot_* оказываются сиблингами карточки.
        /// </summary>
        public void RepairAuthoredHierarchy()
        {
            Transform card = FindCard();
            if (card == null) return;

            Adopt(card, FindOwn("Title"));
            Transform gold = FindOwn("ShopGoldText");
            if (gold == null) gold = FindOwn("GoldText");
            Adopt(card, gold);
            Adopt(card, FindOwn("CloseXButton"));

            Transform tabs = FindOwn("Tabs");
            Adopt(card, tabs);

            Transform pages = FindOwn("Pages");
            if (pages == null && AuthoredUi.CanBuild)
            {
                var pagesGo = new GameObject("Pages", typeof(RectTransform));
                pagesGo.transform.SetParent(card, false);
                pages = pagesGo.transform;
            }

            Adopt(card, pages);

            Transform character = characterPage != null ? characterPage.transform : FindOwn("CharacterPage");
            Transform background = backgroundPage != null ? backgroundPage.transform : FindOwn("BackgroundPage");
            Transform goldPageTf = goldPage != null ? goldPage.transform : FindOwn("GoldPage");
            if (pages != null)
            {
                Adopt(pages, character);
                Adopt(pages, background);
                Adopt(pages, goldPageTf);
            }

            if (character != null)
            {
                if (characterPage == null) characterPage = character.gameObject;
                Transform list = skinList != null ? skinList : FindOwn("SkinList");
                Adopt(character, list);
                if (list != null) skinList = list;

                Transform message = shopMessage != null ? shopMessage.transform : null;
                if (message == null)
                {
                    Transform named = character.Find("ShopMessage");
                    if (named == null) named = FindOwn("ShopMessage");
                    message = named;
                }

                if (message != null && message.parent != background && message.parent != goldPageTf)
                    Adopt(character, message);
            }

            ReparentSkinSlotsToList();
            RestoreDefaultSlotChildren();
            StretchFill(pages as RectTransform, 108f, 136f, 108f, 472f);
            StretchFill(character as RectTransform, 0f, 0f, 0f, 0f);
            StretchFill(background as RectTransform, 0f, 0f, 0f, 0f);
            StretchFill(goldPageTf as RectTransform, 0f, 0f, 0f, 0f);
            PlaceSkinListRect();
        }

        private void ReparentSkinSlotsToList()
        {
            if (skinList == null) return;

            var found = new List<CharacterSkinSlotView>();
            transform.GetComponentsInChildren(true, found);
            for (int i = 0; i < found.Count; i++)
            {
                CharacterSkinSlotView slot = found[i];
                if (slot == null) continue;
                Adopt(skinList, slot.transform);
            }
        }

        private void RestoreDefaultSlotChildren()
        {
            CharacterSkinSlotView slot = null;
            var found = new List<CharacterSkinSlotView>();
            transform.GetComponentsInChildren(true, found);
            for (int i = 0; i < found.Count; i++)
            {
                CharacterSkinSlotView candidate = found[i];
                if (candidate == null) continue;
                if (candidate.skinId == GameConstants.DefaultSkinId
                    || candidate.gameObject.name == "SkinSlot_" + GameConstants.DefaultSkinId)
                {
                    slot = candidate;
                    break;
                }
            }

            if (slot == null || slot.transform.childCount > 0) return;

            Adopt(slot.transform, slot.preview != null ? slot.preview.transform : null);
            Adopt(slot.transform, slot.nameLabel != null ? slot.nameLabel.transform : null);
            Adopt(slot.transform, slot.actionButton != null ? slot.actionButton.transform : null);
            Adopt(slot.transform, slot.wornBadge != null ? slot.wornBadge.transform : null);
        }

        private Transform FindOwn(string name)
        {
            return AuthoredUi.FindDeep(transform, name);
        }

        private static void Adopt(Transform parent, Transform child)
        {
            if (parent == null || child == null || child.parent == parent) return;
            child.SetParent(parent, false);
        }

        private static void StretchFill(RectTransform rt, float left, float bottom, float right, float top)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            rt.localScale = Vector3.one;
        }

        private void PlaceSkinListRect()
        {
            if (!WireShopPageScroll(characterPage, skinList, shopMessage)) return;
            ApplyAuthoredShopGrid(skinList);
            FitShopListContent(skinList as RectTransform);
        }

        private void EnsureCharacterSkinScroll()
        {
            if (!WireShopPageScroll(characterPage, skinList, shopMessage)) return;
            FitShopListContent(skinList as RectTransform);
        }

        private void ApplyShopLayout()
        {
            Transform card = FindCard();
            if (card == null) return;

            var cardRt = card as RectTransform;
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta = OverlayCardFit.CardSize;
                cardRt.localScale = Vector3.one;
            }

            PlaceRect(card.Find("Title") as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -255f), new Vector2(510f, 56f));
            if (titleText != null)
            {
                titleText.fontSize = 44;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMaxSize = 44;
            }

            RectTransform goldRt = card.Find("ShopGoldText") as RectTransform;
            if (goldRt == null) goldRt = card.Find("GoldText") as RectTransform;
            PlaceRect(goldRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -314f), new Vector2(456f, 36f));

            if (closeXButton != null)
            {
                OverlayCardFit.PlaceCloseX(closeXButton.transform as RectTransform, card as RectTransform);
                HideCloseXLabel();
            }

            Transform tabs = card.Find("Tabs");
            PlaceRect(tabs as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -348f), new Vector2(844f, TabRowHeight));
            LayoutTabs();

            Transform pages = card.Find("Pages");
            StretchFill(pages as RectTransform, 108f, 136f, 108f, 472f);
            PlaceSkinListRect();
            PlaceBackgroundListRect();
            PlaceGoldAdButton();
        }

        private void PlaceBackgroundListRect()
        {
            if (!WireShopPageScroll(backgroundPage, backgroundList, backgroundMessage)) return;
            ApplyAuthoredShopGrid(backgroundList);
            FitShopListContent(backgroundList as RectTransform);
        }

        private void EnsureBackgroundScroll()
        {
            if (!WireShopPageScroll(backgroundPage, backgroundList, backgroundMessage)) return;
            FitShopListContent(backgroundList as RectTransform);
        }

        private void RefitShopListIfPageChanged(GameObject page, bool character, ref float lastHeight)
        {
            var pageRt = page != null ? page.transform as RectTransform : null;
            if (pageRt == null) return;

            float height = pageRt.rect.height;
            if (Mathf.Abs(height - lastHeight) < 0.5f) return;
            lastHeight = height;
            if (!page.activeInHierarchy) return;

            if (character) EnsureCharacterSkinScroll();
            else EnsureBackgroundScroll();
        }

        private static bool WireShopPageScroll(GameObject page, Transform list, Text message)
        {
            if (page == null || list == null) return false;

            var pageRt = page.transform as RectTransform;
            var contentRt = list as RectTransform;
            if (pageRt == null || contentRt == null) return false;

            var image = page.GetComponent<Image>();
            if (image == null) image = page.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var mask = page.GetComponent<RectMask2D>();
            if (mask == null) mask = page.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0f, ShopListMaskBottom, 0f, 0f);

            if (message != null) message.maskable = false;

            var scroll = page.GetComponent<ScrollRect>();
            if (scroll == null) scroll = page.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = pageRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 45f;
            scroll.verticalScrollbar = null;
            scroll.horizontalScrollbar = null;
            return true;
        }

        private static void ApplyAuthoredShopGrid(Transform list)
        {
            if (list == null) return;
            var layout = list.GetComponent<GridLayoutGroup>();
            if (layout == null) return;
            layout.padding = new RectOffset(8, 8, 18, ShopListBottomPadding);
            layout.cellSize = new Vector2(400f, 540f);
            layout.spacing = new Vector2(12f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        private static void FitShopListContent(RectTransform content)
        {
            if (content == null) return;

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
            content.localScale = Vector3.one;

            var layout = content.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                RectOffset pad = layout.padding;
                if (pad.bottom != ShopListBottomPadding)
                    layout.padding = new RectOffset(pad.left, pad.right, pad.top, ShopListBottomPadding);
            }

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void PlaceGoldAdButton()
        {
            if (goldAdButton == null) return;
            PlaceRect(goldAdButton.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(510f, 100f));
            Text label = goldAdButton.GetComponentInChildren<Text>(true);
            if (label == null) return;
            label.fontSize = 28;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 28;
        }

        private void ApplyPlayOrAuthoredLayout()
        {
            if (Application.isPlaying && SceneCanvas.LayoutLocked)
            {
                EnsureCharacterSkinScroll();
                EnsureBackgroundScroll();
                BindOverlayCard();
                return;
            }

            ApplyShopLayout();
            BindOverlayCard();
        }

        public void ApplyAuthoredShopLayout()
        {
            ApplyShopLayout();
            BindArt();
            CollectSkinSlots();
            for (int i = 0; i < skinSlots.Count; i++)
            {
                if (skinSlots[i] != null) skinSlots[i].ApplyAuthoredLayout();
            }

            CollectBackgroundSlots();
            for (int i = 0; i < backgroundSlots.Count; i++)
            {
                if (backgroundSlots[i] != null) backgroundSlots[i].ApplyAuthoredLayout();
            }
        }

        private void RepairExistingLayout()
        {
            ApplyShopLayout();
        }

        private static void PlaceTab(Button button, float x)
        {
            if (button == null) return;
            PlaceRect(button.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(236f, TabRowHeight));
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

        public static ShopPanel EnsureOnCanvas(Transform canvas)
        {
            if (canvas == null) return null;

            ShopPanel existing = FindOn(canvas);
            if (existing != null)
            {
                existing.EnsureChildren();
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

        public static ShopPanel FindOn(Transform canvas)
        {
            if (canvas == null) return null;

            Transform root = BannerSafeArea.ResolveOverlayRoot(canvas);
            Transform named = root != null ? root.Find(OverlayName) : null;
            if (named == null) named = canvas.Find(OverlayName);
            if (named == null) named = AuthoredUi.FindDeep(canvas, OverlayName);
            if (named != null)
                return AuthoredUi.ExistingComponent<ShopPanel>(named.gameObject, OverlayName);

            return canvas.GetComponentInChildren<ShopPanel>(true);
        }

        public static Button FindShopButton(Transform canvas)
        {
            Transform named = FindNamed(canvas, ButtonName);
            return named != null ? named.GetComponent<Button>() : null;
        }

        public static Button EnsureShopButton(Transform canvas, bool menuStyle)
        {
            if (canvas == null) return null;

            Button existing = FindShopButton(canvas);
            if (existing != null) return existing;

            if (!AuthoredUi.CanBuild)
            {
                AuthoredUi.Missing(ButtonName);
                return null;
            }

            return CreateShopButton(canvas, menuStyle);
        }

        public static Button CreateShopButton(Transform canvas, bool menuStyle)
        {
            var go = new GameObject(ButtonName, typeof(RectTransform));
            go.transform.SetParent(BannerSafeArea.ResolveContentRoot(canvas), false);

            var rt = go.GetComponent<RectTransform>();
            if (menuStyle)
            {
                RectTransform settings = FindNamed(canvas, "SettingsButton") as RectTransform;
                if (settings != null)
                {
                    rt.anchorMin = settings.anchorMin;
                    rt.anchorMax = settings.anchorMax;
                    rt.pivot = settings.pivot;
                    rt.sizeDelta = settings.sizeDelta;
                    float step = Mathf.Max(180f, settings.sizeDelta.y * 0.72f);
                    rt.anchoredPosition = settings.anchoredPosition + new Vector2(0f, -step);
                }
                else
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, -360f);
                    rt.sizeDelta = new Vector2(800f, 250f);
                }
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-16f, -140f);
                rt.sizeDelta = new Vector2(240f, 88f);
            }

            var image = go.AddComponent<Image>();
            Sprite btn = ResolveBtn();
            if (btn != null) image.sprite = btn;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            CreateLabel(go.transform, "Label", GameTexts.Shop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter, menuStyle ? 44 : 32, GameConstants.NavyText, FontStyle.Bold);

            return button;
        }

        private static ShopPanel CreateOverlay(Transform canvas)
        {
            var overlay = new GameObject(OverlayName, typeof(RectTransform));
            overlay.transform.SetParent(BannerSafeArea.ResolveOverlayRoot(canvas), false);
            Stretch(overlay.GetComponent<RectTransform>());

            var panel = overlay.AddComponent<ShopPanel>();
            panel.BuildMissingHierarchy();
            panel.BindExistingRefs();
            panel.BindArt();
            panel.WireButtons();
            panel.SelectTab(Tab.Character, true);
            panel.RefreshGold();
            overlay.SetActive(false);
            return panel;
        }

        private void BuildMissingHierarchy()
        {
            if (transform.Find("Dimmer") == null)
            {
                var dimmerGo = new GameObject("Dimmer", typeof(RectTransform));
                dimmerGo.transform.SetParent(transform, false);
                Stretch(dimmerGo.GetComponent<RectTransform>());
                var dimmerImage = dimmerGo.AddComponent<Image>();
                Sprite dimmerSprite = ResolveDimmer();
                if (dimmerSprite != null) dimmerImage.sprite = dimmerSprite;
                dimmerImage.color = Color.white;
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

            if (card.Find("Title") == null)
            {
                titleText = CreateLabel(card, "Title", GameTexts.Shop,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(560f, 72f),
                    TextAnchor.MiddleCenter, 52, GameConstants.NavyText, FontStyle.Bold);
            }

            if (card.Find("ShopGoldText") == null && card.Find("GoldText") == null)
            {
                goldText = CreateLabel(card, "ShopGoldText", GameTexts.Gold(Wallet.Gold),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(560f, 48f),
                    TextAnchor.MiddleCenter, 36, new Color(
                        GameConstants.NavyText.r, GameConstants.NavyText.g, GameConstants.NavyText.b, 0.78f),
                    FontStyle.Bold);
                goldCounter = GoldCounterView.Bind(goldText);
            }

            if (card.Find("CloseXButton") == null)
            {
                closeXButton = CreateFaceButton(card, "CloseXButton",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    OverlayCardFit.CloseOffset, OverlayCardFit.CloseSize, ResolveClose(), string.Empty, 1);
                OverlayCardFit.PlaceCloseX(closeXButton.transform as RectTransform, card as RectTransform);
                HideCloseXLabel();
            }

            Transform tabs = card.Find("Tabs");
            if (tabs == null)
            {
                var tabsGo = new GameObject("Tabs", typeof(RectTransform));
                tabsGo.transform.SetParent(card, false);
                PlaceRect(tabsGo.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(760f, TabRowHeight));
                tabs = tabsGo.transform;
            }

            if (tabs.Find("CharacterTab") == null)
                characterTab = CreateFaceButton(tabs, "CharacterTab",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-248f, 0f), new Vector2(236f, TabRowHeight), ResolveBtn(), GameTexts.TabCharacter, 28);

            if (tabs.Find("BackgroundTab") == null)
                backgroundTab = CreateFaceButton(tabs, "BackgroundTab",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 0f), new Vector2(236f, TabRowHeight), ResolveBtn(), GameTexts.TabBackground, 28);

            if (tabs.Find("GoldTab") == null)
                goldTab = CreateFaceButton(tabs, "GoldTab",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(248f, 0f), new Vector2(236f, TabRowHeight), ResolveBtn(), GameTexts.TabGold, 28);

            Transform pages = card.Find("Pages");
            if (pages == null)
            {
                var pagesGo = new GameObject("Pages", typeof(RectTransform));
                pagesGo.transform.SetParent(card, false);
                PlaceRect(pagesGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(720f, 420f));
                pages = pagesGo.transform;
            }

            if (pages.Find("CharacterPage") == null)
                characterPage = CreatePage(pages, "CharacterPage");
            if (pages.Find("BackgroundPage") == null)
                backgroundPage = CreatePage(pages, "BackgroundPage");
            if (pages.Find("GoldPage") == null)
                goldPage = CreatePage(pages, "GoldPage");

            if (placeholderText == null && characterPage != null)
            {
                Transform placeholder = characterPage.transform.Find("Placeholder");
                if (placeholder != null) placeholderText = placeholder.GetComponent<Text>();
            }
        }

        private static GameObject CreatePage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        public void EnsureSkinListForEditor()
        {
            EnsureChildren();
            if (characterPage == null) return;

            Transform placeholder = characterPage.transform.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            if (skinList == null)
            {
                Transform existing = characterPage.transform.Find("SkinList");
                skinList = existing != null ? existing : CreateSkinList(characterPage.transform);
            }

            if (shopMessage == null)
            {
                Transform existing = characterPage.transform.Find("ShopMessage");
                if (existing != null) shopMessage = existing.GetComponent<Text>();
                else shopMessage = CreateShopMessage(characterPage.transform);
            }

            CollectSkinSlots();
        }

        private void EnsureSkinPage()
        {
            if (characterPage == null) return;

            Transform placeholder = characterPage.transform.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            if (skinList == null)
            {
                Transform existing = characterPage.transform.Find("SkinList");
                if (existing != null) skinList = existing;
                else if (AuthoredUi.CanBuild) skinList = CreateSkinList(characterPage.transform);
                else AuthoredUi.Missing("SkinList");
            }

            if (shopMessage == null)
            {
                Transform existing = characterPage.transform.Find("ShopMessage");
                if (existing != null) shopMessage = existing.GetComponent<Text>();
            }

            CollectSkinSlots();
        }

        private void EnsureBackgroundPage()
        {
            if (backgroundPage == null) return;

            Transform placeholder = backgroundPage.transform.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            if (backgroundList == null)
            {
                Transform existing = backgroundPage.transform.Find("BackgroundList");
                if (existing != null) backgroundList = existing;
                else if (AuthoredUi.CanBuild) backgroundList = CreateBackgroundList(backgroundPage.transform);
                else AuthoredUi.Missing("BackgroundList");
            }

            if (backgroundMessage == null)
            {
                Transform existing = backgroundPage.transform.Find("ShopMessage");
                if (existing != null) backgroundMessage = existing.GetComponent<Text>();
                else if (AuthoredUi.CanBuild) backgroundMessage = CreateShopMessage(backgroundPage.transform);
                else AuthoredUi.Missing("BackgroundPage/ShopMessage");
            }

            if (AuthoredUi.CanBuild) BuildBackgroundSlots();
            else CollectBackgroundSlots();
        }

        private void EnsureGoldPage()
        {
            if (goldPage == null) return;

            Transform placeholder = goldPage.transform.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            if (goldAdButton == null)
            {
                Transform existing = goldPage.transform.Find("WatchAdGoldButton");
                if (existing != null) goldAdButton = existing.GetComponent<Button>();
                else if (AuthoredUi.CanBuild) goldAdButton = CreateGoldAdButton(goldPage.transform);
                else AuthoredUi.Missing("WatchAdGoldButton");
            }

            if (goldMessage == null)
            {
                Transform existing = goldPage.transform.Find("ShopMessage");
                if (existing != null) goldMessage = existing.GetComponent<Text>();
                else if (AuthoredUi.CanBuild) goldMessage = CreateShopMessage(goldPage.transform);
            }

            WireGoldAdButton();
        }

        private static Button CreateGoldAdButton(Transform page)
        {
            Button button = CreateFaceButton(page, "WatchAdGoldButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(640f, 120f), ResolveBtn(),
                GameTexts.WatchAdForGold(), 32);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 18;
                label.resizeTextMaxSize = 32;
            }

            return button;
        }

        private static Transform CreateSkinList(Transform page)
        {
            var go = new GameObject("SkinList", typeof(RectTransform));
            go.transform.SetParent(page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 40f);
            rt.offsetMax = new Vector2(-4f, -4f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var layout = go.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 18, ShopListBottomPadding);
            layout.cellSize = new Vector2(400f, 540f);
            layout.spacing = new Vector2(12f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            return go.transform;
        }

        private void ResetCharacterSkinScroll()
        {
            if (characterPage == null) return;
            var scroll = characterPage.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private static Transform CreateBackgroundList(Transform page)
        {
            var go = new GameObject("BackgroundList", typeof(RectTransform));
            go.transform.SetParent(page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 40f);
            rt.offsetMax = new Vector2(-4f, -4f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var layout = go.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 18, ShopListBottomPadding);
            layout.cellSize = new Vector2(400f, 540f);
            layout.spacing = new Vector2(12f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            return go.transform;
        }

        private void ResetBackgroundScroll()
        {
            if (backgroundPage == null) return;
            var scroll = backgroundPage.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private static Text CreateShopMessage(Transform page)
        {
            Text text = CreateLabel(page, "ShopMessage", string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(680f, 32f),
                TextAnchor.MiddleCenter, 26, GameConstants.NavyText, FontStyle.Bold);
            text.gameObject.SetActive(false);
            return text;
        }

        private void CollectSkinSlots()
        {
            skinSlots.Clear();
            if (skinList != null)
                skinList.GetComponentsInChildren(true, skinSlots);
            if (skinSlots.Count == 0)
            {
                Transform card = FindCard();
                if (card != null) card.GetComponentsInChildren(true, skinSlots);
            }
        }

        private void CollectBackgroundSlots()
        {
            backgroundSlots.Clear();
            if (backgroundList != null)
                backgroundList.GetComponentsInChildren(true, backgroundSlots);
            if (backgroundSlots.Count == 0)
            {
                Transform card = FindCard();
                if (card != null) card.GetComponentsInChildren(true, backgroundSlots);
            }
        }

        private CharacterSkinSlotView FindSkinSlot(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < skinSlots.Count; i++)
            {
                CharacterSkinSlotView slot = skinSlots[i];
                if (slot == null) continue;
                if (slot.skinId == id) return slot;
                if (slot.gameObject.name == "SkinSlot_" + id) return slot;
            }

            return null;
        }

        private void BuildBackgroundSlots()
        {
            if (backgroundList == null) return;

            CollectBackgroundSlots();
            if (!AuthoredUi.CanBuild) return;

            IReadOnlyList<BackgroundData> items = BackgroundCatalog.Current.Backgrounds;
            for (int i = 0; i < items.Count; i++)
            {
                BackgroundData background = items[i];
                if (background == null) continue;
                if (HasBackgroundSlot(background.id)) continue;
                backgroundSlots.Add(BackgroundSlotView.Create(backgroundList, background));
            }
        }

        private bool HasBackgroundSlot(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < backgroundSlots.Count; i++)
            {
                BackgroundSlotView slot = backgroundSlots[i];
                if (slot == null) continue;
                if (slot.backgroundId == id) return true;
                if (slot.gameObject.name == "BackgroundSlot_" + id) return true;
            }

            return false;
        }

        private void HandleSkinClicked(string id)
        {
            CharacterSkinData skin = CharacterSkinCatalog.Current.Get(id);
            if (skin == null) return;

            if (CharacterSkins.IsSelected(id)) return;

            if (CharacterSkins.IsUnlocked(id))
            {
                CharacterSkins.Select(id);
                GameAudio.Play(GameAudio.Sfx.Equip);
                return;
            }

            if (skin.unlockByBattlePass)
            {
                GameAudio.Play(GameAudio.Sfx.BuyFail);
                ShowShopMessage(GameTexts.BattlePassLocked);
                return;
            }

            if (!CharacterSkins.TryBuyAndEquip(id, out string failMessage))
            {
                GameAudio.Play(GameAudio.Sfx.BuyFail);
                ShowShopMessage(string.IsNullOrEmpty(failMessage) ? GameTexts.NotEnoughGold : failMessage);
                return;
            }

            GameAudio.Play(GameAudio.Sfx.BuyOk);
        }

        private void HandleBackgroundClicked(string id)
        {
            BackgroundData background = BackgroundCatalog.Current.Get(id);
            if (background == null) return;

            if (Backgrounds.IsSelected(id)) return;

            if (Backgrounds.IsUnlocked(id))
            {
                Backgrounds.Select(id);
                GameAudio.Play(GameAudio.Sfx.Equip);
                return;
            }

            if (background.unlockByBattlePass)
            {
                GameAudio.Play(GameAudio.Sfx.BuyFail);
                ShowShopMessage(GameTexts.BattlePassLocked);
                return;
            }

            if (!Backgrounds.TryBuyAndEquip(id, out string failMessage))
            {
                GameAudio.Play(GameAudio.Sfx.BuyFail);
                ShowShopMessage(string.IsNullOrEmpty(failMessage) ? GameTexts.NotEnoughGold : failMessage);
                return;
            }

            GameAudio.Play(GameAudio.Sfx.BuyOk);
        }

        private void ShowShopMessage(string message)
        {
            Text target = currentTab == Tab.Background && backgroundMessage != null
                ? backgroundMessage
                : currentTab == Tab.Gold && goldMessage != null
                    ? goldMessage
                    : shopMessage;
            if (target != null)
            {
                target.text = message;
                target.gameObject.SetActive(true);
                if (shopMessageRoutine != null) StopCoroutine(shopMessageRoutine);
                if (isActiveAndEnabled) shopMessageRoutine = StartCoroutine(HideShopMessageRoutine());
            }

            UIManager ui = FindObjectOfType<UIManager>();
            if (ui != null) ui.ShowMessage(message);
        }

        private IEnumerator HideShopMessageRoutine()
        {
            yield return new WaitForSeconds(1.6f);
            shopMessageRoutine = null;
            HideMessage(shopMessage);
            HideMessage(backgroundMessage);
            HideMessage(goldMessage);
        }

        private static void HideMessage(Text text)
        {
            if (text == null) return;
            text.text = string.Empty;
            text.gameObject.SetActive(false);
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
                rt.offsetMin = new Vector2(12f, 8f);
                rt.offsetMax = new Vector2(-12f, -8f);
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
            text.resizeTextMinSize = Mathf.Max(16, fontSize - 16);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            Transform direct = root.Find(name);
            if (direct != null) return direct;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }

            return null;
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
