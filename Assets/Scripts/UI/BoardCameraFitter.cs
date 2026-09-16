using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вписывает мировую сетку СТРОГО в BoardPlayArea с равными полями.
    /// Рамку и аспект поля задаёт BannerSafeArea; здесь только камера в уже готовый rect.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BoardCameraFitter : MonoBehaviour
    {
        public static BoardCameraFitter Instance { get; private set; }

        public Camera gameCamera;
        public RectTransform boardPlayArea;
        public SpriteRenderer boardBackdrop;
        public ScreenLayoutConfig config;

        private void Awake()
        {
            Instance = this;
            if (gameCamera == null) gameCamera = GetComponent<Camera>();
            if (gameCamera == null) gameCamera = Camera.main;
            SyncBackdrop(gameCamera);
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        public void Fit(LevelGenerator generator = null, Camera cam = null)
        {
            if (cam == null) cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;

            if (generator == null && GameManager.Instance != null)
                generator = GameManager.Instance.levelGenerator;

            if (Application.isPlaying)
                Canvas.ForceUpdateCanvases();

            if (boardPlayArea == null && ScreenLayoutBuilder.Instance != null)
                boardPlayArea = ScreenLayoutBuilder.Instance.boardPlayArea;

            if (generator == null)
            {
                SyncBackdrop(cam);
                return;
            }

            if (boardPlayArea == null || !UiWorldUtility.TryGetViewportRect(boardPlayArea, cam, out Rect viewport))
            {
                generator.FitCameraFallback(cam);
                SyncBackdrop(cam);
                return;
            }

            generator.GetBoardWorldBounds(out float minX, out float maxX, out float minY, out float maxY);
            float gw = Mathf.Max(0.01f, maxX - minX);
            float gh = Mathf.Max(0.01f, maxY - minY);
            float gx = (minX + maxX) * 0.5f;
            float gy = (minY + maxY) * 0.5f;

            float inset = config != null ? config.boardFitInset : 0.04f;
            float vw = Mathf.Max(0.05f, viewport.width * (1f - inset * 2f));
            float vh = Mathf.Max(0.05f, viewport.height * (1f - inset * 2f));
            float vxc = viewport.center.x;
            float vyc = viewport.center.y;

            float aspect = Mathf.Max(0.05f, cam.aspect);
            float size = Mathf.Max(gh / (2f * vh), gw / (2f * vw * aspect));
            cam.orthographicSize = Mathf.Max(2f, size);

            float worldW = 2f * cam.orthographicSize * aspect;
            float worldH = 2f * cam.orthographicSize;
            cam.transform.position = new Vector3(
                gx - (vxc - 0.5f) * worldW,
                gy - (vyc - 0.5f) * worldH,
                cam.transform.position.z);

            SyncBackdrop(cam);
        }

        public void SyncBackdrop(Camera cam = null)
        {
            if (cam == null) cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam != null)
                cam.backgroundColor = GameConstants.BoardBackground;

            HideOverlayBoardBackground();
            CoverWorldBackdrop(cam);
        }

        private static void HideOverlayBoardBackground()
        {
            Image overlay = null;
            SceneCanvas scene = SceneCanvas.InScene;
            if (scene != null) overlay = scene.background;
            if (overlay == null)
            {
                GameObject found = GameObject.Find("BoardBackground");
                if (found != null) overlay = found.GetComponent<Image>();
            }

            if (overlay == null) return;

            // Overlay Image рисуется поверх мира и прячет стрелки; cover оставляем world backdrop.
            overlay.preserveAspect = false;
            overlay.type = Image.Type.Simple;
            overlay.raycastTarget = false;
            overlay.enabled = false;

            RectTransform rt = overlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void CoverWorldBackdrop(Camera cam)
        {
            if (boardBackdrop == null) return;
            if (cam == null)
            {
                boardBackdrop.enabled = false;
                return;
            }

            SceneCanvas canvas = SceneCanvas.InScene;
            BackgroundVisual.Resolve(Backgrounds.Selected, false, canvas, out Sprite sprite, out Color color);
            if (sprite == null)
            {
                boardBackdrop.enabled = false;
                if (cam != null) cam.backgroundColor = color.a > 0f ? color : GameConstants.BoardBackground;
                return;
            }

            boardBackdrop.enabled = true;
            boardBackdrop.sprite = sprite;
            boardBackdrop.color = color;
            boardBackdrop.drawMode = SpriteDrawMode.Simple;
            boardBackdrop.sortingOrder = -20;

            float worldH = 2f * cam.orthographicSize;
            float worldW = worldH * Mathf.Max(0.05f, cam.aspect);
            Vector2 size = sprite.bounds.size;
            if (size.x < 0.0001f || size.y < 0.0001f) return;

            float scale = Mathf.Max(worldW / size.x, worldH / size.y);
            Transform tf = boardBackdrop.transform;
            tf.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            tf.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
