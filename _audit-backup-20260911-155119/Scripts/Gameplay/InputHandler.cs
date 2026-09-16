using UnityEngine;
using UnityEngine.EventSystems;

namespace Unpuzzle
{
    /// <summary>
    /// Тап: экран → мир → клетка сетки → стрелка, которая владеет клеткой.
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

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameManager == null) gameManager = GameManager.Instance;
        }

        private void Update()
        {
            if (!inputEnabled) return;
            if (gameManager != null && gameManager.IsIntroPlaying) return;
            if (gameManager != null && gameManager.IsBlockedBumpPlaying) return;
            if (SchoolTutorialPanel.IsOpen) return;
            if (ShopPanel.IsOpen) return;
            if (SettingsPanel.IsOpen) return;
            if (DailyHintsPanel.IsOpen) return;
            if (ProcessMouseInput()) return;
            ProcessTouchInput();
        }

        private bool ProcessMouseInput()
        {
            if (!Input.GetMouseButtonDown(0)) return false;
            if (blockWhenOverUI && IsPointerOverUI(-1)) return true;

            TryHandlePointer(Input.mousePosition);
            return true;
        }

        private void ProcessTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                if (blockWhenOverUI && IsPointerOverUI(touch.fingerId)) continue;

                TryHandlePointer(touch.position);
            }
        }

        private void TryHandlePointer(Vector2 screenPosition)
        {
            if (BannerSafeArea.IsScreenPointInReserve(screenPosition)) return;
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;

            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.Occupancy == null) return;
            if (gameManager.IsIntroPlaying) return;
            if (gameManager.IsBlockedBumpPlaying) return;

            Vector3 world = gameCamera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;

            OccupancyGrid occupancy = gameManager.Occupancy;
            Vector2Int cell = occupancy.WorldToCell(world);
            ArrowController arrow = occupancy.Get(cell);
            if (arrow != null && !arrow.IsRemoving)
            {
                gameManager.OnArrowTapped(arrow);
                return;
            }

            IGridBlocker blocker = occupancy.GetBlocker(cell);
            if (blocker is PointerController pointer)
                gameManager.OnPointerTapped(pointer);
            else if (blocker is BookController book)
                gameManager.OnBookTapped(book);
            else if (blocker is BackpackController backpack)
                gameManager.OnBackpackTapped(backpack, world);
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
