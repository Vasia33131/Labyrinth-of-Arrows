using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вариант A «коридоры HUD»: 5 горизонтальных полос Safe Area.
    /// ScreenRoot = только Character + Board + Candy. TOP/BOTTOM HUD — дети Canvas.
    /// UI-рамки не клиппят ввод по полю.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ScreenLayoutBuilder : MonoBehaviour
    {
        public static ScreenLayoutBuilder Instance { get; private set; }

        public ScreenLayoutConfig config = new ScreenLayoutConfig();

        [Header("Авторский режим")]
        [Tooltip("Включи только чтобы пересчитать якоря из config. Иначе сцена не трогается.")]
        public bool applyRuntimeLayout;

        [Header("Корень")]
        public RectTransform screenRoot;
        public Canvas hostCanvas;

        [Header("Коридоры HUD")]
        public RectTransform topHud;
        public RectTransform bottomHud;
        public RectTransform menuButton;
        public RectTransform settingsButton;
        public RectTransform levelBadge;
        public RectTransform hintButton;
        public RectTransform undoButton;
        public RectTransform extraMoveButton;
        public RectTransform restartButton;

        [Header("Персонаж")]
        public RectTransform characterZone;
        public RectTransform characterSlot;
        public RectTransform progressRow;
        public Text characterLabel;

        [Header("Поле")]
        public RectTransform boardZone;
        public RectTransform boardFrame;
        public RectTransform boardPlayArea;
        public RectTransform orbitPath;
        public Text boardLabel;

        [Header("Конфеты")]
        public RectTransform candyZone;
        public RectTransform candyBox;
        public RectTransform candyLandPoint;
        public Text candyLabel;

        private int lastWidth;
        private int lastHeight;
        private Rect lastSafe;

        private void Awake()
        {
            Instance = this;
            if (Application.isPlaying) applyRuntimeLayout = false;
            if (hostCanvas == null) hostCanvas = GetComponentInParent<Canvas>();
            if (screenRoot == null) screenRoot = transform as RectTransform;
            Apply();
        }

        private void OnEnable()
        {
            Instance = this;
            Apply();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (!applyRuntimeLayout) return;
            if (Screen.width == lastWidth && Screen.height == lastHeight && Screen.safeArea == lastSafe)
                return;
            Apply();
        }

        /// <summary>
        /// Широкий ПК: HUD и коридоры поля на всю ширину рамки — без 6% полей,
        /// иначе после вычета sticky снова дыра слева и справа (п. 1.6.2.1).
        /// Телефон оставляет авторские 0.06.
        /// </summary>
        public void SyncPlayChromeHorizontal()
        {
            float pad = YandexDevice.IsDesktop ? 0f : config.horizontalPadding;
            SpanHorizontal(topHud, pad);
            SpanHorizontal(screenRoot, pad);
        }

        private static void SpanHorizontal(RectTransform rt, float pad)
        {
            if (rt == null) return;
            Vector2 min = rt.anchorMin;
            Vector2 max = rt.anchorMax;
            min.x = pad;
            max.x = 1f - pad;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        public void Apply()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafe = Screen.safeArea;

            if (Application.isPlaying)
            {
                SyncPlayChromeHorizontal();
                return;
            }

            if (!applyRuntimeLayout) return;

            if (config == null) config = ScreenLayoutConfig.CreateDefault();
            HideLegacyChrome();
            ClearBoardOverlays();
            HideCharacterAndCandyPlates();

            EnlargeHudButtons(config);
            config.NormalizeHeights();

            if (screenRoot == null) screenRoot = transform as RectTransform;
            if (hostCanvas == null) hostCanvas = GetComponentInParent<Canvas>();

            ResolveHudHierarchy();
            HideLegacyChrome();

            float top = config.topHudHeight;
            float bottom = config.bottomHudHeight;
            float mid = config.MiddleShare;

            PlaceInSafeArea(topHud, 0f, 1f - top, 1f, 1f);
            PlaceInSafeArea(screenRoot, 0f, bottom, 1f, 1f - top);
            PlaceInSafeArea(bottomHud, 0f, 0f, 1f, bottom);

            float candyRel = config.candyHeight / mid;
            float boardRel = config.boardHeight / mid;
            SetStretch(candyZone, 0f, 0f, 1f, candyRel);
            SetStretch(boardZone, 0f, candyRel, 1f, candyRel + boardRel);
            SetStretch(characterZone, 0f, candyRel + boardRel, 1f, 1f);

            LayoutCharacterSlot();
            LayoutBoardInner();
            LayoutCandyInner();
            LayoutOrbitPath();
            LayoutTopHud();
            LayoutBottomHud();
            LayoutHintText();

            Canvas.ForceUpdateCanvases();
            LayoutCharacterSlot();
            LayoutCandyInner();
            LayoutTopHud();
            LayoutBottomHud();
        }

        /// <summary>
        /// Overlay-заливка рамки поля рисуется поверх мира и мутит стрелки.
        /// На поле оставляем только якоря, без полупрозрачных Image.
        /// </summary>
        private void ClearBoardOverlays()
        {
            DisableOverlayImages(boardZone);
            DisableOverlayImages(boardFrame);
            DisableOverlayImages(boardPlayArea);
            DisableOverlayImages(orbitPath);
        }

        private void HideCharacterAndCandyPlates()
        {
            DisableOwnFrame(characterZone);
            DisableOwnFrame(characterSlot);
            DisableOwnFrame(candyZone);
            DisableFillOnly(candyBox);
        }

        private static void DisableOwnFrame(RectTransform rt)
        {
            if (rt == null) return;
            if (rt.GetComponent<CandyBoxView>() != null) return;
            if (rt.name == "Frame") return;

            Image image = rt.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }

            DisableFillOnly(rt);
        }

        private static void DisableFillOnly(RectTransform rt)
        {
            if (rt == null) return;
            Transform fill = rt.Find("Fill");
            if (fill == null) return;
            Image image = fill.GetComponent<Image>();
            if (image == null) return;
            image.enabled = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
        }

        private static void DisableOverlayImages(RectTransform root)
        {
            if (root == null) return;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null) continue;
                if (image.GetComponent<Text>() != null) continue;
                image.enabled = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }
        }

        private static void EnlargeHudButtons(ScreenLayoutConfig config)
        {
            if (config.hudControlHeight < 140f) config.hudControlHeight = 148f;
            if (config.hudSideButtonWidth < 140f) config.hudSideButtonWidth = 148f;
            if (config.hudBottomButtonSize < 140f) config.hudBottomButtonSize = 148f;
            if (config.hudBottomButtonGap > 24f) config.hudBottomButtonGap = 22f;
            if (config.topHudHeight < 0.13f) config.topHudHeight = 0.145f;
            if (config.bottomHudHeight < 0.095f) config.bottomHudHeight = 0.11f;
        }

        private void LayoutCharacterSlot()
        {
            if (characterSlot == null || characterZone == null) return;

            float zoneH = Mathf.Max(1f, characterZone.rect.height);
            float zoneW = Mathf.Max(1f, characterZone.rect.width);
            if (zoneH < 2f && screenRoot != null)
                zoneH = screenRoot.rect.height * (config.characterHeight / Mathf.Max(0.2f, config.MiddleShare));
            if (zoneW < 2f && screenRoot != null)
                zoneW = screenRoot.rect.width;

            float pad = zoneH * config.characterInnerPadding;
            float side = Mathf.Max(8f, zoneH - pad * 2f);
            float maxWide = Mathf.Max(8f, zoneW - pad * 2f);
            side = Mathf.Min(side, maxWide);
            PlaceCenteredSquare(characterSlot, side, Vector2.zero);
        }

        private static void PlaceCenteredSquare(RectTransform rt, float side, Vector2 pos)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(side, side);
        }

        private void LayoutBoardInner()
        {
            if (boardFrame != null)
                SetStretch(boardFrame, 0f, 0f, 1f, 1f);

            float pad = config.boardFramePadding;
            if (boardPlayArea != null)
                SetStretch(boardPlayArea, pad, pad, 1f - pad, 1f - pad);
        }

        private void LayoutCandyInner()
        {
            if (candyBox == null || candyZone == null) return;

            float zoneH = Mathf.Max(1f, candyZone.rect.height);
            if (zoneH < 2f && screenRoot != null)
                zoneH = screenRoot.rect.height * (config.candyHeight / Mathf.Max(0.2f, config.MiddleShare));

            float hPad = config.candyBoxHorizontalPad;
            float vPad = config.candyBoxVerticalPad;
            float gapNorm = config.candyToBottomHudGapPx / Mathf.Max(1f, zoneH);
            float bottom = Mathf.Max(vPad, gapNorm);
            SetStretch(candyBox, hPad, bottom, 1f - hPad, 1f - vPad);

            if (candyLandPoint == null) return;
            candyLandPoint.anchorMin = new Vector2(0.5f, 0.5f);
            candyLandPoint.anchorMax = new Vector2(0.5f, 0.5f);
            candyLandPoint.pivot = new Vector2(0.5f, 0.5f);
            candyLandPoint.anchoredPosition = Vector2.zero;
            candyLandPoint.sizeDelta = new Vector2(24f, 24f);
        }

        private void LayoutOrbitPath()
        {
            if (orbitPath == null || boardFrame == null) return;

            float grow = 0.035f;
            SetStretch(orbitPath, -grow, -grow, 1f + grow, 1f + grow);
        }

        private void LayoutTopHud()
        {
            if (topHud == null) return;

            float zoneH = Mathf.Max(1f, topHud.rect.height);
            float height = config.hudControlHeight;
            if (zoneH > 2f) height = Mathf.Min(height, Mathf.Max(48f, zoneH - 8f));
            float btn = Mathf.Min(config.hudSideButtonWidth, height);

            PlaceHudControl(menuButton, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(btn, height));
            PlaceHudControl(settingsButton, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(btn, height));

            // LevelBadge / прогресс-бар: размер только из сцены, layout его не трогает.
        }

        private void LayoutBottomHud()
        {
            // Hint / Undo / ExtraMove / Restart — квадраты из сцены, рантайм не двигает.
        }

        private void LayoutHintText()
        {
            Transform canvas = ResolveCanvasTransform();
            if (canvas == null) return;

            var hint = canvas.Find("HintText") as RectTransform;
            if (hint == null) return;

            hint.anchorMin = new Vector2(0.5f, 0.5f);
            hint.anchorMax = new Vector2(0.5f, 0.5f);
            hint.pivot = new Vector2(0.5f, 0.5f);
            hint.anchoredPosition = Vector2.zero;
            hint.sizeDelta = new Vector2(900f, 60f);
        }

        private static void PlaceHudControl(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;

            Transform icon = rt.Find("Icon");
            if (icon == null) icon = rt.Find("GearIcon");
            if (icon is RectTransform iconRt)
            {
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(size.x * 0.58f, size.y * 0.58f);
            }
        }

        private void PlaceInSafeArea(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            if (rt == null) return;

            BannerSafeArea banner = BannerSafeArea.For(rt);
            Rect safe = banner != null && banner.ContentAlreadyInset
                ? new Rect(0f, 0f, 1f, 1f)
                : UiWorldUtility.GetSafeAreaNormalized();
            float padX = config.horizontalPadding;
            float x0 = Mathf.Lerp(safe.xMin, safe.xMax, padX);
            float x1 = Mathf.Lerp(safe.xMin, safe.xMax, 1f - padX);
            float y0 = Mathf.Lerp(safe.yMin, safe.yMax, minY);
            float y1 = Mathf.Lerp(safe.yMin, safe.yMax, maxY);

            rt.anchorMin = new Vector2(Mathf.Lerp(x0, x1, minX), y0);
            rt.anchorMax = new Vector2(Mathf.Lerp(x0, x1, maxX), y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private void ResolveHudHierarchy()
        {
            Transform canvas = ResolveCanvasTransform();
            if (canvas == null) return;

            if (topHud == null) topHud = canvas.Find("TopHud") as RectTransform;
            if (bottomHud == null) bottomHud = canvas.Find("BottomHud") as RectTransform;
            if (topHud == null && AuthoredUi.CanBuild) topHud = CreateRuntimeStrip(canvas, "TopHud");
            if (bottomHud == null && AuthoredUi.CanBuild) bottomHud = CreateRuntimeStrip(canvas, "BottomHud");
            if (topHud == null) AuthoredUi.Missing("TopHud");
            if (bottomHud == null) AuthoredUi.Missing("BottomHud");

            menuButton = Adopt(topHud, menuButton, "MenuButton");
            settingsButton = Adopt(topHud, settingsButton, "SettingsButton");
            levelBadge = Adopt(topHud, levelBadge, "LevelBadge");
            if (hintButton == null && bottomHud != null)
                hintButton = bottomHud.Find("HintButton") as RectTransform;
            if (undoButton == null && bottomHud != null)
                undoButton = bottomHud.Find("UndoButton") as RectTransform;
            if (extraMoveButton == null && bottomHud != null)
                extraMoveButton = bottomHud.Find("ExtraMoveButton") as RectTransform;
            restartButton = Adopt(bottomHud, restartButton, "RestartButton");
        }

        private static RectTransform CreateRuntimeStrip(Transform canvas, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private RectTransform Adopt(RectTransform parent, RectTransform current, string name)
        {
            if (parent == null) return current;
            if (Application.isPlaying) return current != null ? current : parent.Find(name) as RectTransform;

            RectTransform rt = current;
            if (rt == null) rt = parent.Find(name) as RectTransform;
            if (rt == null) rt = FindHudControl(name);
            if (rt == null) return current;
            if (rt.parent != parent) rt.SetParent(parent, false);
            return rt;
        }

        private RectTransform FindHudControl(string name)
        {
            Transform canvas = ResolveCanvasTransform();
            if (canvas == null) return null;

            var direct = canvas.Find(name) as RectTransform;
            if (direct != null) return direct;

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (IsOverlayRoot(child.name)) continue;
                var found = child.Find(name) as RectTransform;
                if (found != null) return found;
            }

            return null;
        }

        private static bool IsOverlayRoot(string name)
        {
            return BannerSafeArea.IsGameOverlay(name);
        }

        private void HideLegacyChrome()
        {
            if (progressRow == null && characterZone != null)
                progressRow = characterZone.Find("ProgressRow") as RectTransform;
            if (progressRow != null) progressRow.gameObject.SetActive(false);

            HideLabel(characterLabel);
            HideLabel(boardLabel);
            HideLabel(candyLabel);
        }

        private static void HideLabel(Text label)
        {
            if (label == null) return;
            label.enabled = false;
            label.gameObject.SetActive(false);
        }

        private Transform ResolveCanvasTransform()
        {
            Transform canvas = hostCanvas != null ? hostCanvas.transform : null;
            if (canvas == null && screenRoot != null && screenRoot.parent != null)
                canvas = screenRoot.parent;
            if (canvas == null) canvas = transform.parent;
            return BannerSafeArea.ResolveContentRoot(canvas);
        }

        private static void SetStretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
