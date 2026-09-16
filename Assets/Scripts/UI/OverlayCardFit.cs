using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Одна коробка и один крестик для всех оверлеев.
    /// Высота ближе к квадратному спрайту подарка, крестик — у правого края банта,
    /// а не в прозрачном углу RectTransform.
    /// </summary>
    public static class OverlayCardFit
    {
        public static readonly Vector2 CardSize = new Vector2(1080f, 1180f);
        /// <summary>
        /// Школьный туториал: размер Card как в портрете 1080×1920
        /// (якоря 8.3–91.7% × 15.6–84.4%). Детей не переставляем — только
        /// равномерно масштабируем коробку, иначе в ландшафте Title/Scheme/Body наезжают.
        /// </summary>
        public static readonly Vector2 TutorialCardSize = new Vector2(900f, 1320f);
        public static readonly Vector2 CloseSize = new Vector2(130f, 130f);
        /// <summary>От визуального правого-верхнего края спрайта: вплотную к правому краю банта.</summary>
        public static readonly Vector2 CloseOffset = new Vector2(-172f, -158f);
        public const float Pad = 32f;
        public const string DimmerName = "Dimmer";
        public const string DimmerVisualName = "DimmerVisual";
        /// <summary>На ПК фиолетовый фон чуть шире подарка, не на весь экран.</summary>
        public const float WideDimmerScale = 1.12f;
        /// <summary>Высота плашки: уходит выше и ниже экрана, чтобы не было обрезанных краёв.</summary>
        public const float WideDimmerHeightScale = 1.35f;

        /// <summary>ПК: плашка вокруг карточки. Телефон: на весь экран без баннера.</summary>
        public static bool UseWideDimmer => YandexDevice.IsDesktop;

        public static bool Apply(RectTransform card, RectTransform closeX)
        {
            return ApplySized(card, CardSize, closeX, true);
        }

        /// <summary>
        /// Карточка туториала: фиксированный авторский размер + единый scale.
        /// Детей, якоря схемы и кнопку «Дальше» не трогает.
        /// </summary>
        public static bool ApplyTutorial(RectTransform card)
        {
            return ApplySized(card, TutorialCardSize, null, false);
        }

        private static bool ApplySized(RectTransform card, Vector2 designedSize, RectTransform closeX, bool placeClose)
        {
            if (card == null) return false;
            if (designedSize.x < 8f || designedSize.y < 8f) return false;

            var parent = card.parent as RectTransform;
            if (parent == null) return false;

            float maxW = parent.rect.width - Pad * 2f;
            float maxH = parent.rect.height - Pad * 2f;
            if (maxW < 8f || maxH < 8f) return false;

            float scale = Mathf.Min(1f, maxW / designedSize.x, maxH / designedSize.y);
            if (scale < 0.05f) return false;

            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = designedSize;
            card.localScale = new Vector3(scale, scale, 1f);

            var face = card.GetComponent<Image>();
            if (face != null)
            {
                face.type = Image.Type.Simple;
                face.preserveAspect = true;
            }

            if (placeClose) PlaceCloseX(closeX, card);
            FitDimmer(parent, card);
            return true;
        }

        /// <summary>
        /// Затемнение остаётся на весь оверлей (клик снаружи закрывает).
        /// ПК: фиолетовая плашка чуть шире карточки.
        /// Телефон: фиолетовая зона на весь BannerContent (экран без баннера).
        /// </summary>
        public static void FitDimmer(RectTransform overlay, RectTransform card)
        {
            if (overlay == null) return;

            var dimmer = overlay.Find(DimmerName) as RectTransform;
            if (dimmer == null) return;

            var dimmerFace = dimmer.GetComponent<Image>();
            if (!UseWideDimmer)
            {
                FitPhoneFullDimmer(dimmer, dimmerFace);
                return;
            }

            RectTransform visual = EnsureDimmerVisual(dimmer, dimmerFace);
            if (visual == null) return;

            if (dimmerFace != null)
            {
                dimmerFace.color = new Color(1f, 1f, 1f, 0f);
                dimmerFace.raycastTarget = true;
            }

            Rect gift = VisualSpriteRect(card);
            float sx = card != null ? Mathf.Abs(card.localScale.x) : 1f;
            float sy = card != null ? Mathf.Abs(card.localScale.y) : 1f;
            if (sx < 0.0001f) sx = 1f;
            if (sy < 0.0001f) sy = 1f;

            float cardW = gift.width * sx;
            float cardH = gift.height * sy;
            if (cardW < 8f) cardW = overlay.rect.width * 0.5f;
            if (cardH < 8f) cardH = overlay.rect.height * 0.7f;

            float width = cardW * WideDimmerScale;
            float maxW = Mathf.Max(8f, overlay.rect.width - Pad * 2f);
            if (width > maxW) width = maxW;
            if (width < cardW + 8f && maxW > cardW)
                width = Mathf.Min(cardW + 24f, maxW);

            float height = Mathf.Max(cardH * WideDimmerHeightScale, overlay.rect.height * 1.08f);

            visual.anchorMin = new Vector2(0.5f, 0.5f);
            visual.anchorMax = new Vector2(0.5f, 0.5f);
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(width, height);
            visual.anchoredPosition = card != null
                ? card.anchoredPosition + new Vector2(gift.center.x * sx, gift.center.y * sy)
                : Vector2.zero;
            visual.localScale = Vector3.one;
            visual.gameObject.SetActive(true);
        }

        private static RectTransform EnsureDimmerVisual(RectTransform dimmer, Image dimmerFace)
        {
            Transform existing = dimmer.Find(DimmerVisualName);
            var visual = existing as RectTransform;
            if (visual == null)
            {
                var go = new GameObject(DimmerVisualName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(dimmer, false);
                visual = go.GetComponent<RectTransform>();
            }

            var image = visual.GetComponent<Image>();
            if (image == null) image = visual.gameObject.AddComponent<Image>();
            Sprite sprite = dimmerFace != null ? dimmerFace.sprite : null;
            if (sprite == null && image.sprite == null)
            {
                ArtLibrary art = ArtLibrary.Current;
                if (art != null) sprite = art.bgDimmer;
            }

            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            visual.SetAsFirstSibling();
            return visual;
        }

        /// <summary>
        /// Телефон: растянуть bg_dimmer на весь Dimmer (он уже без полосы баннера).
        /// </summary>
        private static void FitPhoneFullDimmer(RectTransform dimmer, Image dimmerFace)
        {
            Transform visual = dimmer.Find(DimmerVisualName);
            var visualImage = visual != null ? visual.GetComponent<Image>() : null;
            if (visual != null) visual.gameObject.SetActive(false);

            if (dimmerFace == null) return;
            if (dimmerFace.sprite == null && visualImage != null && visualImage.sprite != null)
                dimmerFace.sprite = visualImage.sprite;
            if (dimmerFace.sprite == null)
            {
                ArtLibrary art = ArtLibrary.Current;
                if (art != null) dimmerFace.sprite = art.bgDimmer;
            }

            dimmerFace.color = Color.white;
            dimmerFace.type = Image.Type.Simple;
            dimmerFace.preserveAspect = false;
            dimmerFace.raycastTarget = true;

            dimmer.anchorMin = Vector2.zero;
            dimmer.anchorMax = Vector2.one;
            dimmer.offsetMin = Vector2.zero;
            dimmer.offsetMax = Vector2.zero;
            dimmer.pivot = new Vector2(0.5f, 0.5f);
            dimmer.localScale = Vector3.one;
        }

        public static void PlaceCloseX(RectTransform closeX, RectTransform card)
        {
            if (closeX == null || card == null) return;

            if (closeX.parent != card) closeX.SetParent(card, false);
            closeX.SetAsLastSibling();
            closeX.anchorMin = new Vector2(1f, 1f);
            closeX.anchorMax = new Vector2(1f, 1f);
            closeX.pivot = new Vector2(1f, 1f);
            closeX.sizeDelta = CloseSize;
            closeX.localScale = Vector3.one;

            Rect visual = VisualSpriteRect(card);
            Vector2 cardTopRight = new Vector2(card.rect.xMax, card.rect.yMax);
            closeX.anchoredPosition = CloseOffset + new Vector2(visual.xMax, visual.yMax) - cardTopRight;
        }

        /// <summary>
        /// «?» слева от банта, зеркало крестика: тот же Y и тот же отступ от края.
        /// </summary>
        public static void PlaceSchoolHelp(RectTransform help, RectTransform card)
        {
            if (help == null || card == null) return;

            if (help.parent != card) help.SetParent(card, false);
            help.anchorMin = new Vector2(0f, 1f);
            help.anchorMax = new Vector2(0f, 1f);
            help.pivot = new Vector2(0f, 1f);
            help.sizeDelta = CloseSize;
            help.localScale = Vector3.one;

            Rect visual = VisualSpriteRect(card);
            Vector2 cardTopLeft = new Vector2(card.rect.xMin, card.rect.yMax);
            Vector2 helpOffset = new Vector2(-CloseOffset.x, CloseOffset.y);
            help.anchoredPosition = helpOffset + new Vector2(visual.xMin, visual.yMax) - cardTopLeft;
        }

        /// <summary>
        /// Реальный прямоугольник подарка при preserveAspect — не пустые поля RectTransform.
        /// </summary>
        public static Rect VisualSpriteRect(RectTransform card)
        {
            if (card == null) return new Rect(0f, 0f, 0f, 0f);

            Rect r = card.rect;
            var face = card.GetComponent<Image>();
            if (face == null || !face.preserveAspect || face.sprite == null)
                return r;

            float spriteW = Mathf.Max(1f, face.sprite.rect.width);
            float spriteH = Mathf.Max(1f, face.sprite.rect.height);
            float spriteAspect = spriteW / spriteH;
            float cardAspect = r.width / Mathf.Max(1f, r.height);

            if (spriteAspect > cardAspect)
            {
                float h = r.width / spriteAspect;
                return new Rect(r.xMin, r.center.y - h * 0.5f, r.width, h);
            }

            float w = r.height * spriteAspect;
            return new Rect(r.center.x - w * 0.5f, r.yMin, w, r.height);
        }

        public static RectTransform FindCard(Transform overlay)
        {
            if (overlay == null) return null;
            Transform card = overlay.Find("Card");
            return card as RectTransform;
        }

        public static RectTransform FindCloseX(Transform overlay, RectTransform card)
        {
            Transform found = card != null ? card.Find("CloseXButton") : null;
            if (found == null && overlay != null) found = overlay.Find("CloseXButton");
            return found as RectTransform;
        }
    }
}
