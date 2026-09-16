using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Резервирует полосу под sticky: телефон снизу, ПК справа.
    /// Safe area учитывается вместе с баннером. Полоса без игровых кнопок.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class BannerSafeArea : MonoBehaviour
    {
        public const string ContentName = "BannerContent";
        public const string StripName = "BannerReserve";
        public const string FrameName = "UiFrame";
        private const string DimmerName = "Dimmer";

        public RectTransform ContentRoot { get; private set; }
        public bool ContentAlreadyInset { get; private set; }
        public RectTransform FrameRoot => frame;

        private Canvas hostCanvas;
        private RectTransform frame;
        private RectTransform strip;
        private Image stripImage;
        private int lastWidth;
        private int lastHeight;
        private Rect lastSafe;
        private bool lastDesktop;
        private float lastFrameWidth;
        private float lastContentWidth;
        private int lastFrameChildCount;

        public static BannerSafeArea For(Transform t)
        {
            if (t == null) return null;
            var area = t.GetComponent<BannerSafeArea>();
            if (area != null) return area;
            return t.GetComponentInParent<BannerSafeArea>();
        }

        public static Transform ResolveContentRoot(Transform canvasOrAny)
        {
            BannerSafeArea area = For(canvasOrAny);
            if (area != null && area.frame != null) return area.frame;
            if (area != null && area.ContentRoot != null) return area.ContentRoot;
            return canvasOrAny;
        }

        public static BannerSafeArea Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            var area = canvas.GetComponent<BannerSafeArea>();
            if (area == null)
                area = canvas.gameObject.AddComponent<BannerSafeArea>();

            area.Apply();
            return area;
        }

        public static BannerSafeArea BindExisting(Canvas canvas)
        {
            if (canvas == null) return null;
            var area = canvas.GetComponent<BannerSafeArea>();
            if (area == null) return null;
            area.BindPlay();
            return area;
        }

        public static bool IsScreenPointInReserve(Vector2 screenPoint)
        {
            BannerSafeArea[] areas = FindObjectsOfType<BannerSafeArea>();
            for (int i = 0; i < areas.Length; i++)
            {
                BannerSafeArea area = areas[i];
                if (area == null || area.strip == null || !area.strip.gameObject.activeInHierarchy)
                    continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(area.strip, screenPoint, null))
                    return true;
            }

            return false;
        }

        private void Awake()
        {
            hostCanvas = GetComponent<Canvas>();
            Apply();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData -= HandleSdkReady;
            YG2.onGetSDKData += HandleSdkReady;
            Apply();
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkReady;
        }

        private void Start()
        {
            Apply();
            RefitBoard();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                // Панели создаются в рантайме уже после раскладки, их затемнение нужно догнать.
                if (frame != null && frame.childCount != lastFrameChildCount)
                    LayoutDimmers();

                if (Screen.width == lastWidth
                    && Screen.height == lastHeight
                    && Screen.safeArea == lastSafe
                    && YandexDevice.IsDesktop == lastDesktop)
                    return;
                BindPlay();
                RefitBoard();
                return;
            }

            AdoptStrayChildren();
            if (Screen.width == lastWidth
                && Screen.height == lastHeight
                && Screen.safeArea == lastSafe
                && YandexDevice.IsDesktop == lastDesktop)
                return;

            Apply();
            RefitBoard();
        }

        private void HandleSdkReady()
        {
            Apply();
            RefitBoard();
        }

        public void BindPlay()
        {
            if (hostCanvas == null) hostCanvas = GetComponent<Canvas>();
            Transform existing = transform.Find(ContentName);
            ContentRoot = existing as RectTransform;
            Transform stripTf = transform.Find(StripName);
            strip = stripTf as RectTransform;
            if (strip != null) stripImage = strip.GetComponent<Image>();
            if (ContentRoot == null) return;
            frame = ContentRoot.Find(FrameName) as RectTransform;

            ApplyInsets();
        }

        public void Apply()
        {
            if (hostCanvas == null) hostCanvas = GetComponent<Canvas>();
            if (!Application.isPlaying) Canvas.ForceUpdateCanvases();
            EnsureHierarchy();
            AdoptStrayChildren();
            ApplyInsets();
        }

        private void ApplyInsets()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafe = Screen.safeArea;
            lastDesktop = YandexDevice.IsDesktop;
            if (ContentRoot == null) return;

            float scale = Mathf.Max(0.0001f, hostCanvas != null ? hostCanvas.scaleFactor : 1f);
            GetScreenInsets(out float left, out float bottom, out float right, out float top);
            ContentRoot.anchorMin = Vector2.zero;
            ContentRoot.anchorMax = Vector2.one;
            ContentRoot.pivot = new Vector2(0.5f, 0.5f);
            ContentRoot.offsetMin = new Vector2(left / scale, bottom / scale);
            ContentRoot.offsetMax = new Vector2(-right / scale, -top / scale);
            ContentAlreadyInset = true;
            LayoutFrame(scale, left, bottom, right, top);
            LayoutStrip(scale, bottom, right);
        }

        /// <summary>
        /// Портретная рамка под интерактив. Вёрстка задана нормализованными якорями,
        /// поэтому на широком экране кнопки растягиваются по горизонтали.
        /// </summary>
        private void LayoutFrame(float scale, float left, float bottom, float right, float top)
        {
            if (frame == null) return;

            float contentWidth = Mathf.Max(1f, (Screen.width - left - right) / scale);
            float contentHeight = Mathf.Max(1f, (Screen.height - bottom - top) / scale);
            float frameWidth = Mathf.Min(contentWidth, contentHeight * GameConstants.UiFrameAspect);

            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;

            if (frameWidth >= contentWidth - 0.5f)
            {
                // Узкий экран: рамка занимает всю область, вёрстка работает как раньше.
                frame.sizeDelta = new Vector2(contentWidth, contentHeight);
                frame.localScale = Vector3.one;
            }
            else
            {
                // Широкий экран: логический размер остаётся портретным, ужимаем масштабом.
                // Сузить сам rect нельзя: кегли шрифтов и карточки магазина заданы
                // в абсолютных единицах и перестанут помещаться.
                float fit = contentHeight / GameConstants.UiReferenceHeight;
                frame.sizeDelta = new Vector2(GameConstants.UiReferenceWidth, GameConstants.UiReferenceHeight);
                frame.localScale = new Vector3(fit, fit, 1f);
            }

            lastFrameWidth = frameWidth;
            lastContentWidth = contentWidth;
            LayoutDimmers();
        }

        /// <summary>
        /// Затемнение лежит внутри рамки и на широком экране не достаёт до краёв окна.
        /// Выпускаем его за границы рамки ровно на ширину пустых полей.
        /// </summary>
        private void LayoutDimmers()
        {
            if (frame == null || lastFrameWidth < 1f) return;

            lastFrameChildCount = frame.childCount;
            float overshoot = Mathf.Max(0f, (lastContentWidth - lastFrameWidth) * 0.5f) / lastFrameWidth;
            RectTransform[] all = frame.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                RectTransform rt = all[i];
                if (rt == null || rt.name != DimmerName) continue;
                rt.anchorMin = new Vector2(-overshoot, 0f);
                rt.anchorMax = new Vector2(1f + overshoot, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private void EnsureHierarchy()
        {
            if (ContentRoot == null)
            {
                Transform existing = transform.Find(ContentName);
                ContentRoot = existing as RectTransform;
                if (ContentRoot == null)
                {
                    var go = new GameObject(ContentName, typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    ContentRoot = go.GetComponent<RectTransform>();
                }
            }

            ContentRoot.SetAsFirstSibling();

            if (frame == null)
            {
                Transform existingFrame = ContentRoot.Find(FrameName);
                frame = existingFrame as RectTransform;
                if (frame == null)
                {
                    var go = new GameObject(FrameName, typeof(RectTransform));
                    go.transform.SetParent(ContentRoot, false);
                    frame = go.GetComponent<RectTransform>();
                }
            }

            if (strip == null)
            {
                Transform existing = transform.Find(StripName);
                strip = existing as RectTransform;
                if (strip == null)
                {
                    var go = new GameObject(StripName, typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    strip = go.GetComponent<RectTransform>();
                    stripImage = go.AddComponent<Image>();
                }
            }

            if (stripImage == null) stripImage = strip.GetComponent<Image>();
            if (stripImage == null) stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.raycastTarget = true;
            stripImage.color = Application.isEditor
                ? new Color(0.05f, 0.06f, 0.08f, 0.55f)
                : new Color(0f, 0f, 0f, 0f);
            strip.SetAsLastSibling();
        }

        private void AdoptStrayChildren()
        {
            if (ContentRoot == null) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child == ContentRoot || child == strip) continue;
                if (child.name == ContentName || child.name == StripName) continue;
                child.SetParent(ContentRoot, false);
            }

            AdoptIntoFrame();
        }

        /// <summary>
        /// В рамку уходит весь интерактив. Фон остаётся на всю площадь Canvas,
        /// иначе по бокам от рамки появятся пустые поля.
        /// </summary>
        private void AdoptIntoFrame()
        {
            if (frame == null || ContentRoot == null) return;

            bool adopted = false;
            for (int i = ContentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = ContentRoot.GetChild(i);
                if (child == null || child == frame) continue;
                if (IsFullBleed(child.name)) continue;
                child.SetParent(frame, false);
                adopted = true;
            }

            frame.SetAsLastSibling();
            if (adopted)
                RestoreMenuDrawOrder(frame);
        }

        private static bool IsFullBleed(string name)
        {
            return name == "Background" || name == "BoardBackground";
        }

        /// <summary>
        /// SetParent при adopt кладёт объект в конец BannerContent.
        /// Фон сцены обычно первый ребёнок Canvas, поэтому оказывается последним sibling
        /// и рисуется поверх «Играть» и настроек.
        /// </summary>
        public static void RestoreMenuDrawOrder(Transform contentRoot)
        {
            if (contentRoot == null) return;
            if (contentRoot.GetComponentInParent<MainMenuController>() == null) return;

            Transform nested = contentRoot.Find(ContentName);
            if (nested != null) contentRoot = nested;

            int childCount = contentRoot.childCount;
            if (childCount == 0) return;

            var background = new List<Transform>(1);
            var middle = new List<Transform>(childCount);
            var buttons = new List<Transform>(3);
            var overlays = new List<Transform>(4);

            for (int i = 0; i < childCount; i++)
            {
                Transform child = contentRoot.GetChild(i);
                string name = child.name;
                if (name == "Background")
                    background.Add(child);
                else if (name == "PlayButton" || name == "SettingsButton" || name == "ShopButton")
                    buttons.Add(child);
                else if (IsMenuOverlay(name))
                    overlays.Add(child);
                else
                    middle.Add(child);
            }

            SortNamed(buttons, "PlayButton", "SettingsButton", "ShopButton");
            SortNamed(overlays,
                "SettingsOverlay",
                ShopPanel.OverlayName,
                PlayModePanel.OverlayName,
                SchoolTutorialPanel.OverlayName,
                DailyHintsPanel.OverlayName);

            int index = 0;
            if (background.Count > 0)
            {
                background[0].SetAsFirstSibling();
                index = 1;
            }

            index = ApplyDrawLayer(middle, index);
            index = ApplyDrawLayer(buttons, index);
            ApplyDrawLayer(overlays, index);
        }

        private static bool IsMenuOverlay(string name)
        {
            return name == "SettingsOverlay"
                || name == ShopPanel.OverlayName
                || name == PlayModePanel.OverlayName
                || name == SchoolTutorialPanel.OverlayName
                || name == DailyHintsPanel.OverlayName;
        }

        private static void SortNamed(List<Transform> items, params string[] order)
        {
            items.Sort((a, b) => NameOrder(order, a.name).CompareTo(NameOrder(order, b.name)));
        }

        private static int NameOrder(string[] order, string name)
        {
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] == name) return i;
            }

            return order.Length;
        }

        private static int ApplyDrawLayer(List<Transform> layer, int index)
        {
            for (int i = 0; i < layer.Count; i++)
            {
                layer[i].SetSiblingIndex(index);
                index++;
            }

            return index;
        }

        private static void GetScreenInsets(out float left, out float bottom, out float right, out float top)
        {
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            Rect safe = Screen.safeArea;
            if (safe.width < 2f || safe.height < 2f)
                safe = new Rect(0f, 0f, w, h);

            left = Mathf.Max(0f, safe.xMin);
            bottom = Mathf.Max(0f, safe.yMin);
            right = Mathf.Max(0f, w - safe.xMax);
            top = Mathf.Max(0f, h - safe.yMax);

            if (YandexDevice.IsDesktop)
                right += Reserve(w, GameConstants.DesktopBannerWidth, GameConstants.DesktopBannerWidthFraction);
            else
                bottom += Reserve(h, GameConstants.MobileBannerHeight, GameConstants.MobileBannerHeightFraction);
        }

        /// <summary>Доля экрана переживает любой devicePixelRatio, пиксели — только нижняя граница.</summary>
        private static float Reserve(float screenSize, float minPixels, float fraction)
        {
            float max = screenSize * GameConstants.BannerReserveMaxFraction;
            return Mathf.Clamp(screenSize * fraction, Mathf.Min(minPixels, max), max);
        }

        private void LayoutStrip(float scale, float bottom, float right)
        {
            if (strip == null) return;

            if (YandexDevice.IsDesktop)
            {
                strip.anchorMin = new Vector2(1f, 0f);
                strip.anchorMax = new Vector2(1f, 1f);
                strip.pivot = new Vector2(1f, 0.5f);
                strip.offsetMin = new Vector2(-right / scale, 0f);
                strip.offsetMax = Vector2.zero;
            }
            else
            {
                strip.anchorMin = new Vector2(0f, 0f);
                strip.anchorMax = new Vector2(1f, 0f);
                strip.pivot = new Vector2(0.5f, 0f);
                strip.offsetMin = Vector2.zero;
                strip.offsetMax = new Vector2(0f, bottom / scale);
            }

#if UNITY_EDITOR
            EnsureEditorLabel();
#endif
        }

#if UNITY_EDITOR
        private void EnsureEditorLabel()
        {
            if (strip == null) return;
            Transform existing = strip.Find("Label");
            Text label = existing != null ? existing.GetComponent<Text>() : null;
            if (label == null)
            {
                if (Application.isPlaying) return;
                var go = new GameObject("Label", typeof(RectTransform));
                go.transform.SetParent(strip, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                label = go.AddComponent<Text>();
                label.font = GoldCounterView.ResolveFont();
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.color = new Color(1f, 1f, 1f, 0.55f);
            }

            label.fontSize = YandexDevice.IsDesktop ? 22 : 28;
            label.text = YandexDevice.IsDesktop ? "Баннер" : "Баннер";
        }
#endif

        private static void RefitBoard()
        {
            if (!Application.isPlaying) return;
            if (BoardCameraFitter.Instance != null)
                BoardCameraFitter.Instance.Fit();
        }
    }
}
