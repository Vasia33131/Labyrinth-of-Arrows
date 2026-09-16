using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Школа: если игрок долго стоит в Playing, показываем палец на легальный свайп портфеля
    /// или на безопасную стрелку. Не ход, указку не крутит, заряды подсказки не тратит.
    /// Не кампания: отдельной системы хинтов нет.
    /// </summary>
    [DisallowMultipleComponent]
    public class SchoolIdleHint : MonoBehaviour
    {
        public const string OverlayName = "SchoolIdleHint";

        private const float SwipeSeconds = 0.85f;
        private const float SwipePause = 0.28f;
        private const float TapSeconds = 0.55f;
        private const float FadeSeconds = 0.16f;
        private const float SwipeSpanCells = 0.55f;
        private const float MessageSeconds = 4f;

        private static Sprite cachedFinger;

        private RectTransform overlay;
        private RectTransform hand;
        private Image handImage;
        private CanvasGroup fade;
        private Camera gameCamera;

        private bool showing;
        private bool swipeMode;
        private float idleSeconds;
        private float waitSeconds;
        private float animSeconds;
        private float fadeSeconds;
        private Vector2 swipeFrom;
        private Vector2 swipeTo;
        private Vector2 tapRest;
        private int lastWidth;
        private int lastHeight;

        public static SchoolIdleHint Active { get; private set; }

        public static SchoolIdleHint Ensure()
        {
            SceneCanvas scene = SceneCanvas.InScene;
            Canvas canvas = scene != null ? scene.GetComponent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return null;

            var hint = canvas.GetComponent<SchoolIdleHint>();
            if (hint == null) hint = canvas.gameObject.AddComponent<SchoolIdleHint>();
            hint.BindCamera();
            return hint;
        }

        public static void NotifyInput()
        {
            if (Active != null) Active.Dismiss();
        }

        public static void HideActive()
        {
            if (Active != null) Active.Hide();
        }

        public void ResetIdle()
        {
            Hide();
            idleSeconds = 0f;
            waitSeconds = NextWait();
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }

        private void OnEnable()
        {
            Active = this;
            ResetIdle();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            Hide();
        }

        private void Update()
        {
            if (ReadAnyInput())
            {
                Dismiss();
                return;
            }

            if (!CanRun())
            {
                if (showing) Hide();
                idleSeconds = 0f;
                return;
            }

            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                if (showing) Hide();
                idleSeconds = 0f;
                return;
            }

            if (showing)
            {
                Animate();
                return;
            }

            idleSeconds += Time.unscaledDeltaTime;
            if (idleSeconds < waitSeconds) return;

            // Решение один раз, не каждый кадр: либо рука, либо снова ждём.
            idleSeconds = 0f;
            waitSeconds = NextWait();
            if (!TryShow())
                return;
        }

        private void Dismiss()
        {
            Hide();
            idleSeconds = 0f;
            waitSeconds = NextWait();
        }

        private void Hide()
        {
            showing = false;
            animSeconds = 0f;
            fadeSeconds = 0f;
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        private bool CanRun()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsSchoolMode) return false;
            if (game.State != GameState.Playing) return false;
            if (game.IsIntroPlaying || game.IsBlockedBumpPlaying) return false;
            if (game.inputHandler != null && !game.inputHandler.inputEnabled) return false;

            if (YG2.isPauseGame || !YG2.isFocusWindowGame) return false;
            if (OrientationGate.IsBlocking) return false;
            if (SchoolTutorialPanel.IsOpen) return false;
            if (SettingsPanel.IsOpen || ShopPanel.IsOpen) return false;
            if (DailyHintsPanel.IsOpen || PlayModePanel.IsOpen) return false;
            if (InterstitialAds.IsShowing || RewardedAds.IsBusy) return false;
            return true;
        }

        private static bool ReadAnyInput()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                return true;

            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    return true;
            }

            return false;
        }

        private bool TryShow()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return false;

            BackpackController backpack = FindLegalBackpack(game);
            if (backpack != null && backpack.TryGetLegalSlide(game.Occupancy, out ArrowDirection slideDir))
                return ShowSwipe(backpack, slideDir, game);

            ArrowController arrow = FindSafeArrow(game);
            if (arrow != null)
                return ShowTap(arrow, game);

            return false;
        }

        private static BackpackController FindLegalBackpack(GameManager game)
        {
            IReadOnlyList<BackpackController> packs = game.AllBackpacks;
            if (packs == null) return null;

            for (int i = 0; i < packs.Count; i++)
            {
                BackpackController pack = packs[i];
                if (pack == null || !pack.IsAlive) continue;
                if (pack.TryGetLegalSlide(game.Occupancy, out _))
                    return pack;
            }

            return null;
        }

        private static ArrowController FindSafeArrow(GameManager game)
        {
            if (game.bonuses != null)
                return game.bonuses.FindSafeArrow();

            IReadOnlyList<ArrowController> arrows = game.ActiveArrows;
            if (arrows == null) return null;
            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow == null || arrow.IsRemoving) continue;
                if (game.EvaluateTap(arrow, out _) == MoveResult.Success)
                    return arrow;
            }

            return null;
        }

        private bool ShowSwipe(BackpackController backpack, ArrowDirection slideDir, GameManager game)
        {
            if (!EnsureOverlay()) return false;
            if (!TryWorldToLocal(backpack.WorldCenter, out Vector2 center)) return false;

            float cell = game.Occupancy != null ? game.Occupancy.CellSize : GameConstants.CellSize;
            Vector2Int step = ArrowController.DirectionToCellStep(slideDir);
            Vector3 worldDelta = new Vector3(step.x, step.y, 0f) * cell * SwipeSpanCells;
            if (!TryWorldToLocal(backpack.WorldCenter - worldDelta * 0.5f, out swipeFrom))
                return false;
            if (!TryWorldToLocal(backpack.WorldCenter + worldDelta * 0.5f, out swipeTo))
                swipeTo = center + ((Vector2)worldDelta).normalized * 80f;

            swipeMode = true;
            showing = true;
            animSeconds = 0f;
            fadeSeconds = 0f;
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            PlaceHand(swipeFrom, swipeTo - swipeFrom);
            overlay.gameObject.SetActive(true);
            if (fade != null) fade.alpha = 0f;

            if (game.uiManager != null)
                game.uiManager.ShowMessage(GameTexts.SchoolIdleSwipe, MessageSeconds);
            return true;
        }

        private bool ShowTap(ArrowController arrow, GameManager game)
        {
            if (!EnsureOverlay()) return false;
            if (!TryWorldToLocal(arrow.transform.position, out tapRest)) return false;

            swipeMode = false;
            showing = true;
            animSeconds = 0f;
            fadeSeconds = 0f;
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            PlaceHand(tapRest, Vector2.down);
            overlay.gameObject.SetActive(true);
            if (fade != null) fade.alpha = 0f;

            arrow.PlayHintHighlight(2.2f);
            if (game.uiManager != null)
                game.uiManager.ShowMessage(GameTexts.SchoolIdleTap, MessageSeconds);
            return true;
        }

        private void Animate()
        {
            fadeSeconds += Time.unscaledDeltaTime;
            if (fade != null)
                fade.alpha = Mathf.Clamp01(fadeSeconds / FadeSeconds);

            animSeconds += Time.unscaledDeltaTime;
            if (swipeMode)
                AnimateSwipe();
            else
                AnimateTap();
        }

        private void AnimateSwipe()
        {
            float cycle = SwipeSeconds + SwipePause;
            float t = animSeconds % cycle;
            Vector2 pos;
            if (t <= SwipeSeconds)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / SwipeSeconds);
                pos = Vector2.Lerp(swipeFrom, swipeTo, u);
            }
            else
            {
                pos = swipeFrom;
            }

            PlaceHand(pos, swipeTo - swipeFrom);
        }

        private void AnimateTap()
        {
            float u = 0.5f + 0.5f * Mathf.Sin(animSeconds / TapSeconds * Mathf.PI * 2f);
            Vector2 pos = tapRest + Vector2.up * (10f * (1f - u));
            PlaceHand(pos, Vector2.down);
        }

        private void PlaceHand(Vector2 local, Vector2 pointDir)
        {
            if (hand == null) return;
            hand.anchoredPosition = local;
            if (pointDir.sqrMagnitude < 0.0001f) pointDir = Vector2.down;
            float angle = Mathf.Atan2(pointDir.y, pointDir.x) * Mathf.Rad2Deg - 90f;
            hand.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool TryWorldToLocal(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (overlay == null) return false;
            BindCamera();
            if (gameCamera == null) return false;

            Vector3 screen = gameCamera.WorldToScreenPoint(world);
            if (screen.z < 0f) return false;
            if (BannerSafeArea.IsScreenPointInReserve(screen)) return false;

            Canvas canvas = overlay.GetComponentInParent<Canvas>();
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlay, screen, eventCam, out local);
        }

        private void BindCamera()
        {
            GameManager game = GameManager.Instance;
            if (game != null && game.inputHandler != null && game.inputHandler.gameCamera != null)
            {
                gameCamera = game.inputHandler.gameCamera;
                return;
            }

            if (BoardCameraFitter.Instance != null && BoardCameraFitter.Instance.gameCamera != null)
            {
                gameCamera = BoardCameraFitter.Instance.gameCamera;
                return;
            }

            if (gameCamera == null) gameCamera = Camera.main;
        }

        private bool EnsureOverlay()
        {
            if (overlay != null && hand != null) return true;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            // BannerContent: игровое окно без полосы баннера. UiFrame не трогаем — телефонная вёрстка как была.
            Transform parent = ResolveOverlayParent(canvas);
            Transform existing = parent.Find(OverlayName);
            if (existing == null && parent != canvas.transform)
                existing = canvas.transform.Find(OverlayName);

            if (existing != null)
            {
                overlay = existing as RectTransform;
            }
            else
            {
                var go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(parent, false);
                overlay = go.GetComponent<RectTransform>();
            }

            overlay.SetParent(parent, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.pivot = new Vector2(0.5f, 0.5f);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.localScale = Vector3.one;
            overlay.SetAsLastSibling();

            fade = overlay.GetComponent<CanvasGroup>();
            if (fade == null) fade = overlay.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.interactable = false;

            if (overlay.GetComponent<RectMask2D>() == null)
                overlay.gameObject.AddComponent<RectMask2D>();

            Transform handTf = overlay.Find("Hand");
            if (handTf == null)
            {
                var handGo = new GameObject("Hand", typeof(RectTransform));
                handGo.transform.SetParent(overlay, false);
                handTf = handGo.transform;
            }

            hand = handTf as RectTransform;
            hand.anchorMin = new Vector2(0.5f, 0.5f);
            hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.pivot = new Vector2(0.5f, 1f);
            hand.sizeDelta = new Vector2(72f, 100f);
            hand.localScale = Vector3.one;

            handImage = hand.GetComponent<Image>();
            if (handImage == null) handImage = hand.gameObject.AddComponent<Image>();
            handImage.sprite = ResolveFingerSprite();
            handImage.color = Color.white;
            handImage.raycastTarget = false;
            handImage.preserveAspect = true;
            handImage.type = Image.Type.Simple;

            overlay.gameObject.SetActive(false);
            return true;
        }

        /// <summary>
        /// Родитель — BannerContent (уже с вычетом баннера справа/снизу).
        /// Не UiFrame: на широком ПК рамка уже колонка, а портфель в мире камеры.
        /// World→screen кладёт палец в портфель, а не в полосу баннера.
        /// </summary>
        private static Transform ResolveOverlayParent(Canvas canvas)
        {
            BannerSafeArea area = BannerSafeArea.For(canvas.transform);
            if (area != null && area.ContentRoot != null)
                return area.ContentRoot;
            return canvas.transform;
        }

        private static Sprite ResolveFingerSprite()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.schoolIdleHand != null)
                return art.schoolIdleHand;
            return GetBuiltFinger();
        }

        private static Sprite GetBuiltFinger()
        {
            if (cachedFinger != null) return cachedFinger;

            const int w = 128;
            const int h = 176;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };

            var pixels = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Color32 skin = new Color32(255, 214, 168, 255);
            Color32 line = new Color32(196, 142, 96, 255);

            FillRoundRect(pixels, w, h, 28, 8, 100, 78, 22, skin);
            FillRoundRect(pixels, w, h, 50, 70, 78, 168, 14, skin);
            StrokeRoundRect(pixels, w, h, 28, 8, 100, 78, 22, line);
            StrokeRoundRect(pixels, w, h, 50, 70, 78, 168, 14, line);

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            cachedFinger = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 1f), 128f);
            cachedFinger.name = "SchoolIdleFinger";
            return cachedFinger;
        }

        private static void FillRoundRect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int r, Color32 color)
        {
            for (int y = y0; y <= y1; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || x >= w) continue;
                    if (OutsideRound(x, y, x0, y0, x1, y1, r)) continue;
                    px[y * w + x] = color;
                }
            }
        }

        private static void StrokeRoundRect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int r, Color32 color)
        {
            int inner = Mathf.Max(1, r - 3);
            for (int y = y0; y <= y1; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || x >= w) continue;
                    if (OutsideRound(x, y, x0, y0, x1, y1, r)) continue;
                    if (!OutsideRound(x, y, x0 + 3, y0 + 3, x1 - 3, y1 - 3, inner)) continue;
                    px[y * w + x] = color;
                }
            }
        }

        private static bool OutsideRound(int x, int y, int x0, int y0, int x1, int y1, int r)
        {
            if (r <= 0) return false;
            int cx = x < x0 + r ? x0 + r : (x > x1 - r ? x1 - r : x);
            int cy = y < y0 + r ? y0 + r : (y > y1 - r ? y1 - r : y);
            if (cx == x && cy == y) return false;
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy > r * r;
        }

        private static float NextWait()
        {
            return GameConstants.SchoolIdleHintSeconds;
        }
    }
}
