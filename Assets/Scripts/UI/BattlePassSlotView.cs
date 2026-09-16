using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Клик забирает награду. Свайп уходит в ScrollRect — даже если он не родитель клетки.
    /// Размер клетки скрипт не меняет.
    /// </summary>
    public class BattlePassSlotView : MonoBehaviour,
        IPointerDownHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public int level;
        public bool isPremium;
        public Text numberLabel;
        public Text caption;
        public Image square;
        public Image rewardIcon;

        private ScrollRect scrollRect;
        private bool dragged;

        public void Refresh()
        {
            bool unlocked = level >= 1 && level <= BattlePass.CurrentLevel;
            bool claimed = isPremium ? BattlePass.IsPremiumClaimed(level) : BattlePass.IsFreeClaimed(level);
            bool premiumReady = !isPremium || BattlePass.PremiumUnlocked;
            bool available = unlocked && premiumReady && !claimed;

            if (numberLabel != null)
                numberLabel.text = level.ToString();

            if (caption != null)
                caption.text = CaptionFor(claimed, available);

            ArtLibrary art = ArtLibrary.Current;
            Sprite face = SlotFace(art, unlocked, claimed);
            if (square != null)
            {
                square.sprite = face;
                square.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.42f);
                square.preserveAspect = true;
            }

            ApplyRewardIcon(art, claimed, available, unlocked);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragged = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragged) return;
            TryClaim();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            ResolveScrollRect();
            if (scrollRect != null) scrollRect.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            ResolveScrollRect();
            if (scrollRect != null) scrollRect.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (scrollRect != null) scrollRect.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (scrollRect != null) scrollRect.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            ResolveScrollRect();
            if (scrollRect != null) scrollRect.OnScroll(eventData);
        }

        private void TryClaim()
        {
            if (level < 1) return;

            bool claimed = isPremium
                ? BattlePass.ClaimPremium(level)
                : BattlePass.ClaimFree(level);
            if (!claimed) return;

            GameAudio.Play(GameAudio.Sfx.BuyOk);
            Refresh();
            BattlePassView view = GetComponentInParent<BattlePassView>();
            if (view != null) view.Refresh();
        }

        private void ResolveScrollRect()
        {
            if (scrollRect != null) return;

            scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect != null) return;

            BattlePassView view = GetComponentInParent<BattlePassView>();
            if (view != null) scrollRect = view.trackScroll;
        }

        private string CaptionFor(bool claimed, bool available)
        {
            if (claimed) return string.Empty;
            if (BattlePass.IsStubFinal(level))
                return isPremium ? GameTexts.RewardSkin : GameTexts.RewardBackground;
            if (!available && isPremium && !BattlePass.PremiumUnlocked)
                return BattlePass.GetPremiumGold(level).ToString();
            return isPremium
                ? BattlePass.GetPremiumGold(level).ToString()
                : BattlePass.GetFreeGold(level).ToString();
        }

        private Sprite SlotFace(ArtLibrary art, bool unlocked, bool claimed)
        {
            if (art == null) return null;
            if (!unlocked && art.bpSlotLocked != null) return art.bpSlotLocked;
            if (claimed)
                return isPremium ? art.bpSlotPremium : art.bpSlotFree;
            return isPremium ? art.bpSlotPremium : art.bpSlotFree;
        }

        private void ApplyRewardIcon(ArtLibrary art, bool claimed, bool available, bool unlocked)
        {
            if (rewardIcon == null) return;

            Sprite sprite = null;
            if (claimed && art != null)
                sprite = art.iconCheck;
            else if (BattlePass.IsStubFinal(level))
                sprite = ResolveFinalPreview(art);
            else if (art != null)
                sprite = isPremium ? art.iconCoinsStack : art.iconCoin;

            rewardIcon.sprite = sprite;
            rewardIcon.enabled = sprite != null;
            rewardIcon.preserveAspect = true;
            rewardIcon.raycastTarget = false;
            float alpha = !unlocked ? 0.4f : (claimed || available ? 1f : 0.7f);
            rewardIcon.color = new Color(1f, 1f, 1f, alpha);
        }

        private Sprite ResolveFinalPreview(ArtLibrary art)
        {
            if (isPremium)
            {
                CharacterSkinData skin = CharacterSkinCatalog.Current.Get(CharacterSkinCatalog.TeacherId);
                return skin != null ? skin.ResolveIdle(art) : null;
            }

            BackgroundData background = BackgroundCatalog.Current.Get(BackgroundCatalog.ClassroomId);
            if (background == null) return null;
            if (background.sprite != null) return background.sprite;
            return background.ResolveSprite(true, art);
        }
    }
}
