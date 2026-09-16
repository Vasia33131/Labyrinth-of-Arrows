using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Пишет текущий экранный прямоугольник якорями (offset = 0).
    /// Детей LayoutGroup не трогает — сетка школы остаётся клетками.
    /// </summary>
    public static class RectAnchorCapture
    {
        public static int ConvertTree(Transform root)
        {
            if (root == null) return 0;

            var rects = new List<RectTransform>();
            root.GetComponentsInChildren(true, rects);
            rects.Sort((a, b) => Depth(b).CompareTo(Depth(a)));

            int converted = 0;
            for (int i = 0; i < rects.Count; i++)
            {
                if (ConvertToAnchors(rects[i])) converted++;
            }

            return converted;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t != null)
            {
                d++;
                t = t.parent;
            }

            return d;
        }

        private static bool ConvertToAnchors(RectTransform rect)
        {
            if (rect == null) return false;
            if (ShouldSkip(rect)) return false;
            var parent = rect.parent as RectTransform;
            if (parent == null) return false;
            if (parent.GetComponent<LayoutGroup>() != null) return false;

            Vector2 parentSize = parent.rect.size;
            if (parentSize.x < 1f || parentSize.y < 1f) return false;

            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;
            Vector2 offMin = rect.offsetMin;
            Vector2 offMax = rect.offsetMax;

            if (Mathf.Abs(offMin.x) < 0.05f && Mathf.Abs(offMin.y) < 0.05f
                && Mathf.Abs(offMax.x) < 0.05f && Mathf.Abs(offMax.y) < 0.05f)
                return false;

            rect.anchorMin = new Vector2(
                min.x + offMin.x / parentSize.x,
                min.y + offMin.y / parentSize.y);
            rect.anchorMax = new Vector2(
                max.x + offMax.x / parentSize.x,
                max.y + offMax.y / parentSize.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rect);
            return true;
        }

        /// <summary>
        /// BannerContent хранит пиксельные inset баннера; BannerReserve рисует полосу.
        /// Их якоря выставляет BannerSafeArea, не этот захват.
        /// </summary>
        private static bool ShouldSkip(RectTransform rect)
        {
            Transform t = rect;
            while (t != null)
            {
                if (t.name == BannerSafeArea.StripName) return true;
                t = t.parent;
            }

            return rect.name == BannerSafeArea.ContentName;
        }
    }
}
