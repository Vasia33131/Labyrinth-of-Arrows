using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Линейная указка: 3–5 клеток, тапом не снимается.
    /// После каждого успешного выезда стрелки поворачивается на 90° вокруг пивота.
    /// </summary>
    public class PointerController : MonoBehaviour, IGridBlocker
    {
        public const int SortingOrder = 11;
        public const float SizeInCells = 0.86f;
        public const float PivotSizeInCells = 0.96f;

        public static readonly Color StubBody = new Color(0.38f, 0.28f, 0.18f, 1f);
        public static readonly Color StubPivot = new Color(0.55f, 0.40f, 0.22f, 1f);

        [Header("Данные")]
        public string pointerId;
        public bool rotateCW = true;

        [Header("Арт (null = квадрат-заглушка)")]
        public Sprite bodySprite;
        public Sprite pivotSprite;

        public Vector2Int Pivot { get; private set; }
        public ArrowDirection Direction { get; private set; }
        public int Length { get; private set; }

        public Vector2Int Cell => Pivot;
        public IReadOnlyList<Vector2Int> OccupiedCells => cells;
        public bool BlocksExit => IsAlive;
        public bool IsAlive => gameObject != null && gameObject.activeSelf;

        private readonly List<Vector2Int> cells = new List<Vector2Int>(8);
        private readonly List<SpriteRenderer> segmentRenderers = new List<SpriteRenderer>(8);
        private float cellSize = GameConstants.CellSize;
        private OccupancyGrid occupancy;
        private Coroutine shakeRoutine;
        private Coroutine flashRoutine;

        public void Setup(
            string id,
            Vector2Int pivot,
            int length,
            ArrowDirection dir,
            bool clockwise,
            OccupancyGrid grid,
            Sprite authoredBody = null,
            Sprite authoredPivot = null)
        {
            pointerId = id;
            Pivot = pivot;
            Length = Mathf.Max(1, length);
            Direction = dir;
            rotateCW = clockwise;
            occupancy = grid;
            cellSize = grid != null ? grid.CellSize : GameConstants.CellSize;
            if (authoredBody != null) bodySprite = authoredBody;
            if (authoredPivot != null) pivotSprite = authoredPivot;

            cells.Clear();
            BuildCells(pivot, Length, dir, cells);

            if (grid != null) transform.position = grid.CellToWorld(pivot);
            else transform.position = new Vector3(pivot.x * cellSize, pivot.y * cellSize, 0f);
            transform.rotation = Quaternion.identity;

            RebuildVisual();
            gameObject.SetActive(true);
        }

        public void RestorePose(ArrowDirection dir, Vector2Int[] path)
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

            Direction = dir;
            cells.Clear();
            if (path != null && path.Length > 0)
            {
                for (int i = 0; i < path.Length; i++) cells.Add(path[i]);
                Pivot = cells[0];
                Length = cells.Count;
            }
            else
            {
                BuildCells(Pivot, Length, dir, cells);
            }

            if (occupancy != null) transform.position = occupancy.CellToWorld(Pivot);
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

        /// <summary>Поворот только после Success. Занято / вне сетки — пропуск, поза та же.</summary>
        public bool TryRotateAfterSuccess(OccupancyGrid grid)
        {
            if (grid == null) grid = occupancy;
            if (grid == null || cells.Count == 0) return false;

            ArrowDirection nextDir = RotateDir(Direction, rotateCW);
            var next = new List<Vector2Int>(cells.Count);
            RotateCellsAroundPivot(cells, Pivot, rotateCW, next);

            for (int i = 0; i < next.Count; i++)
            {
                Vector2Int cell = next[i];
                if (!grid.InBounds(cell)) return false;

                ArrowController arrow = grid.Get(cell);
                if (arrow != null && !arrow.IsRemoving) return false;

                IGridBlocker blocker = grid.GetBlocker(cell);
                if (blocker != null && !ReferenceEquals(blocker, this) && blocker.BlocksExit) return false;
            }

            grid.UnregisterBlocker(this);
            Direction = nextDir;
            cells.Clear();
            for (int i = 0; i < next.Count; i++) cells.Add(next[i]);
            grid.RegisterBlocker(this);
            RebuildVisual();
            GameAudio.Play(GameAudio.Sfx.SchoolPointerRotate);
            return true;
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
        private Color RestPivotColor => pivotSprite != null ? Color.white : StubPivot;

        private IEnumerator RejectFlashRoutine()
        {
            const float duration = 0.16f;
            Color bodyFrom = new Color(0.72f, 0.32f, 0.22f, 1f);
            Color pivotFrom = new Color(0.88f, 0.48f, 0.28f, 1f);
            Color bodyTo = RestBodyColor;
            Color pivotTo = RestPivotColor;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                ApplySegmentColors(Color.Lerp(bodyFrom, bodyTo, u), Color.Lerp(pivotFrom, pivotTo, u));
                yield return null;
            }

            ApplySegmentColors(bodyTo, pivotTo);
            flashRoutine = null;
        }

        private void RebuildVisual()
        {
            EnsureSegments();
            if (occupancy != null) transform.position = occupancy.CellToWorld(Pivot);

            Sprite body = bodySprite != null ? bodySprite : ArrowSpriteFactory.GetSquare();
            if (body == null) body = ArrowSpriteFactory.GetSquare();
            Sprite pivotSpr = pivotSprite != null ? pivotSprite : ArrowSpriteFactory.GetSquare();
            if (pivotSpr == null) pivotSpr = body;

            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer renderer = segmentRenderers[i];
                bool isPivot = i == 0;
                Sprite sprite = isPivot ? pivotSpr : body;
                if (sprite == null) sprite = ArrowSpriteFactory.GetSquare();
                renderer.sprite = sprite;
                renderer.color = isPivot ? RestPivotColor : RestBodyColor;
                renderer.sortingLayerID = 0;
                renderer.sortingOrder = SortingOrder + (isPivot ? 1 : 0);

                float world = cellSize * (isPivot ? PivotSizeInCells : SizeInCells);
                float spriteSize = 1f;
                if (sprite != null)
                    spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                if (spriteSize < 0.0001f) spriteSize = 1f;
                renderer.transform.localScale = Vector3.one * (world / spriteSize);

                Vector2Int delta = cells[i] - Pivot;
                renderer.transform.localPosition = new Vector3(delta.x * cellSize, delta.y * cellSize, 0f);
                bool alignBody = !isPivot && bodySprite != null;
                renderer.transform.localRotation = alignBody
                    ? Quaternion.Euler(0f, 0f, AxisSpriteAngle(Direction))
                    : Quaternion.identity;
                renderer.gameObject.SetActive(true);
            }

            for (int i = cells.Count; i < segmentRenderers.Count; i++)
            {
                if (segmentRenderers[i] != null)
                    segmentRenderers[i].gameObject.SetActive(false);
            }
        }

        private void ApplySegmentColors(Color body, Color pivot)
        {
            for (int i = 0; i < segmentRenderers.Count; i++)
            {
                SpriteRenderer renderer = segmentRenderers[i];
                if (renderer == null || !renderer.gameObject.activeSelf) continue;
                renderer.color = i == 0 ? pivot : body;
            }
        }

        private void EnsureSegments()
        {
            GameConstants.SetLayerSafe(gameObject, GameConstants.ArrowLayerName);

            while (segmentRenderers.Count < cells.Count)
            {
                int index = segmentRenderers.Count;
                string name = index == 0 ? "Pivot" : $"Seg{index}";
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

        public static void BuildCells(Vector2Int pivot, int length, ArrowDirection dir, List<Vector2Int> dst)
        {
            if (dst == null) return;
            dst.Clear();
            int len = Mathf.Max(1, length);
            Vector2Int step = ArrowController.DirectionToCellStep(dir);
            for (int i = 0; i < len; i++)
                dst.Add(pivot + step * i);
        }

        public static Vector2Int[] BuildCells(Vector2Int pivot, int length, ArrowDirection dir)
        {
            var list = new List<Vector2Int>(Mathf.Max(1, length));
            BuildCells(pivot, length, dir, list);
            return list.ToArray();
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

        public static ArrowDirection RotateDir(ArrowDirection dir, bool clockwise)
        {
            int steps = clockwise ? 1 : 3;
            return (ArrowDirection)(((int)dir + steps) % 4);
        }

        public static void RotateCellsAroundPivot(
            IReadOnlyList<Vector2Int> source,
            Vector2Int pivot,
            bool clockwise,
            List<Vector2Int> dst)
        {
            dst.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                int dx = source[i].x - pivot.x;
                int dy = source[i].y - pivot.y;
                if (clockwise) dst.Add(new Vector2Int(pivot.x + dy, pivot.y - dx));
                else dst.Add(new Vector2Int(pivot.x - dy, pivot.y + dx));
            }
        }

        public static Vector2Int[] RotateCellsAroundPivot(IReadOnlyList<Vector2Int> source, Vector2Int pivot, bool clockwise)
        {
            var list = new List<Vector2Int>(source != null ? source.Count : 0);
            RotateCellsAroundPivot(source, pivot, clockwise, list);
            return list.ToArray();
        }
    }
}
