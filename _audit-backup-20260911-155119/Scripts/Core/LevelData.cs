using System;
using System.Text;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Формат уровня: JSON в Resources/Levels/level_XX.json.
    /// Загрузка: LevelCatalog.Load(index) → LevelGenerator.Build.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id = 1;
        public int gridWidth = 5;
        public int gridHeight = 7;
        public int movesLimit = 8;
        public string[] solution;
        public LevelArrowData[] arrows;
        public LevelButtonData[] buttons;
        /// <summary>Школьные книги. null = нет поля → в школе AutoPlace. [] = авторский «без книг».</summary>
        public LevelBookData[] books;
        /// <summary>Школьные указки. Нет поля / пусто = нет указок. Кампания не использует.</summary>
        public LevelPointerData[] pointers;
        /// <summary>Школьные портфели. Нет поля / пусто = нет портфелей. Кампания не использует.</summary>
        public LevelBackpackData[] backpacks;

        public static LevelData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<LevelData>(json);
        }

        public string ToPrettyJson()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("{");
            sb.AppendLine($"  \"id\": {id},");
            sb.AppendLine($"  \"gridWidth\": {gridWidth},");
            sb.AppendLine($"  \"gridHeight\": {gridHeight},");
            sb.AppendLine($"  \"movesLimit\": {movesLimit},");

            sb.Append("  \"solution\": [");
            if (solution != null)
            {
                for (int i = 0; i < solution.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(solution[i]).Append('"');
                }
            }

            sb.AppendLine("],");
            sb.AppendLine("  \"arrows\": [");

            if (arrows != null)
            {
                for (int i = 0; i < arrows.Length; i++)
                {
                    LevelArrowData a = arrows[i];
                    sb.Append("    { ");
                    sb.Append($"\"id\": \"{a.id}\", \"x\": {a.x}, \"y\": {a.y}, ");
                    sb.Append($"\"dir\": \"{a.dir}\", \"color\": \"{a.color}\"");
                    if (a.locked) sb.Append(", \"locked\": true");

                    if (a.HasPath())
                    {
                        sb.Append(", \"pathX\": [");
                        AppendInts(sb, a.pathX);
                        sb.Append("], \"pathY\": [");
                        AppendInts(sb, a.pathY);
                        sb.Append(']');
                    }

                    string[] parents = a.GetLockParents();
                    if (parents.Length > 0)
                    {
                        sb.Append(", \"lockParents\": [");
                        for (int p = 0; p < parents.Length; p++)
                        {
                            if (p > 0) sb.Append(", ");
                            sb.Append('"').Append(parents[p]).Append('"');
                        }

                        sb.Append(']');
                    }

                    sb.Append(" }");
                    sb.AppendLine(i + 1 < arrows.Length ? "," : "");
                }
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"buttons\": [");

            if (buttons != null)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    LevelButtonData b = buttons[i];
                    sb.Append("    { ");
                    sb.Append($"\"x\": {b.x}, \"y\": {b.y}, \"rotation\": \"{b.rotation}\"");
                    sb.Append($", \"oneShot\": {(b.oneShot ? "true" : "false")}");
                    sb.Append($", \"oncePerArrow\": {(b.oncePerArrow ? "true" : "false")}");
                    if (b.affectOnlyTargetColor)
                    {
                        sb.Append(", \"affectOnlyTargetColor\": true");
                        sb.Append($", \"targetColor\": \"{b.targetColor}\"");
                    }

                    sb.Append(" }");
                    sb.AppendLine(i + 1 < buttons.Length ? "," : "");
                }
            }

            bool hasBooksField = books != null;
            bool hasBookItems = hasBooksField && books.Length > 0;
            bool hasPointers = pointers != null && pointers.Length > 0;
            bool hasBackpacks = backpacks != null && backpacks.Length > 0;
            sb.AppendLine(hasBooksField || hasPointers || hasBackpacks ? "  ]," : "  ]");

            if (hasBooksField)
            {
                sb.AppendLine("  \"books\": [");
                if (hasBookItems)
                {
                    for (int i = 0; i < books.Length; i++)
                    {
                        LevelBookData book = books[i];
                        sb.Append("    { ");
                        sb.Append($"\"id\": \"{book.id}\", \"x\": {book.x}, \"y\": {book.y}");
                        sb.Append($", \"blockedArrowId\": \"{book.blockedArrowId}\"");
                        sb.Append($", \"keyArrowId\": \"{book.keyArrowId}\"");
                        sb.Append(" }");
                        sb.AppendLine(i + 1 < books.Length ? "," : "");
                    }
                }

                sb.AppendLine(hasPointers || hasBackpacks ? "  ]," : "  ]");
            }

            if (hasPointers)
            {
                sb.AppendLine("  \"pointers\": [");
                for (int i = 0; i < pointers.Length; i++)
                {
                    LevelPointerData pointer = pointers[i];
                    sb.Append("    { ");
                    sb.Append($"\"id\": \"{pointer.id}\", \"pivotX\": {pointer.pivotX}, \"pivotY\": {pointer.pivotY}");
                    sb.Append($", \"length\": {pointer.length}, \"dir\": \"{pointer.dir}\"");
                    sb.Append($", \"rotateCW\": {(pointer.rotateCW ? "true" : "false")}");
                    sb.Append(" }");
                    sb.AppendLine(i + 1 < pointers.Length ? "," : "");
                }

                sb.AppendLine(hasBackpacks ? "  ]," : "  ]");
            }

            if (hasBackpacks)
            {
                sb.AppendLine("  \"backpacks\": [");
                for (int i = 0; i < backpacks.Length; i++)
                {
                    LevelBackpackData backpack = backpacks[i];
                    sb.Append("    { ");
                    sb.Append($"\"id\": \"{backpack.id}\", \"x\": {backpack.x}, \"y\": {backpack.y}");
                    sb.Append($", \"dir\": \"{backpack.dir}\"");
                    sb.Append(" }");
                    sb.AppendLine(i + 1 < backpacks.Length ? "," : "");
                }

                sb.AppendLine("  ]");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Сдвигает отрицательные клетки к нулю и расширяет сетку, чтобы все path/x/y были внутри.
        /// </summary>
        public static bool SanitizeBounds(LevelData data)
        {
            if (data == null || data.arrows == null || data.arrows.Length == 0) return false;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData arrow = data.arrows[i];
                Vector2Int[] path = arrow.GetPath();
                if (path == null || path.Length == 0) return false;
                arrow.x = path[0].x;
                arrow.y = path[0].y;
                if (!arrow.HasPath())
                {
                    arrow.pathX = new[] { arrow.x };
                    arrow.pathY = new[] { arrow.y };
                }

                for (int c = 0; c < path.Length; c++)
                {
                    int x = path[c].x;
                    int y = path[c].y;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData book = data.books[i];
                    if (book == null) continue;
                    if (book.x < minX) minX = book.x;
                    if (book.y < minY) minY = book.y;
                    if (book.x > maxX) maxX = book.x;
                    if (book.y > maxY) maxY = book.y;
                }
            }

            if (data.pointers != null)
            {
                for (int i = 0; i < data.pointers.Length; i++)
                {
                    LevelPointerData pointer = data.pointers[i];
                    if (pointer == null) continue;
                    Vector2Int[] cells = pointer.GetCells();
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (cells[c].x < minX) minX = cells[c].x;
                        if (cells[c].y < minY) minY = cells[c].y;
                        if (cells[c].x > maxX) maxX = cells[c].x;
                        if (cells[c].y > maxY) maxY = cells[c].y;
                    }
                }
            }

            if (data.backpacks != null)
            {
                for (int i = 0; i < data.backpacks.Length; i++)
                {
                    LevelBackpackData backpack = data.backpacks[i];
                    if (backpack == null) continue;
                    Vector2Int[] cells = backpack.GetCells();
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (cells[c].x < minX) minX = cells[c].x;
                        if (cells[c].y < minY) minY = cells[c].y;
                        if (cells[c].x > maxX) maxX = cells[c].x;
                        if (cells[c].y > maxY) maxY = cells[c].y;
                    }
                }
            }

            if (minX == int.MaxValue) return false;
            if (maxX - minX > 48 || maxY - minY > 48) return false;

            int dx = minX < 0 ? -minX : 0;
            int dy = minY < 0 ? -minY : 0;
            if (dx != 0 || dy != 0)
            {
                Translate(data, dx, dy);
                maxX += dx;
                maxY += dy;
                minX += dx;
                minY += dy;
            }

            if (minX < 0 || minY < 0) return false;
            if (data.gridWidth < maxX + 1) data.gridWidth = maxX + 1;
            if (data.gridHeight < maxY + 1) data.gridHeight = maxY + 1;

            return LevelSolver.AllCellsInBounds(data);
        }

        private static void Translate(LevelData data, int dx, int dy)
        {
            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData arrow = data.arrows[i];
                arrow.x += dx;
                arrow.y += dy;
                if (arrow.pathX == null || arrow.pathY == null) continue;
                for (int c = 0; c < arrow.pathX.Length; c++)
                {
                    arrow.pathX[c] += dx;
                    if (c < arrow.pathY.Length) arrow.pathY[c] += dy;
                }
            }

            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData book = data.books[i];
                    if (book == null) continue;
                    book.x += dx;
                    book.y += dy;
                }
            }

            if (data.pointers != null)
            {
                for (int i = 0; i < data.pointers.Length; i++)
                {
                    LevelPointerData pointer = data.pointers[i];
                    if (pointer == null) continue;
                    pointer.pivotX += dx;
                    pointer.pivotY += dy;
                }
            }

            if (data.backpacks == null) return;
            for (int i = 0; i < data.backpacks.Length; i++)
            {
                LevelBackpackData backpack = data.backpacks[i];
                if (backpack == null) continue;
                backpack.x += dx;
                backpack.y += dy;
            }
        }

        private static void AppendInts(StringBuilder sb, int[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(values[i]);
            }
        }
    }

    [Serializable]
    public class LevelArrowData
    {
        public string id;
        public int x;
        public int y;
        public string dir = "Up";
        public string color = "Grey";
        public bool locked;
        public string[] lockParents;

        /// <summary>Клетки фигуры по X. Пусто = одна клетка (x, y) — совместимость со старыми JSON.</summary>
        public int[] pathX;

        /// <summary>Клетки фигуры по Y. Длина должна совпадать с pathX.</summary>
        public int[] pathY;

        public string[] GetLockParents() => lockParents ?? Array.Empty<string>();

        public bool HasPath()
        {
            return pathX != null && pathY != null && pathX.Length > 0 && pathX.Length == pathY.Length;
        }

        public Vector2Int[] GetPath()
        {
            if (HasPath())
            {
                var path = new Vector2Int[pathX.Length];
                for (int i = 0; i < pathX.Length; i++)
                    path[i] = new Vector2Int(pathX[i], pathY[i]);
                return path;
            }

            return new[] { new Vector2Int(x, y) };
        }

        public ArrowDirection ParseDirection()
        {
            return Enum.TryParse(dir, true, out ArrowDirection parsed) ? parsed : ArrowDirection.Up;
        }

        public ArrowColor ParseColor()
        {
            return Enum.TryParse(color, true, out ArrowColor parsed) ? parsed : ArrowColor.Grey;
        }
    }

    [Serializable]
    public class LevelButtonData
    {
        public int x;
        public int y;
        public string rotation = "Rotate90CW";
        public bool oneShot;
        public bool oncePerArrow = true;
        public bool affectOnlyTargetColor;
        public string targetColor = "Red";

        public SwitchRotation ParseRotation()
        {
            return Enum.TryParse(rotation, true, out SwitchRotation parsed) ? parsed : SwitchRotation.Rotate90CW;
        }

        public ArrowColor ParseTargetColor()
        {
            return Enum.TryParse(targetColor, true, out ArrowColor parsed) ? parsed : ArrowColor.Red;
        }
    }

    /// <summary>Большая книга на клетке. Ключ — стрелка с иконкой книжки.</summary>
    [Serializable]
    public class LevelBookData
    {
        public string id;
        public int x;
        public int y;
        public string blockedArrowId;
        public string keyArrowId;
    }

    /// <summary>Линейная указка. length включает пивот. Нет поля pointers в JSON — указок нет.</summary>
    [Serializable]
    public class LevelPointerData
    {
        public string id;
        public int pivotX;
        public int pivotY;
        public int length = 3;
        public string dir = "Right";
        public bool rotateCW;

        public ArrowDirection ParseDirection()
        {
            return Enum.TryParse(dir, true, out ArrowDirection parsed) ? parsed : ArrowDirection.Right;
        }

        public Vector2Int[] GetCells()
        {
            return PointerController.BuildCells(new Vector2Int(pivotX, pivotY), length, ParseDirection());
        }
    }

    /// <summary>Портфель на двух соседних клетках. x,y — первая; dir — ось и вторая клетка.</summary>
    [Serializable]
    public class LevelBackpackData
    {
        public string id;
        public int x;
        public int y;
        public string dir = "Right";

        public ArrowDirection ParseDirection()
        {
            return Enum.TryParse(dir, true, out ArrowDirection parsed) ? parsed : ArrowDirection.Right;
        }

        public Vector2Int[] GetCells()
        {
            return BackpackController.BuildCells(new Vector2Int(x, y), ParseDirection());
        }
    }

    /// <summary>Каталог JSON-уровней в Resources/Levels/.</summary>
    public static class LevelCatalog
    {
        public const int TargetCount = 100;
        public const string PrefsKey = "Unpuzzle.CurrentLevel";

        private static int cachedCount = -1;

        public static string FileStem(int id) => $"level_{id:D2}";

        public static string ResourcePath(int id) => $"{GameConstants.LevelsResourcesPath}/{FileStem(id)}";

        public static int Count
        {
            get
            {
                if (cachedCount >= 0) return cachedCount;

                int found = 0;
                for (int i = 1; i <= TargetCount + 16; i++)
                {
                    if (Resources.Load<TextAsset>(ResourcePath(i)) == null) break;
                    found = i;
                }

                cachedCount = found > 0 ? found : TargetCount;
                return cachedCount;
            }
        }

        public static void InvalidateCount() => cachedCount = -1;

        public static LevelData Load(int id)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath(id));
            if (asset == null) return null;

            LevelData data = LevelData.FromJson(asset.text);
            if (data != null && data.id <= 0) data.id = id;
            if (data != null && !LevelData.SanitizeBounds(data)) return null;
            return data;
        }
    }
}
