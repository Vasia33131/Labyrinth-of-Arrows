using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Большая книга на клетке. Не стрелка: тапом не убирается, ход не тратит.
    /// Occupancy считает её блокером луча. Исчезает, когда ключ успешно убран.
    /// </summary>
    public class BookController : MonoBehaviour, IGridBlocker
    {
        public const int SortingOrder = 13;
        public const float SizeInCells = 0.92f;

        public static readonly Color StubBody = new Color(0.52f, 0.26f, 0.10f, 1f);
        public static readonly Color StubIcon = new Color(0.72f, 0.42f, 0.18f, 1f);

        [Header("Данные")]
        public string bookId;
        public string blockedArrowId;
        public string keyArrowId;

        [Header("Арт (null = квадрат-заглушка)")]
        public Sprite bodySprite;
        public SpriteRenderer bodyRenderer;

        public Vector2Int Cell { get; private set; }
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
        public ArrowController BlockedArrow { get; private set; }
        public ArrowController KeyArrow { get; private set; }

        public bool IsAlive => gameObject != null && gameObject.activeSelf;
        public bool BlocksExit => IsAlive;

        private readonly Vector2Int[] occupiedCells = new Vector2Int[1];
        private Coroutine shakeRoutine;
        private Coroutine flashRoutine;

        public void Setup(
            string id,
            Vector2Int cell,
            string blockedId,
            string keyId,
            ArrowController blocked,
            ArrowController key,
            OccupancyGrid grid,
            Sprite authoredSprite = null)
        {
            bookId = id;
            Cell = cell;
            occupiedCells[0] = cell;
            blockedArrowId = blockedId;
            keyArrowId = keyId;
            BlockedArrow = blocked;
            KeyArrow = key;
            if (authoredSprite != null) bodySprite = authoredSprite;

            float cellSize = grid != null ? grid.CellSize : GameConstants.CellSize;
            if (grid != null) transform.position = grid.CellToWorld(cell);
            else transform.position = new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);

            EnsureBody(cellSize);
            gameObject.SetActive(true);
        }

        public void Dismiss()
        {
            if (GameManager.Instance != null && GameManager.Instance.Occupancy != null)
                GameManager.Instance.Occupancy.UnregisterBlocker(this);

            if (gameObject.activeSelf)
                GameAudio.Play(GameAudio.Sfx.SchoolBookVanish);
            gameObject.SetActive(false);
        }

        public void RestoreAlive()
        {
            gameObject.SetActive(true);
        }

        public void PlayRejectFeedback()
        {
            if (!gameObject.activeInHierarchy) return;
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(RejectShakeRoutine());
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(RejectFlashRoutine());
        }

        private IEnumerator RejectShakeRoutine()
        {
            Vector3 origin = transform.position;
            const float duration = 0.14f;
            const float amplitude = 0.06f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float falloff = 1f - t / duration;
                transform.position = origin + new Vector3(
                    Mathf.Sin(t * 64f) * amplitude * falloff,
                    Mathf.Cos(t * 48f) * amplitude * 0.35f * falloff,
                    0f);
                yield return null;
            }

            transform.position = origin;
            shakeRoutine = null;
        }

        private Color RestBodyColor => bodySprite != null ? Color.white : StubBody;

        private IEnumerator RejectFlashRoutine()
        {
            const float duration = 0.16f;
            Color from = new Color(0.72f, 0.32f, 0.22f, 1f);
            Color to = RestBodyColor;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (bodyRenderer != null)
                    bodyRenderer.color = Color.Lerp(from, to, t / duration);
                yield return null;
            }

            if (bodyRenderer != null) bodyRenderer.color = to;
            flashRoutine = null;
        }

        private void EnsureBody(float cellSize)
        {
            if (bodyRenderer == null)
            {
                Transform found = transform.Find("Body");
                if (found != null) bodyRenderer = found.GetComponent<SpriteRenderer>();
            }

            if (bodyRenderer == null)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(transform, false);
                bodyRenderer = body.AddComponent<SpriteRenderer>();
            }

            GameConstants.SetLayerSafe(gameObject, GameConstants.ArrowLayerName);
            GameConstants.SetLayerSafe(bodyRenderer.gameObject, GameConstants.ArrowLayerName);

            Sprite sprite = bodySprite != null ? bodySprite : ArrowSpriteFactory.GetSquare();
            if (sprite == null) sprite = ArrowSpriteFactory.GetSquare();
            bodyRenderer.sprite = sprite;
            bodyRenderer.color = RestBodyColor;
            bodyRenderer.sortingLayerID = 0;
            bodyRenderer.sortingOrder = SortingOrder;

            float world = cellSize * SizeInCells;
            float spriteSize = 1f;
            if (sprite != null)
                spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (spriteSize < 0.0001f) spriteSize = 1f;
            bodyRenderer.transform.localScale = Vector3.one * (world / spriteSize);
            bodyRenderer.transform.localPosition = Vector3.zero;
            bodyRenderer.transform.localRotation = Quaternion.identity;
        }
    }
}
