using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>Не-стрелка на клетке: книга, указка, портфель. Тапом не снимается как выезд.</summary>
    public interface IGridBlocker
    {
        Vector2Int Cell { get; }
        IReadOnlyList<Vector2Int> OccupiedCells { get; }
        bool BlocksExit { get; }
    }

    /// <summary>Почему луч CanExit не проходит.</summary>
    public enum ExitObstruction
    {
        None = 0,
        Arrow = 1,
        Book = 2,
        Pointer = 3,
        Backpack = 4
    }

    /// <summary>
    /// Дискретная сетка: клетка → живая стрелка + слой блокеров.
    /// Без Physics2D. Get/GetAtWorld по-прежнему отдают только стрелку (тап).
    /// </summary>
    public class OccupancyGrid
    {
        public int Width { get; private set; } = 6;
        public int Height { get; private set; } = 8;
        public float CellSize { get; private set; } = GameConstants.CellSize;
        public Vector2 OriginOffset { get; private set; }

        private ArrowController[] map = new ArrowController[0];
        private IGridBlocker[] blockers = new IGridBlocker[0];

        public void Configure(int width, int height, float cellSize, Vector2 originOffset)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = cellSize > 0.01f ? cellSize : GameConstants.CellSize;
            OriginOffset = originOffset;
            map = new ArrowController[Width * Height];
            blockers = new IGridBlocker[Width * Height];
        }

        public void Clear()
        {
            if (map == null || map.Length != Width * Height)
                map = new ArrowController[Width * Height];
            else
                System.Array.Clear(map, 0, map.Length);

            if (blockers == null || blockers.Length != Width * Height)
                blockers = new IGridBlocker[Width * Height];
            else
                System.Array.Clear(blockers, 0, blockers.Length);
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        public Vector3 CellToWorld(Vector2Int gridPos)
        {
            float x = (gridPos.x - (Width - 1) * 0.5f) * CellSize + OriginOffset.x;
            float y = (gridPos.y - (Height - 1) * 0.5f) * CellSize + OriginOffset.y;
            return new Vector3(x, y, 0f);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            float x = (world.x - OriginOffset.x) / CellSize + (Width - 1) * 0.5f;
            float y = (world.y - OriginOffset.y) / CellSize + (Height - 1) * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
        }

        public ArrowController Get(Vector2Int cell)
        {
            if (!InBounds(cell) || map == null) return null;
            return map[cell.y * Width + cell.x];
        }

        public ArrowController GetAtWorld(Vector3 world)
        {
            return Get(WorldToCell(world));
        }

        public IGridBlocker GetBlocker(Vector2Int cell)
        {
            if (!InBounds(cell) || blockers == null) return null;
            return blockers[cell.y * Width + cell.x];
        }

        public void Register(ArrowController arrow)
        {
            if (arrow == null || arrow.Cells == null) return;

            for (int i = 0; i < arrow.Cells.Count; i++)
            {
                Vector2Int cell = arrow.Cells[i];
                if (!InBounds(cell)) continue;
                map[cell.y * Width + cell.x] = arrow;
            }
        }

        public void Unregister(ArrowController arrow)
        {
            if (arrow == null || map == null) return;

            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == arrow) map[i] = null;
            }
        }

        public void RegisterBlocker(IGridBlocker blocker)
        {
            if (blocker == null || blockers == null) return;

            IReadOnlyList<Vector2Int> cells = blocker.OccupiedCells;
            if (cells != null && cells.Count > 0)
            {
                for (int i = 0; i < cells.Count; i++)
                    SetBlockerCell(cells[i], blocker);
                return;
            }

            SetBlockerCell(blocker.Cell, blocker);
        }

        private void SetBlockerCell(Vector2Int cell, IGridBlocker blocker)
        {
            if (!InBounds(cell)) return;
            blockers[cell.y * Width + cell.x] = blocker;
        }

        public void UnregisterBlocker(IGridBlocker blocker)
        {
            if (blocker == null || blockers == null) return;

            for (int i = 0; i < blockers.Length; i++)
            {
                if (blockers[i] == blocker) blockers[i] = null;
            }
        }

        public void Rebuild(IReadOnlyList<ArrowController> arrows)
        {
            Rebuild(arrows, null);
        }

        public void Rebuild(IReadOnlyList<ArrowController> arrows, IReadOnlyList<BookController> books)
        {
            Rebuild(arrows, books, null);
        }

        public void Rebuild(
            IReadOnlyList<ArrowController> arrows,
            IReadOnlyList<BookController> books,
            IReadOnlyList<PointerController> pointers)
        {
            Rebuild(arrows, books, pointers, null);
        }

        public void Rebuild(
            IReadOnlyList<ArrowController> arrows,
            IReadOnlyList<BookController> books,
            IReadOnlyList<PointerController> pointers,
            IReadOnlyList<BackpackController> backpacks)
        {
            Clear();
            if (arrows != null)
            {
                for (int i = 0; i < arrows.Count; i++)
                {
                    ArrowController arrow = arrows[i];
                    if (arrow == null || !arrow.gameObject.activeSelf || !arrow.OccupiesGrid) continue;
                    Register(arrow);
                }
            }

            if (books != null)
            {
                for (int i = 0; i < books.Count; i++)
                {
                    BookController book = books[i];
                    if (book == null || !book.BlocksExit) continue;
                    RegisterBlocker(book);
                }
            }

            if (pointers != null)
            {
                for (int i = 0; i < pointers.Count; i++)
                {
                    PointerController pointer = pointers[i];
                    if (pointer == null || !pointer.BlocksExit) continue;
                    RegisterBlocker(pointer);
                }
            }

            if (backpacks == null) return;

            for (int i = 0; i < backpacks.Count; i++)
            {
                BackpackController backpack = backpacks[i];
                if (backpack == null || !backpack.BlocksExit) continue;
                RegisterBlocker(backpack);
            }
        }

        /// <summary>
        /// Змейка разматывается по своему path, затем уходит прямо от кончика в direction.
        /// Блокирует только луч от последней клетки пути до края; свои клетки не препятствие.
        /// Книга, указка и портфель на луче считаются как чужая стрелка.
        /// </summary>
        public bool CanExit(ArrowController arrow)
        {
            return GetExitObstruction(arrow) == ExitObstruction.None;
        }

        public ExitObstruction GetExitObstruction(ArrowController arrow)
        {
            return GetExitObstruction(arrow, out _);
        }

        public ExitObstruction GetExitObstruction(ArrowController arrow, out Vector2Int cell)
        {
            cell = default;
            if (arrow == null || arrow.Cells == null || arrow.Cells.Count == 0)
                return ExitObstruction.Arrow;

            Vector2Int step = ArrowController.DirectionToCellStep(arrow.direction);
            Vector2Int cursor = arrow.Cells[arrow.Cells.Count - 1] + step;
            while (InBounds(cursor))
            {
                ArrowController occupant = Get(cursor);
                if (occupant != null && occupant != arrow && occupant.OccupiesGrid)
                {
                    cell = cursor;
                    return ExitObstruction.Arrow;
                }

                IGridBlocker blocker = GetBlocker(cursor);
                if (blocker != null && blocker.BlocksExit)
                {
                    cell = cursor;
                    if (blocker is PointerController) return ExitObstruction.Pointer;
                    if (blocker is BackpackController) return ExitObstruction.Backpack;
                    return ExitObstruction.Book;
                }

                cursor += step;
            }

            return ExitObstruction.None;
        }
    }
}
