using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Overlay-canvas → мир ортокамеры (плоскость z = 0).
    /// UI-рамки только якоря; геймплей живёт в мире.
    /// </summary>
    public static class UiWorldUtility
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        public static Camera ResolveCamera(Camera cam)
        {
            if (cam != null) return cam;
            return Camera.main;
        }

        public static float PlaneDepth(Camera cam)
        {
            if (cam == null) return 10f;
            return Mathf.Abs(cam.transform.position.z);
        }

        public static Vector3 ScreenToWorld(Camera cam, Vector2 screen)
        {
            cam = ResolveCamera(cam);
            if (cam == null) return new Vector3(screen.x, screen.y, 0f);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, PlaneDepth(cam)));
            world.z = 0f;
            return world;
        }

        public static Vector3 RectToWorld(RectTransform rt, Camera cam)
        {
            if (rt == null) return Vector3.zero;
            cam = ResolveCamera(cam);
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCam, rt.position);
            return ScreenToWorld(cam, screen);
        }

        public static bool TryGetScreenRect(RectTransform rt, out Rect screenRect)
        {
            screenRect = default;
            if (rt == null) return false;

            rt.GetWorldCorners(Corners);
            float minX = Corners[0].x;
            float minY = Corners[0].y;
            float maxX = Corners[2].x;
            float maxY = Corners[2].y;
            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return screenRect.width > 0.5f && screenRect.height > 0.5f;
        }

        public static bool TryGetViewportRect(RectTransform rt, Camera cam, out Rect viewport)
        {
            viewport = new Rect(0f, 0f, 1f, 1f);
            if (!TryGetScreenRect(rt, out Rect screen)) return false;

            cam = ResolveCamera(cam);
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Rect pixel = canvas != null ? canvas.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            if (pixel.width < 1f || pixel.height < 1f)
                pixel = new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

            float x0 = (screen.xMin - pixel.x) / pixel.width;
            float y0 = (screen.yMin - pixel.y) / pixel.height;
            float x1 = (screen.xMax - pixel.x) / pixel.width;
            float y1 = (screen.yMax - pixel.y) / pixel.height;
            viewport = Rect.MinMaxRect(x0, y0, x1, y1);
            return viewport.width > 0.02f && viewport.height > 0.02f;
        }

        public static bool TryGetWorldRect(RectTransform rt, Camera cam, out Rect worldRect)
        {
            worldRect = default;
            if (!TryGetScreenRect(rt, out Rect screen)) return false;
            cam = ResolveCamera(cam);
            if (cam == null) return false;

            Vector3 bl = ScreenToWorld(cam, new Vector2(screen.xMin, screen.yMin));
            Vector3 tr = ScreenToWorld(cam, new Vector2(screen.xMax, screen.yMax));
            worldRect = Rect.MinMaxRect(
                Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y),
                Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
            return worldRect.width > 0.001f && worldRect.height > 0.001f;
        }

        public static Rect GetSafeAreaNormalized()
        {
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            Rect safe = Screen.safeArea;
            if (safe.width < 2f || safe.height < 2f)
                safe = new Rect(0f, 0f, w, h);

            return Rect.MinMaxRect(safe.xMin / w, safe.yMin / h, safe.xMax / w, safe.yMax / h);
        }

        public static void ApplySafeArea(RectTransform root)
        {
            if (root == null) return;

            Rect safe = GetSafeAreaNormalized();
            root.anchorMin = new Vector2(safe.xMin, safe.yMin);
            root.anchorMax = new Vector2(safe.xMax, safe.yMax);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
