using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вёрстка только из сцены. Скрипт не двигает RectTransform, не меняет иерархию,
    /// не добавляет компоненты. Только биндинг, клики, свайп и состояние наград.
    /// </summary>
    public class BattlePassView : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public const string ObjectName = "BattlePassStrip";
        public const string InfoName = "InfoBlock";
        public const string TrackName = "TrackHost";
        public const string ViewportName = "Viewport";
        public const string ContentName = "Content";

        public Text titleText;
        public Text levelText;
        public RectTransform trackHost;
        public ScrollRect trackScroll;
        public RectTransform trackContent;
        public Button purchaseButton;
        public Text purchaseLabel;
        public GameObject premiumBadge;
        public GameObject premiumLock;

        private bool purchaseWired;

        private void Awake()
        {
            BattlePass.RevokePremium();
            BindExisting();
            WirePurchase();
            Refresh();
        }

        private void OnEnable()
        {
            GameTexts.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            BattlePass.SyncFromSchoolProgress();
            ApplyTexts();

            if (levelText != null)
                levelText.text = BattlePass.CurrentLevel + "/" + BattlePass.LevelCount;

            bool premium = BattlePass.PremiumUnlocked;
            if (purchaseButton != null)
                purchaseButton.gameObject.SetActive(!premium);
            if (purchaseLabel != null && !premium)
                purchaseLabel.text = BattlePass.PremiumPriceGold.ToString();
            if (premiumBadge != null)
                premiumBadge.SetActive(premium);
            if (premiumLock != null)
                premiumLock.SetActive(!premium);

            if (trackContent == null) return;
            BattlePassSlotView[] slots = trackContent.GetComponentsInChildren<BattlePassSlotView>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].Refresh();
            }
        }

        /// <summary>Заголовок полосы и бейдж «Премиум» стоят в сцене — переписываем на языке YG2.</summary>
        private void ApplyTexts()
        {
            UiLabel.Set(titleText, GameTexts.BattlePassTitle);

            Transform info = transform.Find(InfoName);
            if (info != null)
            {
                UiLabel.SetNamed(info, "Title", GameTexts.BattlePassTitle);
                if (premiumBadge != null)
                    UiLabel.SetNamed(premiumBadge.transform, "Text", GameTexts.Premium);
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (trackScroll != null) trackScroll.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (trackScroll != null) trackScroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (trackScroll != null) trackScroll.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (trackScroll != null) trackScroll.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (trackScroll != null) trackScroll.OnScroll(eventData);
        }

        private void BindExisting()
        {
            if (trackHost == null)
            {
                Transform named = transform.Find(TrackName);
                if (named == null && transform.parent != null)
                    named = transform.parent.Find(TrackName);
                trackHost = named as RectTransform;
            }

            if (trackScroll == null && trackHost != null)
                trackScroll = trackHost.GetComponent<ScrollRect>();

            if (trackContent == null && trackScroll != null)
                trackContent = trackScroll.content;

            if (purchaseButton == null)
            {
                Transform info = transform.Find(InfoName);
                Transform button = info != null ? info.Find("PurchaseButton") : null;
                if (button != null) purchaseButton = button.GetComponent<Button>();
            }

            if (purchaseLabel == null && purchaseButton != null)
            {
                Transform text = purchaseButton.transform.Find("Text");
                if (text == null) text = purchaseButton.transform.Find("Label");
                if (text != null) purchaseLabel = text.GetComponent<Text>();
                if (purchaseLabel == null)
                    purchaseLabel = purchaseButton.GetComponentInChildren<Text>(true);
            }

            if (premiumBadge == null)
            {
                Transform info = transform.Find(InfoName);
                Transform badge = info != null ? info.Find("PremiumBadge") : null;
                if (badge != null) premiumBadge = badge.gameObject;
            }
        }

        private void WirePurchase()
        {
            if (purchaseWired || purchaseButton == null) return;
            purchaseButton.onClick.AddListener(BuyPremium);
            purchaseWired = true;
        }

        private void BuyPremium()
        {
            if (BattlePass.PremiumUnlocked) return;
            if (Wallet.Gold < BattlePass.PremiumPriceGold)
            {
                GameAudio.Play(GameAudio.Sfx.BuyFail);
                return;
            }

            if (!BattlePass.TryBuyPremium()) return;
            GameAudio.Play(GameAudio.Sfx.BuyOk);
            Refresh();
        }
    }
}
