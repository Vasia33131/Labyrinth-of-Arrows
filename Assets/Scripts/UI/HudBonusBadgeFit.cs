using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Count из сцены — доля визуальной иконки (якоря 0..1 по спрайту).
    /// Слот кнопки в ландшафте широкий, спрайт квадратный: без этого
    /// бейдж уезжает в пустой угол RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public class HudBonusBadgeFit : MonoBehaviour
    {
        public const string CountName = "Count";

        private RectTransform badge;
        private Vector2 authoredMin;
        private Vector2 authoredMax;
        private bool captured;
        private float lastWidth;
        private float lastHeight;

        public static void Ensure(Component button)
        {
            if (button == null) return;
            var fit = button.GetComponent<HudBonusBadgeFit>();
            if (fit == null) fit = button.gameObject.AddComponent<HudBonusBadgeFit>();
            fit.Apply(true);
        }

        private void OnEnable()
        {
            Apply(true);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Apply(false);
        }

        private void Apply(bool force)
        {
            var button = transform as RectTransform;
            if (button == null) return;

            float w = button.rect.width;
            float h = button.rect.height;
            if (!force && Mathf.Approximately(w, lastWidth) && Mathf.Approximately(h, lastHeight))
                return;
            if (w < 8f || h < 8f) return;
            if (!ResolveBadge(button)) return;

            if (!captured)
            {
                authoredMin = badge.anchorMin;
                authoredMax = badge.anchorMax;
                captured = true;
            }

            Rect visual = OverlayCardFit.VisualSpriteRect(button);
            if (visual.width < 8f || visual.height < 8f) return;

            float x0 = Mathf.Lerp(visual.xMin, visual.xMax, authoredMin.x);
            float y0 = Mathf.Lerp(visual.yMin, visual.yMax, authoredMin.y);
            float x1 = Mathf.Lerp(visual.xMin, visual.xMax, authoredMax.x);
            float y1 = Mathf.Lerp(visual.yMin, visual.yMax, authoredMax.y);
            if (x1 < x0)
            {
                float t = x0;
                x0 = x1;
                x1 = t;
            }

            if (y1 < y0)
            {
                float t = y0;
                y0 = y1;
                y1 = t;
            }

            badge.SetParent(button, false);
            badge.anchorMin = new Vector2(0.5f, 0.5f);
            badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(x1 - x0, y1 - y0);
            badge.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) - button.rect.center;
            badge.localScale = Vector3.one;
            StretchValue(badge);

            lastWidth = w;
            lastHeight = h;
        }

        private bool ResolveBadge(RectTransform button)
        {
            if (badge != null) return true;
            Transform found = button.Find(CountName);
            badge = found as RectTransform;
            return badge != null;
        }

        private static void StretchValue(RectTransform count)
        {
            Transform value = count.Find("Value");
            var valueRt = value as RectTransform;
            if (valueRt == null) return;

            valueRt.anchorMin = Vector2.zero;
            valueRt.anchorMax = Vector2.one;
            valueRt.offsetMin = Vector2.zero;
            valueRt.offsetMax = Vector2.zero;
            valueRt.pivot = new Vector2(0.5f, 0.5f);
            valueRt.anchoredPosition = Vector2.zero;
            valueRt.localScale = Vector3.one;
        }
    }
}
