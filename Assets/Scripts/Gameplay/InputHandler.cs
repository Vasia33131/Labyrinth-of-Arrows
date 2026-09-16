using UnityEngine;
using UnityEngine.EventSystems;

namespace Unpuzzle
{
    /// <summary>
    /// Стрелка / книга / указка — тап в клетку. Портфель — свайп вдоль оси, без сдвига от короткого тапа.
    /// Без Physics2D, без коллайдеров.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        [Header("References")]
        public Camera gameCamera;
        public GameManager gameManager;

        [Header("Input")]
        public bool blockWhenOverUI = true;
        public bool inputEnabled = true;

        private const int MouseFingerId = -1;

        private bool swipeArmed;
        private int swipeFingerId = MouseFingerId;
        private Vector2 swipeStartScreen;
        private BackpackController swipeBackpack;

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameManager == null) gameManager = GameManager.Instance;
        }

        private void Update()
        {
            if (!inputEnabled || IsGameplayBlocked()
                || (gameManager != null && (gameManager.IsIntroPlaying || gameManager.IsBlockedBumpPlaying)))
            {
                CancelBackpackSwipe();
                return;
            }

            if (ProcessMouseInput()) return;
            ProcessTouchInput();
        }

        private bool ProcessMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                SchoolIdleHint.NotifyInput();
                if (BannerSafeArea.IsScreenPointInReserve(Input.mousePosition)) return true;
                if (blockWhenOverUI && IsPointerOverUI(-1)) return true;
                BeginPointer(Input.mousePosition, MouseFingerId);
                return true;
            }

            if (swipeArmed && swipeFingerId == MouseFingerId)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    EndBackpackSwipe(Input.mousePosition);
                    return true;
                }

                if (Input.GetMouseButton(0)) return true;
            }

            return false;
        }

        private void ProcessTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    SchoolIdleHint.NotifyInput();
                    if (BannerSafeArea.IsScreenPointInReserve(touch.position)) continue;
                    if (blockWhenOverUI && IsPointerOverUI(touch.fingerId)) continue;
                    BeginPointer(touch.position, touch.fingerId);
                    continue;
                }

                if (!swipeArmed || touch.fingerId != swipeFingerId) continue;
                if (touch.phase == TouchPhase.Ended)
                    EndBackpackSwipe(touch.position);
                else if (touch.phase == TouchPhase.Canceled)
                    CancelBackpackSwipe();
            }
        }

        private void BeginPointer(Vector2 screenPosition, int fingerId)
        {
            if (BannerSafeArea.IsScreenPointInReserve(screenPosition)) return;
            if (!TryResolveWorld(screenPosition, out Vector3 world, out OccupancyGrid occupancy))
                return;

            Vector2Int cell = occupancy.WorldToCell(world);
            ArrowController arrow = occupancy.Get(cell);
            if (arrow != null && !arrow.IsRemoving)
            {
                CancelBackpackSwipe();
                gameManager.OnArrowTapped(arrow);
                return;
            }

            IGridBlocker blocker = occupancy.GetBlocker(cell);
            if (blocker is PointerController pointer)
            {
                CancelBackpackSwipe();
                gameManager.OnPointerTapped(pointer);
                return;
            }

            if (blocker is BookController book)
            {
                CancelBackpackSwipe();
                gameManager.OnBookTapped(book);
                return;
            }

            if (blocker is BackpackController backpack)
            {
                swipeArmed = true;
                swipeFingerId = fingerId;
                swipeStartScreen = screenPosition;
                swipeBackpack = backpack;
                return;
            }

            if (TryPickNearbyArrow(world, out arrow))
            {
                CancelBackpackSwipe();
                gameManager.OnArrowTapped(arrow);
                return;
            }

            CancelBackpackSwipe();
        }

        /// <summary>
        /// Пустая клетка / край поля: на мобилке ловим выступ увеличенного наконечника.
        /// Занятую книгу, указку и портфель не перехватываем — их клетка уже обработана выше.
        /// </summary>
        private bool TryPickNearbyArrow(Vector3 world, out ArrowController arrow)
        {
            arrow = null;
            if (!ArrowController.UseMobileVisual || gameManager == null) return false;

            var arrows = gameManager.ActiveArrows;
            if (arrows == null) return false;

            float best = float.MaxValue;
            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowController candidate = arrows[i];
                if (candidate == null || candidate.IsRemoving) continue;
                if (!candidate.TryHitVisual(world, out float dist)) continue;
                if (dist >= best) continue;
                best = dist;
                arrow = candidate;
            }

            return arrow != null;
        }

        private void EndBackpackSwipe(Vector2 screenPosition)
        {
            BackpackController backpack = swipeBackpack;
            Vector2 startScreen = swipeStartScreen;
            CancelBackpackSwipe();
            if (backpack == null || !backpack.IsAlive) return;
            if (BannerSafeArea.IsScreenPointInReserve(screenPosition)) return;
            if (!TryResolveWorld(startScreen, out Vector3 from, out _)) return;
            if (!TryResolveWorld(screenPosition, out Vector3 to, out _)) return;
            if (!backpack.TryResolveSwipe(from, to, out ArrowDirection slideDir)) return;

            gameManager.OnBackpackSwiped(backpack, slideDir);
        }

        private void CancelBackpackSwipe()
        {
            swipeArmed = false;
            swipeFingerId = MouseFingerId;
            swipeBackpack = null;
        }

        private bool TryResolveWorld(Vector2 screenPosition, out Vector3 world, out OccupancyGrid occupancy)
        {
            world = Vector3.zero;
            occupancy = null;
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return false;

            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.Occupancy == null) return false;
            if (gameManager.IsIntroPlaying) return false;
            if (gameManager.IsBlockedBumpPlaying) return false;

            world = gameCamera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;
            occupancy = gameManager.Occupancy;
            return true;
        }

        private static bool IsGameplayBlocked()
        {
            return OrientationGate.IsBlocking
                || SchoolTutorialPanel.IsOpen
                || ShopPanel.IsOpen
                || SettingsPanel.IsOpen
                || DailyHintsPanel.IsOpen
                || InterstitialAds.IsShowing;
        }

        private static bool IsPointerOverUI(int pointerId)
        {
            if (EventSystem.current == null) return false;

            return pointerId < 0
                ? EventSystem.current.IsPointerOverGameObject()
                : EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}
