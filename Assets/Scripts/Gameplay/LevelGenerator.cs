using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Спавн уровня из Resources/Levels/level_XX.json в LevelRoot.
    /// JSON-поле buttons игнорируется — классический Unpuzzle без переключателей.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Ссылки")]
        public Transform levelRoot;
        public ArrowController arrowPrefab;
        public Transform switchesRoot;

        [Header("Сетка")]
        public Vector2Int gridSize = new Vector2Int(6, 8);
        public float cellSize = GameConstants.CellSize;
        public Vector2 originOffset = Vector2.zero;

        [Header("Отладка")]
        public bool useTestGrid;
        public int randomSeed = 12345;
        [Range(0f, 1f)] public float fillChance = 0.7f;

        [Header("Runtime")]
        public List<ArrowController> spawnedArrows = new List<ArrowController>();
        public List<BookController> spawnedBooks = new List<BookController>();
        public List<PointerController> spawnedPointers = new List<PointerController>();
        public List<BackpackController> spawnedBackpacks = new List<BackpackController>();

        [System.NonSerialized] public OccupancyGrid occupancy;

        public LevelData LastLoaded { get; private set; }

        public int Build(int levelIndex)
        {
            Clear();

            if (levelRoot == null || arrowPrefab == null)
            {
                GameLog.Error("[LevelGenerator] Не назначены levelRoot / arrowPrefab.");
                return 0;
            }

            if (useTestGrid) return BuildTestGrid(levelIndex);

            LevelData data = LevelCatalog.Load(levelIndex);
            if (data == null)
            {
                GameLog.Error($"[LevelGenerator] Нет файла Resources/{LevelCatalog.ResourcePath(levelIndex)}.json.");
                return 0;
            }

            return BuildFromData(data);
        }

        public int BuildFromData(LevelData data)
        {
            if (data == null) return 0;
            if (levelRoot == null || arrowPrefab == null)
            {
                GameLog.Error("[LevelGenerator] Не назначены levelRoot / arrowPrefab.");
                return 0;
            }

            Clear();
            LastLoaded = data;
            if (!LevelData.SanitizeBounds(data))
            {
                GameLog.Error("[LevelGenerator] Уровень с клетками вне сетки — отброшен.");
                LastLoaded = null;
                return 0;
            }

            gridSize = new Vector2Int(Mathf.Max(1, data.gridWidth), Mathf.Max(1, data.gridHeight));

            if (occupancy == null && GameManager.Instance != null)
                occupancy = GameManager.Instance.Occupancy;

            if (occupancy != null)
            {
                occupancy.Configure(gridSize.x, gridSize.y, cellSize, originOffset);
                occupancy.Clear();
            }

            var byId = new Dictionary<string, ArrowController>();
            LevelArrowData[] arrows = data.arrows ?? System.Array.Empty<LevelArrowData>();

            for (int i = 0; i < arrows.Length; i++)
            {
                Vector2Int[] check = arrows[i].GetPath();
                for (int c = 0; c < check.Length; c++)
                {
                    if (check[c].x < 0 || check[c].y < 0 || check[c].x >= gridSize.x || check[c].y >= gridSize.y)
                    {
                        GameLog.Error($"[LevelGenerator] Клетка ({check[c].x},{check[c].y}) вне сетки {gridSize.x}×{gridSize.y}.");
                        Clear();
                        return 0;
                    }
                }
            }

            for (int i = 0; i < arrows.Length; i++)
            {
                LevelArrowData entry = arrows[i];
                string id = string.IsNullOrWhiteSpace(entry.id) ? $"a{i}" : entry.id;
                Vector2Int[] path = entry.GetPath();
                Vector2Int rootCell = path[0];

                ArrowController arrow = Instantiate(arrowPrefab, GridToWorld(rootCell), Quaternion.identity, levelRoot);
                arrow.name = id;
                arrow.lockParents.Clear();
                arrow.Setup(entry.ParseDirection(), entry.ParseColor(), path, entry.locked, occupancy);
                spawnedArrows.Add(arrow);

                if (occupancy != null) occupancy.Register(arrow);
                if (!byId.ContainsKey(id)) byId.Add(id, arrow);
            }

            for (int i = 0; i < arrows.Length; i++)
            {
                string[] parents = arrows[i].GetLockParents();
                if (parents.Length == 0) continue;

                ArrowController child = spawnedArrows[i];
                for (int p = 0; p < parents.Length; p++)
                {
                    if (!byId.TryGetValue(parents[p], out ArrowController parent) || parent == child) continue;
                    if (!child.lockParents.Contains(parent)) child.lockParents.Add(parent);
                }

                child.RefreshLockState();
            }

            if (PlayProgress.IsSchool)
            {
                SpawnSchoolBooks(data, byId);
                SpawnSchoolPointers(data);
                SpawnSchoolBackpacks(data);
            }

            FitCameraToBoard();
            return spawnedArrows.Count;
        }

        /// <summary>
        /// Orthographic камера: сетка вписывается в BoardPlayArea, иначе — в весь кадр.
        /// </summary>
        public void FitCameraToBoard(Camera cam = null)
        {
            if (BoardCameraFitter.Instance != null)
            {
                BoardCameraFitter.Instance.Fit(this);
                return;
            }

            FitCameraFallback(cam);
        }

        public void GetBoardWorldBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            float cell = cellSize > 0.01f ? cellSize : GameConstants.CellSize;
            minX = float.MaxValue;
            minY = float.MaxValue;
            maxX = float.MinValue;
            maxY = float.MinValue;
            bool any = false;

            if (LastLoaded != null && LastLoaded.arrows != null)
            {
                for (int i = 0; i < LastLoaded.arrows.Length; i++)
                {
                    Vector2Int[] path = LastLoaded.arrows[i].GetPath();
                    for (int c = 0; c < path.Length; c++)
                    {
                        Vector3 world = GridToWorld(path[c]);
                        if (world.x < minX) minX = world.x;
                        if (world.y < minY) minY = world.y;
                        if (world.x > maxX) maxX = world.x;
                        if (world.y > maxY) maxY = world.y;
                        any = true;
                    }
                }

                if (LastLoaded.books != null)
                {
                    for (int i = 0; i < LastLoaded.books.Length; i++)
                    {
                        LevelBookData book = LastLoaded.books[i];
                        if (book == null) continue;
                        Vector3 world = GridToWorld(new Vector2Int(book.x, book.y));
                        if (world.x < minX) minX = world.x;
                        if (world.y < minY) minY = world.y;
                        if (world.x > maxX) maxX = world.x;
                        if (world.y > maxY) maxY = world.y;
                        any = true;
                    }
                }

                if (LastLoaded.pointers != null)
                {
                    for (int i = 0; i < LastLoaded.pointers.Length; i++)
                    {
                        LevelPointerData pointer = LastLoaded.pointers[i];
                        if (pointer == null) continue;
                        Vector2Int[] cells = pointer.GetCells();
                        for (int c = 0; c < cells.Length; c++)
                        {
                            Vector3 world = GridToWorld(cells[c]);
                            if (world.x < minX) minX = world.x;
                            if (world.y < minY) minY = world.y;
                            if (world.x > maxX) maxX = world.x;
                            if (world.y > maxY) maxY = world.y;
                            any = true;
                        }
                    }
                }

                if (LastLoaded.backpacks != null)
                {
                    for (int i = 0; i < LastLoaded.backpacks.Length; i++)
                    {
                        LevelBackpackData backpack = LastLoaded.backpacks[i];
                        if (backpack == null) continue;
                        Vector2Int[] cells = backpack.GetCells();
                        for (int c = 0; c < cells.Length; c++)
                        {
                            Vector3 world = GridToWorld(cells[c]);
                            if (world.x < minX) minX = world.x;
                            if (world.y < minY) minY = world.y;
                            if (world.x > maxX) maxX = world.x;
                            if (world.y > maxY) maxY = world.y;
                            any = true;
                        }
                    }
                }
            }

            if (!any)
            {
                Vector3 a = GridToWorld(Vector2Int.zero);
                Vector3 b = GridToWorld(new Vector2Int(Mathf.Max(0, gridSize.x - 1), Mathf.Max(0, gridSize.y - 1)));
                minX = Mathf.Min(a.x, b.x);
                maxX = Mathf.Max(a.x, b.x);
                minY = Mathf.Min(a.y, b.y);
                maxY = Mathf.Max(a.y, b.y);
            }

            float half = cell * 0.5f;
            minX -= half;
            maxX += half;
            minY -= half;
            maxY += half;

            const float headTailCells = 0.42f;
            float overshoot = cell * headTailCells;
            minX -= overshoot;
            maxX += overshoot;
            minY -= overshoot;
            maxY += overshoot;
        }

        /// <summary>
        /// Старый полный кадр: AABB клеток + вынос головы/хвоста + отступы HUD.
        /// </summary>
        public void FitCameraFallback(Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            GetBoardWorldBounds(out float minX, out float maxX, out float minY, out float maxY);

            const float hudPad = 1.4f;
            minY -= hudPad;
            maxY += hudPad;

            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;
            Vector3 pos = cam.transform.position;
            cam.transform.position = new Vector3(cx, cy, pos.z);

            float width = Mathf.Max(0.01f, maxX - minX);
            float height = Mathf.Max(0.01f, maxY - minY);
            float aspect = Mathf.Max(0.05f, cam.aspect);
            float size = Mathf.Max(height * 0.5f, width * 0.5f / aspect);
            cam.orthographicSize = Mathf.Max(2.5f, size * 1.02f);
        }

        private int BuildTestGrid(int levelIndex)
        {
            LastLoaded = DemoLevel.Create();
            return BuildFromData(LastLoaded);
        }

        public void Clear()
        {
            spawnedArrows.Clear();
            spawnedBooks.Clear();
            spawnedPointers.Clear();
            spawnedBackpacks.Clear();
            LastLoaded = null;
            occupancy?.Clear();
            DestroyChildren(levelRoot);
        }

        /// <summary>Только школьный режим. В Normal книги не спавнятся.</summary>
        private void SpawnSchoolBooks(LevelData data, Dictionary<string, ArrowController> byId)
        {
            spawnedBooks.Clear();
            if (data == null || byId == null) return;

            LevelBookData[] books = SchoolBookResolver.Resolve(data);
            data.books = books;
            if (books == null || books.Length == 0) return;

            var pathCells = new HashSet<long>();
            LevelArrowData[] arrows = data.arrows ?? System.Array.Empty<LevelArrowData>();
            var arrowDataById = new Dictionary<string, LevelArrowData>();
            for (int i = 0; i < arrows.Length; i++)
            {
                LevelArrowData arrowData = arrows[i];
                string arrowId = string.IsNullOrWhiteSpace(arrowData.id) ? $"a{i}" : arrowData.id;
                if (!arrowDataById.ContainsKey(arrowId)) arrowDataById.Add(arrowId, arrowData);
                Vector2Int[] path = arrowData.GetPath();
                for (int c = 0; c < path.Length; c++)
                    pathCells.Add(PackCell(path[c].x, path[c].y));
            }

            CollectPointerCells(data, pathCells);
            CollectBackpackCells(data, pathCells);

            var usedCells = new HashSet<long>();

            for (int i = 0; i < books.Length; i++)
            {
                LevelBookData entry = books[i];
                if (entry == null) continue;
                if (string.IsNullOrEmpty(entry.blockedArrowId) || string.IsNullOrEmpty(entry.keyArrowId)) continue;
                if (!byId.TryGetValue(entry.blockedArrowId, out ArrowController blocked)) continue;
                if (!byId.TryGetValue(entry.keyArrowId, out ArrowController key)) continue;
                if (blocked == key) continue;

                if (!arrowDataById.TryGetValue(entry.blockedArrowId, out LevelArrowData blockedData)
                    || !SchoolBookResolver.KeyIsLockParent(blockedData, entry.keyArrowId))
                {
                    GameLog.Warn(
                        $"[School] Книга {entry.id}: keyArrowId={entry.keyArrowId} не в lockParents {entry.blockedArrowId} — не спавнить.");
                    continue;
                }

                Vector2Int cell = new Vector2Int(entry.x, entry.y);
                if (occupancy != null && !occupancy.InBounds(cell))
                {
                    GameLog.Warn($"[LevelGenerator] Книга {entry.id} вне сетки ({cell.x},{cell.y}).");
                    continue;
                }

                if (!SchoolBookResolver.IsOnCanExitRay(blockedData, cell.x, cell.y, gridSize.x, gridSize.y))
                {
                    GameLog.Warn($"[School] Книга {entry.id} не на луче CanExit {entry.blockedArrowId} — не спавнить.");
                    continue;
                }

                long packed = PackCell(cell.x, cell.y);
                if (pathCells.Contains(packed))
                {
                    GameLog.Warn($"[LevelGenerator] Книга {entry.id} на path стрелки — пропуск.");
                    continue;
                }

                if (!usedCells.Add(packed)) continue;

                string id = string.IsNullOrWhiteSpace(entry.id) ? $"book{i + 1}" : entry.id;
                var go = new GameObject(id);
                go.transform.SetParent(levelRoot, false);
                BookController book = go.AddComponent<BookController>();
                ArtLibrary art = ArtLibrary.Current;
                book.Setup(id, cell, entry.blockedArrowId, entry.keyArrowId, blocked, key, occupancy,
                    art != null ? art.schoolBook : null);
                if (art != null && art.schoolBookIcon != null)
                    key.bookIconSprite = art.schoolBookIcon;

                blocked.BindBlockingBook(book);
                key.BindKeyedBook(book);

                spawnedBooks.Add(book);
                if (occupancy != null) occupancy.RegisterBlocker(book);

                blocked.RefreshLockState();
                key.RefreshLockState();
            }
        }

        /// <summary>Только школьный режим. Нет поля pointers — указок нет.</summary>
        private void SpawnSchoolPointers(LevelData data)
        {
            spawnedPointers.Clear();
            if (data == null || data.pointers == null || data.pointers.Length == 0) return;

            var blocked = new HashSet<long>();
            LevelArrowData[] arrows = data.arrows ?? System.Array.Empty<LevelArrowData>();
            for (int i = 0; i < arrows.Length; i++)
            {
                Vector2Int[] path = arrows[i].GetPath();
                for (int c = 0; c < path.Length; c++)
                    blocked.Add(PackCell(path[c].x, path[c].y));
            }

            for (int i = 0; i < spawnedBooks.Count; i++)
            {
                BookController book = spawnedBooks[i];
                if (book == null || !book.IsAlive) continue;
                blocked.Add(PackCell(book.Cell.x, book.Cell.y));
            }

            CollectBackpackCells(data, blocked);

            for (int i = 0; i < data.pointers.Length; i++)
            {
                LevelPointerData entry = data.pointers[i];
                if (entry == null) continue;

                Vector2Int pivot = new Vector2Int(entry.pivotX, entry.pivotY);
                int length = Mathf.Max(1, entry.length);
                ArrowDirection dir = entry.ParseDirection();
                Vector2Int[] cells = PointerController.BuildCells(pivot, length, dir);
                if (cells == null || cells.Length == 0) continue;

                bool valid = true;
                for (int c = 0; c < cells.Length; c++)
                {
                    Vector2Int cell = cells[c];
                    if (occupancy != null && !occupancy.InBounds(cell))
                    {
                        GameLog.Warn($"[LevelGenerator] Указка {entry.id} вне сетки ({cell.x},{cell.y}).");
                        valid = false;
                        break;
                    }

                    if (blocked.Contains(PackCell(cell.x, cell.y)))
                    {
                        GameLog.Warn($"[LevelGenerator] Указка {entry.id} пересекает стрелку/книгу/указку — пропуск.");
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                for (int c = 0; c < cells.Length; c++)
                    blocked.Add(PackCell(cells[c].x, cells[c].y));

                string id = string.IsNullOrWhiteSpace(entry.id) ? $"pointer{i + 1}" : entry.id;
                var go = new GameObject(id);
                go.transform.SetParent(levelRoot, false);
                PointerController pointer = go.AddComponent<PointerController>();
                ArtLibrary art = ArtLibrary.Current;
                pointer.Setup(id, pivot, length, dir, entry.rotateCW, occupancy,
                    art != null ? art.schoolPointerBody : null,
                    art != null ? art.schoolPointerPivot : null);
                spawnedPointers.Add(pointer);
                if (occupancy != null) occupancy.RegisterBlocker(pointer);
            }
        }

        /// <summary>Только школьный режим. Нет поля backpacks — портфелей нет.</summary>
        private void SpawnSchoolBackpacks(LevelData data)
        {
            spawnedBackpacks.Clear();
            if (data == null || data.backpacks == null || data.backpacks.Length == 0) return;

            var blocked = new HashSet<long>();
            LevelArrowData[] arrows = data.arrows ?? System.Array.Empty<LevelArrowData>();
            for (int i = 0; i < arrows.Length; i++)
            {
                Vector2Int[] path = arrows[i].GetPath();
                for (int c = 0; c < path.Length; c++)
                    blocked.Add(PackCell(path[c].x, path[c].y));
            }

            for (int i = 0; i < spawnedBooks.Count; i++)
            {
                BookController book = spawnedBooks[i];
                if (book == null || !book.IsAlive) continue;
                blocked.Add(PackCell(book.Cell.x, book.Cell.y));
            }

            for (int i = 0; i < spawnedPointers.Count; i++)
            {
                PointerController pointer = spawnedPointers[i];
                if (pointer == null || !pointer.IsAlive) continue;
                IReadOnlyList<Vector2Int> cells = pointer.OccupiedCells;
                if (cells == null) continue;
                for (int c = 0; c < cells.Count; c++)
                    blocked.Add(PackCell(cells[c].x, cells[c].y));
            }

            for (int i = 0; i < data.backpacks.Length; i++)
            {
                LevelBackpackData entry = data.backpacks[i];
                if (entry == null) continue;

                Vector2Int anchor = new Vector2Int(entry.x, entry.y);
                ArrowDirection dir = entry.ParseDirection();
                Vector2Int[] cells = BackpackController.BuildCells(anchor, dir);
                if (cells == null || cells.Length != BackpackController.CellCount) continue;

                bool valid = true;
                for (int c = 0; c < cells.Length; c++)
                {
                    Vector2Int cell = cells[c];
                    if (occupancy != null && !occupancy.InBounds(cell))
                    {
                        GameLog.Warn($"[LevelGenerator] Портфель {entry.id} вне сетки ({cell.x},{cell.y}).");
                        valid = false;
                        break;
                    }

                    if (blocked.Contains(PackCell(cell.x, cell.y)))
                    {
                        GameLog.Warn($"[LevelGenerator] Портфель {entry.id} пересекает стрелку/книгу/указку/портфель — пропуск.");
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                for (int c = 0; c < cells.Length; c++)
                    blocked.Add(PackCell(cells[c].x, cells[c].y));

                string id = string.IsNullOrWhiteSpace(entry.id) ? $"bp{i + 1}" : entry.id;
                var go = new GameObject(id);
                go.transform.SetParent(levelRoot, false);
                BackpackController backpack = go.AddComponent<BackpackController>();
                ArtLibrary art = ArtLibrary.Current;
                backpack.Setup(id, anchor, dir, occupancy, art != null ? art.schoolBackpack : null);
                spawnedBackpacks.Add(backpack);
                if (occupancy != null) occupancy.RegisterBlocker(backpack);
            }
        }

        private static void CollectPointerCells(LevelData data, HashSet<long> dst)
        {
            if (data == null || data.pointers == null || dst == null) return;
            for (int i = 0; i < data.pointers.Length; i++)
            {
                LevelPointerData pointer = data.pointers[i];
                if (pointer == null) continue;
                Vector2Int[] cells = pointer.GetCells();
                for (int c = 0; c < cells.Length; c++)
                    dst.Add(PackCell(cells[c].x, cells[c].y));
            }
        }

        private static void CollectBackpackCells(LevelData data, HashSet<long> dst)
        {
            if (data == null || data.backpacks == null || dst == null) return;
            for (int i = 0; i < data.backpacks.Length; i++)
            {
                LevelBackpackData backpack = data.backpacks[i];
                if (backpack == null) continue;
                Vector2Int[] cells = backpack.GetCells();
                for (int c = 0; c < cells.Length; c++)
                    dst.Add(PackCell(cells[c].x, cells[c].y));
            }
        }

        private static long PackCell(int x, int y) => ((long)x << 32) ^ (uint)y;

        private static void DestroyChildren(Transform root)
        {
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            float x = (gridPos.x - (gridSize.x - 1) * 0.5f) * cellSize + originOffset.x;
            float y = (gridPos.y - (gridSize.y - 1) * 0.5f) * cellSize + originOffset.y;
            return new Vector3(x, y, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.5f, 0.6f, 0.25f);
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    Gizmos.DrawWireCube(GridToWorld(new Vector2Int(x, y)), new Vector3(cellSize, cellSize, 0f));
                }
            }
        }
    }
}
