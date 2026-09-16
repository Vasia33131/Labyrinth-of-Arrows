using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Книги школьного режима: авторский JSON или авторасстановка по lockParents.
    /// AutoPlace только если data.books == null. books: [] — без книг, не дорисовывать.
    /// Кампанию не трогает — вызывается только из школьной загрузки.
    /// </summary>
    public static class SchoolBookResolver
    {
        public static LevelBookData[] Resolve(LevelData data)
        {
            if (data == null) return Array.Empty<LevelBookData>();
            if (data.books != null) return data.books;
            return AutoPlace(data);
        }

        /// <summary>
        /// Книга на первую свободную клетку луча CanExit от кончика.
        /// Не ставить на path стрелок и не на клетку указки/портфеля/другой книги.
        /// keyArrowId = первый lockParent; если его нет в lockParents — книгу не ставим.
        /// Нет свободной клетки на луче — книгу не ставим.
        /// </summary>
        public static LevelBookData[] AutoPlace(LevelData data)
        {
            if (data == null || data.arrows == null || data.arrows.Length == 0)
                return Array.Empty<LevelBookData>();

            int width = Mathf.Max(1, data.gridWidth);
            int height = Mathf.Max(1, data.gridHeight);
            var blockedCells = CollectBlockedCells(data);
            var byId = new Dictionary<string, LevelArrowData>();

            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData arrow = data.arrows[i];
                string id = string.IsNullOrWhiteSpace(arrow.id) ? $"a{i}" : arrow.id;
                if (!byId.ContainsKey(id)) byId.Add(id, arrow);
            }

            var placed = new List<LevelBookData>();
            var usedCells = new HashSet<long>();

            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData child = data.arrows[i];
                string[] parents = child.GetLockParents();
                if (parents.Length == 0) continue;

                string keyId = parents[0];
                string childId = string.IsNullOrWhiteSpace(child.id) ? $"a{i}" : child.id;
                if (!KeyIsLockParent(child, keyId, byId, childId)) continue;

                if (!TryFirstExitCell(child, width, height, blockedCells, usedCells, out Vector2Int cell))
                    continue;

                usedCells.Add(Pack(cell.x, cell.y));
                placed.Add(new LevelBookData
                {
                    id = $"book_{childId}",
                    x = cell.x,
                    y = cell.y,
                    blockedArrowId = childId,
                    keyArrowId = keyId
                });
            }

            return placed.Count > 0 ? placed.ToArray() : Array.Empty<LevelBookData>();
        }

        public static bool KeyIsLockParent(LevelArrowData blocked, string keyId)
        {
            if (blocked == null || string.IsNullOrEmpty(keyId)) return false;
            string[] parents = blocked.GetLockParents();
            for (int p = 0; p < parents.Length; p++)
            {
                if (parents[p] == keyId) return true;
            }

            return false;
        }

        public static bool IsOnCanExitRay(LevelArrowData blocked, int x, int y)
        {
            return IsOnCanExitRay(blocked, x, y, 0, 0);
        }

        public static bool IsOnCanExitRay(LevelArrowData blocked, int x, int y, int width, int height)
        {
            if (blocked == null) return false;
            Vector2Int[] path = blocked.GetPath();
            if (path == null || path.Length == 0) return false;

            Vector2Int step = ArrowController.DirectionToCellStep(blocked.ParseDirection());
            Vector2Int cursor = path[path.Length - 1] + step;
            bool bounded = width > 0 && height > 0;
            int guard = bounded ? width + height + 2 : 64;
            while (guard-- > 0)
            {
                if (bounded && (cursor.x < 0 || cursor.y < 0 || cursor.x >= width || cursor.y >= height))
                    return false;
                if (cursor.x == x && cursor.y == y) return true;
                cursor += step;
            }

            return false;
        }

        private static bool KeyIsLockParent(
            LevelArrowData child,
            string keyId,
            Dictionary<string, LevelArrowData> byId,
            string childId)
        {
            if (string.IsNullOrEmpty(keyId) || !byId.ContainsKey(keyId)) return false;
            if (keyId == childId) return false;
            return KeyIsLockParent(child, keyId);
        }

        private static bool TryFirstExitCell(
            LevelArrowData child,
            int width,
            int height,
            HashSet<long> blockedCells,
            HashSet<long> usedCells,
            out Vector2Int cell)
        {
            cell = default;
            Vector2Int[] path = child.GetPath();
            if (path == null || path.Length == 0) return false;

            Vector2Int step = ArrowController.DirectionToCellStep(child.ParseDirection());
            Vector2Int cursor = path[path.Length - 1] + step;
            while (cursor.x >= 0 && cursor.y >= 0 && cursor.x < width && cursor.y < height)
            {
                long key = Pack(cursor.x, cursor.y);
                if (!blockedCells.Contains(key) && !usedCells.Contains(key))
                {
                    cell = cursor;
                    return true;
                }

                cursor += step;
            }

            return false;
        }

        private static HashSet<long> CollectBlockedCells(LevelData data)
        {
            var cells = new HashSet<long>();
            if (data.arrows != null)
            {
                for (int i = 0; i < data.arrows.Length; i++)
                {
                    Vector2Int[] path = data.arrows[i].GetPath();
                    for (int c = 0; c < path.Length; c++)
                        cells.Add(Pack(path[c].x, path[c].y));
                }
            }

            if (data.pointers != null)
            {
                for (int i = 0; i < data.pointers.Length; i++)
                {
                    LevelPointerData pointer = data.pointers[i];
                    if (pointer == null) continue;
                    Vector2Int[] pointerCells = pointer.GetCells();
                    for (int c = 0; c < pointerCells.Length; c++)
                        cells.Add(Pack(pointerCells[c].x, pointerCells[c].y));
                }
            }

            if (data.backpacks != null)
            {
                for (int i = 0; i < data.backpacks.Length; i++)
                {
                    LevelBackpackData backpack = data.backpacks[i];
                    if (backpack == null) continue;
                    Vector2Int[] packCells = backpack.GetCells();
                    for (int c = 0; c < packCells.Length; c++)
                        cells.Add(Pack(packCells[c].x, packCells[c].y));
                }
            }

            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData book = data.books[i];
                    if (book == null) continue;
                    cells.Add(Pack(book.x, book.y));
                }
            }

            return cells;
        }

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
