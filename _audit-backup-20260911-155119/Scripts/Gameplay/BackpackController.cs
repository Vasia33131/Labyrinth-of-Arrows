using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Портфель: две соседние клетки, с поля не уезжает, ход не тратит.
    /// JSON dir = ось и «+». Тап ближе к торцу вдоль AxisDir → один шаг +;
    /// ближе к противоположному → один шаг −. Середина → CurrentSlideDir.
    /// В одном тапе нельзя развернуться и шагнуть в другую сторону.
    /// </summary>
    public class BackpackController : MonoBehaviour, IGridBlocker
    {
        public const int SortingOrder = 12;
        public const float SizeInCells = 0.90f;
        public const int CellCount = 2;

        public static readonly Color StubBody = new Color(0.34f, 0.45f, 0.58f, 1f);
        public static readonly Color StubBodyAlt = new Color(0.44f, 0.48f, 0.54f, 1f);

        [Header("Данные")]
        public string backpackId;

        [Header("Арт (null = квадрат-заглушка)")]
        public Sprite bodySprite;

        /// <summary>Первая клетка из JSON. Вторая = первая + шаг оси.</summary>
        public Vector2Int Anchor { get; private set; }

        /// <summary>Ось раскладки (не меняется). Right → клетки (x,y) и (x+1,y).</summary>
        public ArrowDirection AxisDir { get; private set; }

        /// <summary>Последний успешный сдвиг. На выбор торца не влияет, кроме попадания ровно в середину.</summary>
        public ArrowDirection CurrentSlideDir { get; private set; }

        public Vector2Int Cell => Anchor;
        public IReadOnlyList<Vector2Int> OccupiedCells => cells;
        public bool BlocksExit => IsAlive;
        public bool IsAlive => gameObject != null && gameObject.activeSelf;

        private readonly List<Vector2Int> cells = new List<Vector2Int>(CellCount);
        private readonly List<SpriteRenderer> segmentRenderers = new List<SpriteRenderer>(CellCount);
        private float cellSize = GameConstants.CellSize;
        private OccupancyGrid occupancy;
        private Coroutine shakeRoutine;
        private Coroutine flashRoutine;

        public void Setup(
            string id,
            Vector2Int anchor,
            ArrowDirection axisDir,
            OccupancyGrid grid,
            Sprite authoredSprite = null)
        {
            backpackId = id;
            Anchor = anchor;
            AxisDir = axisDir;
            CurrentSlideDir = axisDir;
            occupancy = grid;
            cellSize = grid != null ? grid.CellSize : GameConstants.CellSize;
            if (authoredSprite != null) bodySprite = authoredSprite;

            cells.Clear();
            BuildCells(anchor, axisDir, cells);

            if (grid != null) transform.position = grid.CellToWorld(anchor);
            else transform.position = new Vector3(anchor.x * cellSize, anchor.y * cellSize, 0f);
            transform.rotation = Quaternion.identity;

            RebuildVisual();
            gameObject.SetActive(true);
        }

        public void RestorePose(Vector2Int anchor, ArrowDirection axisDir, ArrowDirection slideDir)
        {
            StopFeedback();
            Anchor = anchor;
            AxisDir = axisDir;
            CurrentSlideDir = slideDir;
            cells.Clear();
            BuildCells(anchor, axisDir, cells);
            if (occupancy != null) transform.position = occupancy.CellToWorld(Anchor);
            transform.rotation = Quaternion.identity;
            RebuildVisual();
            gameObject.SetActive(true);
        }

        public Vector2Int[] CopyCells()
        {
            var copy = new Vector2Int[cells.Count];
            cells.CopyTo(copy);
            return copy;
        }

        /// <summary>
        /// Один шаг в выбранную сторону. Занято / край — отказ, поза и CurrentSlideDir без изменения.
        /// Сдвиг не выезд: остаёмся блокером. Вызывающий не тратит ход и не крутит указку.
        /// </summary>
        public bool TrySlide(OccupancyGrid grid, Vector3 worldTap)
        {
            if (grid == null) grid = occupancy;
            if (grid == null || cells.Count != CellCount) return false;

            ArrowDirection slideDir = ChooseSlideDir(grid, worldTap);
            if (!TrySlideIn(grid, slideDir)) return false;

            CurrentSlideDir = slideDir;
            return true;
        }

        /// <summary>
        /// Ближе к торцу вдоль AxisDir → +. Ближе к противоположному → −.
        /// Ровно середина → CurrentSlideDir (без разворота-и-шага).
        /// </summary>
        public ArrowDirection ChooseSlideDir(OccupancyGrid grid, Vector3 worldTap)
        {
            Vector3 plusWorld = EndWorld(grid, true);
            Vector3 minusWorld = EndWorld(grid, false);
            float dPlus = (worldTap - plusWorld).sqrMagnitude;
            float dMinus = (worldTap - minusWorld).sqrMagnitude;
            const float midEps = 0.000001f;
            if (Mathf.Abs(dPlus - dMinus) <= midEps)
                return CurrentSlideDir;
            return dPlus < dMinus ? AxisDir : Opposite(AxisDir);
        }

        private Vector3 EndWorld(OccupancyGrid grid, bool plus)
        {
            Vector2Int step = ArrowController.DirectionToCellStep(AxisDir);
            Vector2Int cell = plus ? Anchor + step : Anchor;
            if (grid != null) return grid.CellToWorld(cell);
            float size = cellSize > 0.01f ? cellSize : GameConstants.CellSize;
            return new Vector3(cell.x * size, cell.y * size, 0f);
        }

        public void PlayRejectFeedback()
        {
            if (!gameObject.activeInHierarchy) return;
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(RejectShakeRoutine());
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(RejectFlashRoutine());
        }

        private bool TrySlideIn(OccupancyGrid grid, ArrowDirection slideDir)
        {
            Vector2Int step = ArrowController.DirectionToCellStep(slideDir);
            Vector2Int newAnchor = Anchor + step;
            Vector2Int[] next = BuildCells(newAnchor, AxisDir);
            if (next == null || next.Length != CellCount) return false;

            for (int i = 0; i < next.Length; i++)
            {
                Vector2Int cell = next[i];
                if (!grid.InBounds(cell)) return false;

                ArrowController arrow = grid.Get(cell);
                if (arrow != null && !arrow.IsRemoving) return false;

                IGridBlocker blocker = grid.GetBlocker(cell);
                if (blocker != null && !ReferenceEquals(blocker, this) && blocker.BlocksExit) return false;
            }

            grid.UnregisterBlocker(this);
            Anchor = newAnchor;
            cells.Clear();
            for (int i = 0; i < next.Length; i++) cells.Add(next[i]);
            grid.RegisterBlocker(this);
            RebuildVisual();
            return true;
        }

        private void StopFeedback()
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
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
        private Color RestBodyAltColor => bodySprite != null ? Color.white : StubBodyAlt;

        private IEnumerator RejectFlashRoutine()
        {
            const float duration = 0.16f;
            Color fromA = new Color(0.55f, 0.38f, 0.42f, 1f);
            Color fromB = new Color(0.62f, 0.44f, 0.40f, 1f);
            Color toA = RestBodyColor;
            Color toB = RestBodyAltColor;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                ApplySegmentColors(Color.Lerp(fromA, toA, u), Color.Lerp(fromB, toB, u));
                yield return null;
            }

            ApplySegmentColors(toA, toB);
            flashRoutine = null;
        }

        private void RebuildVisual()
        {
            EnsureSegments();
            if (occupancy != null) transform.position = occupancy.CellToWorld(Anchor);
            transform.rotation = Quaternion.identity;

            if (bodySprite != null)
                RebuildAuthoredVisual();
            else
                RebuildStubVisual();
        }

        private void RebuildAuthoredVisual()
        {
            Sprite sprite = bodySprite;
            if (sprite == null) sprite = ArrowSpriteFactory.GetSquare();
            if (segmentRenderers.Count == 0) return;

            SpriteRenderer renderer = segmentRenderers[0];
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = SortingOrder;
            renderer.gameObject.SetActive(true);

            Vector2Int step = ArrowController.DirectionToCellStep(AxisDir);
            renderer.transform.localPosition = new Vector3(step.x * cellSize * 0.5f, step.y * cellSize * 0.5f, 0f);
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, AxisSpriteAngle(AxisDir));

            float targetW = 2f * cellSize * SizeInCells;
            float spriteW = sprite != null ? sprite.bounds.size.x : 1f;
            if (spriteW < 0.0001f) spriteW = 1f;
            renderer.transform.localScale = Vector3.one * (targetW / spriteW);

            for (int i = 1; i < segmentRenderers.Count; i++)
            {
                if (segmentRenderers[i] != null)
                    segmentRenderers[i].gameObject.SetActive(false);
            }
        }

        private void RebuildStubVisual()
        {
            Sprite sprite = ArrowSpriteFactory.GetSquare();
            if (sprite == null) sprite = ArrowSpriteFactory.GetSquare();

            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer renderer = segmentRenderers[i];
                Sprite cellSprite = sprite;
                if (cellSprite == null) cellSprite = ArrowSpriteFactory.GetSquare();
                renderer.sprite = cellSprite;
                renderer.color = i == 0 ? StubBody : StubBodyAlt;
                renderer.sortingLayerID = 0;
                renderer.sortingOrder = SortingOrder;

                float world = cellSize * SizeInCells;
                float spriteSize = 1f;
                if (cellSprite != null)
                    spriteSize = Mathf.Max(cellSprite.bounds.size.x, cellSprite.bounds.size.y);
                if (spriteSize < 0.0001f) spriteSize = 1f;
                renderer.transform.localScale = Vector3.one * (world / spriteSize);

                Vector2Int delta = cells[i] - Anchor;
                renderer.transform.localPosition = new Vector3(delta.x * cellSize, delta.y * cellSize, 0f);
                renderer.transform.localRotation = Quaternion.identity;
                renderer.gameObject.SetActive(true);
            }

            for (int i = cells.Count; i < segmentRenderers.Count; i++)
            {
                if (segmentRenderers[i] != null)
                    segmentRenderers[i].gameObject.SetActive(false);
            }
        }

        private static float AxisSpriteAngle(ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Right: return 0f;
                case ArrowDirection.Up: return 90f;
                case ArrowDirection.Left: return 180f;
                case ArrowDirection.Down: return -90f;
                default: return 0f;
            }
        }

        private void ApplySegmentColors(Color first, Color second)
        {
            for (int i = 0; i < segmentRenderers.Count; i++)
            {
                SpriteRenderer renderer = segmentRenderers[i];
                if (renderer == null || !renderer.gameObject.activeSelf) continue;
                renderer.color = i == 0 ? first : second;
            }
        }

        private void EnsureSegments()
        {
            GameConstants.SetLayerSafe(gameObject, GameConstants.ArrowLayerName);

            while (segmentRenderers.Count < cells.Count)
            {
                int index = segmentRenderers.Count;
                string name = $"Seg{index}";
                Transform found = transform.Find(name);
                SpriteRenderer renderer = found != null ? found.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(transform, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                }

                GameConstants.SetLayerSafe(renderer.gameObject, GameConstants.ArrowLayerName);
                segmentRenderers.Add(renderer);
            }
        }

        public static void BuildCells(Vector2Int anchor, ArrowDirection axisDir, List<Vector2Int> dst)
        {
            if (dst == null) return;
            dst.Clear();
            Vector2Int step = ArrowController.DirectionToCellStep(axisDir);
            dst.Add(anchor);
            dst.Add(anchor + step);
        }

        public static Vector2Int[] BuildCells(Vector2Int anchor, ArrowDirection axisDir)
        {
            var list = new List<Vector2Int>(CellCount);
            BuildCells(anchor, axisDir, list);
            return list.ToArray();
        }

        public static ArrowDirection Opposite(ArrowDirection dir)
        {
            return (ArrowDirection)(((int)dir + 2) % 4);
        }
    }
}
