using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Unpuzzle
{
    public static partial class LevelFactory
    {
        public static LevelData GenerateSchool(int schoolId) => GenerateSchool(schoolId, 0, out _);

        /// <summary>Школьный JSON: клубки как поздняя кампания + книги / указка / портфель по номеру.</summary>
        public static LevelData GenerateSchool(int schoolId, int seed, out bool usedFallback)
        {
            schoolId = Clamp(schoolId, 1, SchoolLevelCatalog.TargetCount);
            if (seed == 0) seed = schoolId * 11003 + 9176;
            DifficultySpec spec = DifficultySpec.School(schoolId);

            int waves = schoolId >= 14 ? 10 : 8;
            int attempts = schoolId >= 14 ? 48 : 240;
            for (int wave = 0; wave < waves; wave++)
            {
                var rng = new Random(seed + wave * 13007);
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    LevelData data = TryGenerateSchool(schoolId, spec, rng);
                    if (data == null) continue;
                    usedFallback = false;
                    return data;
                }
            }

            usedFallback = true;
            return CreateSchoolFallback(schoolId, spec, seed);
        }

        private static LevelData TryGenerateSchool(int schoolId, DifficultySpec spec, Random rng)
        {
            int width = spec.Width;
            int height = spec.Height;
            int extra = spec.MaxArrows - spec.MinArrows;
            int target = spec.MinArrows + (extra <= 0 ? 0 : rng.Next(0, Math.Min(extra, 4) + 1));

            List<List<Cell>> geometry = BuildSchoolTangle(width, height, target, spec, rng);
            if (geometry == null || geometry.Count < spec.MinArrows) return null;
            if (!SchoolTangleFillsSpec(geometry, spec)) return null;
            if (HasOutOfBounds(geometry, width, height)) return null;

            List<GenArrow> solution = PeelSolution(geometry, width, height);
            if (solution == null || solution.Count < spec.MinArrows) return null;

            ApplySchoolLocks(solution, spec, rng, width, height);
            ApplyColors(solution, spec, rng);

            LevelData data = ToLevelData(schoolId, spec, solution, new List<GenButton>());
            data.buttons = Array.Empty<LevelButtonData>();
            if (!LevelData.SanitizeBounds(data)) return null;
            if (data.gridWidth > width || data.gridHeight > height) return null;
            if (!SchoolLevelSolver.IsSolvable(data)) return null;

            return DecorateSchool(data, schoolId, spec, rng) ? data : null;
        }

        private static bool SchoolTangleFillsSpec(List<List<Cell>> paths, DifficultySpec spec)
        {
            if (paths == null || spec == null || !IsCompactTangle(paths)) return false;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int cells = 0;
            for (int p = 0; p < paths.Count; p++)
            {
                List<Cell> path = paths[p];
                cells += path.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Cell c = path[i];
                    if (c.x < minX) minX = c.x;
                    if (c.y < minY) minY = c.y;
                    if (c.x > maxX) maxX = c.x;
                    if (c.y > maxY) maxY = c.y;
                }
            }

            int spanW = maxX - minX + 1;
            int spanH = maxY - minY + 1;
            if (spanW < spec.Width - 1 || spanH < spec.Height - 1) return false;

            int specArea = Math.Max(1, spec.Width * spec.Height);
            if (cells * 100 < specArea * 70) return false;
            return paths.Count >= spec.MinArrows;
        }

        private static List<List<Cell>> BuildSchoolTangle(int width, int height, int target, DifficultySpec spec, Random rng)
        {
            Region region = new Region(0, 0, width - 1, height - 1);
            var paths = new List<List<Cell>>(target);
            var occupied = new HashSet<long>();

            int roll = rng.Next(100);
            if (roll < 38)
                ComposeNestedHooks(paths, occupied, region, target, spec, rng);
            else if (roll < 68)
                ComposeSpineAndNests(paths, occupied, region, target, spec, rng);
            else if (roll < 88)
                ComposeSpiralBands(paths, occupied, region, target, spec, rng);
            else
                ComposeGapLanes(paths, occupied, region, target, spec, rng);

            FillCorridors(paths, occupied, region, width, height, target, spec, rng);
            if (paths.Count < spec.MinArrows)
                FillCorridors(paths, occupied, region, width, height, spec.MaxArrows, spec, rng);
            return paths.Count >= spec.MinArrows ? paths : null;
        }

        private static void ApplySchoolLocks(List<GenArrow> solution, DifficultySpec spec, Random rng, int width, int height)
        {
            ApplyLocks(solution, spec, rng);
            if (solution == null || solution.Count < 4) return;

            var occupied = new HashSet<long>();
            for (int i = 0; i < solution.Count; i++) Occupy(occupied, solution[i].path);

            int locked = 0;
            for (int i = 0; i < solution.Count; i++)
            {
                if (solution[i].lockParents.Count > 0) locked++;
            }

            int want = spec.PickLockCount(rng, solution.Count);
            if (locked >= want) return;

            var childOrder = new List<int>(solution.Count - 1);
            for (int i = 1; i < solution.Count; i++) childOrder.Add(i);
            Shuffle(childOrder, rng);

            for (int n = 0; n < childOrder.Count && locked < want; n++)
            {
                int childIndex = childOrder[n];
                GenArrow child = solution[childIndex];
                if (child.lockParents.Count > 0) continue;
                if (!RayHasEmpty(child, occupied, width, height)) continue;

                int parentIndex = rng.Next(0, childIndex);
                child.lockParents.Clear();
                child.lockParents.Add(solution[parentIndex].id);
                locked++;
            }

            EnsureMinFree(solution, Math.Max(2, spec.MinFreeArrows));
        }

        private static bool RayHasEmpty(GenArrow arrow, HashSet<long> occupied, int width, int height)
        {
            if (arrow == null || arrow.path == null || arrow.path.Count == 0) return false;
            Step(arrow.dir, out int dx, out int dy);
            Cell tip = arrow.path[arrow.path.Count - 1];
            int cx = tip.x + dx;
            int cy = tip.y + dy;
            while (cx >= 0 && cy >= 0 && cx < width && cy < height)
            {
                if (!occupied.Contains(Pack(cx, cy))) return true;
                cx += dx;
                cy += dy;
            }

            return false;
        }

        public static bool TryDecorateSchool(LevelData data, int schoolId, int seed)
        {
            if (data == null) return false;
            var rng = new Random(seed == 0 ? schoolId * 13001 + 77 : seed);
            return DecorateSchool(data, schoolId, DifficultySpec.School(schoolId), rng);
        }

        internal static bool DecorateSchool(LevelData data, int schoolId, DifficultySpec spec, Random rng)
        {
            if (data == null || rng == null) return false;

            DensifyJsonLocks(data, spec, rng);
            data.books = SchoolBookResolver.AutoPlace(data);
            int minBooks = schoolId <= 3 ? 3 : schoolId <= 12 ? 4 : 5;
            if (data.books == null || data.books.Length < minBooks) return false;
            if (LevelSolver.CountStartFree(data) < 2) return false;

            bool wantPointer = schoolId >= 4 && schoolId <= 7 || schoolId >= 13;
            bool optionalPointer = schoolId == 11 || schoolId == 12;
            bool wantBackpack = schoolId >= 8;
            string[] peelOrder = data.solution;

            if (wantPointer || optionalPointer)
            {
                List<LevelPointerData> pointerCands = CollectPointerCandidates(data);
                Shuffle(pointerCands, rng);
                int pTake = Math.Min(pointerCands.Count, wantPointer ? 4 : 3);
                for (int p = 0; p < pTake; p++)
                {
                    data.pointers = new[] { pointerCands[p] };
                    data.solution = peelOrder;
                    if (TryFinishSchool(data, schoolId, spec, rng, wantBackpack)) return true;
                }

                data.pointers = null;
                data.solution = peelOrder;
                if (optionalPointer && TryFinishSchool(data, schoolId, spec, rng, wantBackpack)) return true;
                if (wantPointer && !optionalPointer) return false;
            }
            else
            {
                data.pointers = null;
            }

            data.solution = peelOrder;
            return TryFinishSchool(data, schoolId, spec, rng, wantBackpack);
        }

        private static void DensifyJsonLocks(LevelData data, DifficultySpec spec, Random rng)
        {
            if (data == null || data.arrows == null || spec == null || rng == null) return;
            LevelArrowData[] arrows = data.arrows;
            if (arrows.Length < 4) return;

            int locked = 0;
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i].GetLockParents().Length > 0) locked++;
            }

            int want = spec.PickLockCount(rng, arrows.Length);
            if (locked >= want) return;

            var childOrder = new List<int>(arrows.Length - 1);
            for (int i = 1; i < arrows.Length; i++) childOrder.Add(i);
            Shuffle(childOrder, rng);

            for (int n = 0; n < childOrder.Count && locked < want; n++)
            {
                int childIndex = childOrder[n];
                LevelArrowData child = arrows[childIndex];
                if (child.GetLockParents().Length > 0) continue;

                int parentIndex = rng.Next(0, childIndex);
                string parentId = arrows[parentIndex].id;
                if (string.IsNullOrEmpty(parentId) || parentId == child.id) continue;
                child.lockParents = new[] { parentId };
                locked++;
            }

            int free = 0;
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i].GetLockParents().Length == 0) free++;
            }

            int minFree = Math.Max(2, spec.MinFreeArrows);
            for (int i = arrows.Length - 1; i >= 0 && free < minFree; i--)
            {
                if (arrows[i].GetLockParents().Length == 0) continue;
                arrows[i].lockParents = Array.Empty<string>();
                free++;
            }
        }

        private static bool TryFinishSchool(LevelData data, int schoolId, DifficultySpec spec, Random rng, bool wantBackpack)
        {
            string[] peelOrder = data.solution;
            LevelPointerData[] pointers = data.pointers;

            if (wantBackpack)
            {
                List<LevelBackpackData> backpackCands = CollectBackpackCandidates(data);
                Shuffle(backpackCands, rng);
                int take = Math.Min(backpackCands.Count, 4);
                for (int i = 0; i < take; i++)
                {
                    data.backpacks = new[] { backpackCands[i] };
                    data.pointers = pointers;
                    data.solution = peelOrder;
                    if (CommitSchoolSolve(data, schoolId, spec, wantBackpack)) return true;
                }

                data.backpacks = null;
                data.solution = peelOrder;
                return false;
            }

            data.backpacks = null;
            return CommitSchoolSolve(data, schoolId, spec, false);
        }

        private static bool CommitSchoolSolve(LevelData data, int schoolId, DifficultySpec spec, bool wantBackpack)
        {
            if (!LevelData.SanitizeBounds(data)) return false;
            if (!LevelSolver.TrySolveSchool(data, out string[] tokens)) return false;
            data.solution = tokens;
            data.movesLimit = Math.Max(1, data.arrows.Length + spec.PickMoveSlack(data.arrows.Length));
            if (!SchoolLevelSolver.IsSolvable(data)) return false;
            if (!LevelSolver.ValidateSchoolShape(data, schoolId, out _)) return false;
            if (wantBackpack && !LevelSolver.BackpackSlideRequired(data)) return false;
            return true;
        }

        private static List<LevelPointerData> CollectPointerCandidates(LevelData data)
        {
            var result = new List<LevelPointerData>();
            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            var used = CollectStaticCells(data, false, false);

            var rayCells = new HashSet<long>();
            CollectExitRays(data, rayCells, skipFirst: 2);

            var dirs = new[] { ArrowDirection.Right, ArrowDirection.Up, ArrowDirection.Left, ArrowDirection.Down };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (used.Contains(Pack(x, y))) continue;
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        for (int len = 3; len <= 5; len++)
                        {
                            Vector2Int[] cells = PointerController.BuildCells(new Vector2Int(x, y), len, dirs[d]);
                            if (cells == null || cells.Length != len) continue;
                            bool ok = true;
                            bool hitsRay = false;
                            for (int c = 0; c < cells.Length; c++)
                            {
                                Vector2Int cell = cells[c];
                                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                                {
                                    ok = false;
                                    break;
                                }

                                if (used.Contains(Pack(cell.x, cell.y)))
                                {
                                    ok = false;
                                    break;
                                }

                                if (rayCells.Contains(Pack(cell.x, cell.y))) hitsRay = true;
                            }

                            if (!ok) continue;
                            result.Add(new LevelPointerData
                            {
                                id = "p1",
                                pivotX = x,
                                pivotY = y,
                                length = len,
                                dir = dirs[d].ToString(),
                                rotateCW = (x + y + len + d) % 2 == 0
                            });

                            if (hitsRay)
                            {
                                LevelPointerData last = result[result.Count - 1];
                                result.RemoveAt(result.Count - 1);
                                result.Insert(0, last);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static List<LevelBackpackData> CollectBackpackCandidates(LevelData data)
        {
            var result = new List<LevelBackpackData>();
            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            var used = CollectStaticCells(data, true, false);

            var rayCells = new HashSet<long>();
            CollectExitRays(data, rayCells, skipFirst: 2);

            var dirs = new[] { ArrowDirection.Right, ArrowDirection.Up, ArrowDirection.Left, ArrowDirection.Down };
            foreach (long packed in rayCells)
            {
                int rx = (int)(packed >> 32);
                int ry = (int)(uint)packed;
                if (used.Contains(Pack(rx, ry))) continue;

                for (int d = 0; d < dirs.Length; d++)
                {
                    ArrowDirection dir = dirs[d];
                    Vector2Int[] cells = BackpackController.BuildCells(new Vector2Int(rx, ry), dir);
                    if (cells == null || cells.Length != BackpackController.CellCount) continue;
                    bool ok = true;
                    int onRay = 0;
                    for (int c = 0; c < cells.Length; c++)
                    {
                        Vector2Int cell = cells[c];
                        if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                        {
                            ok = false;
                            break;
                        }

                        if (used.Contains(Pack(cell.x, cell.y)))
                        {
                            ok = false;
                            break;
                        }

                        if (rayCells.Contains(Pack(cell.x, cell.y))) onRay++;
                    }

                    if (!ok || onRay == 0) continue;

                    result.Add(new LevelBackpackData
                    {
                        id = "bp1",
                        x = rx,
                        y = ry,
                        dir = dir.ToString()
                    });
                }
            }

            return result;
        }

        private static HashSet<long> CollectStaticCells(LevelData data, bool includePointers, bool includeBackpacks)
        {
            var used = new HashSet<long>();
            if (data.arrows != null)
            {
                for (int i = 0; i < data.arrows.Length; i++)
                {
                    Vector2Int[] path = data.arrows[i].GetPath();
                    for (int c = 0; c < path.Length; c++)
                        used.Add(Pack(path[c].x, path[c].y));
                }
            }

            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData book = data.books[i];
                    if (book == null) continue;
                    used.Add(Pack(book.x, book.y));
                }
            }

            if (includePointers && data.pointers != null)
            {
                for (int i = 0; i < data.pointers.Length; i++)
                {
                    LevelPointerData pointer = data.pointers[i];
                    if (pointer == null) continue;
                    Vector2Int[] cells = pointer.GetCells();
                    for (int c = 0; c < cells.Length; c++)
                        used.Add(Pack(cells[c].x, cells[c].y));
                }
            }

            if (includeBackpacks && data.backpacks != null)
            {
                for (int i = 0; i < data.backpacks.Length; i++)
                {
                    LevelBackpackData backpack = data.backpacks[i];
                    if (backpack == null) continue;
                    Vector2Int[] cells = backpack.GetCells();
                    for (int c = 0; c < cells.Length; c++)
                        used.Add(Pack(cells[c].x, cells[c].y));
                }
            }

            return used;
        }

        private static void CollectExitRays(LevelData data, HashSet<long> dst, int skipFirst)
        {
            if (data.arrows == null) return;
            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            var pathCells = new HashSet<long>();
            for (int i = 0; i < data.arrows.Length; i++)
            {
                Vector2Int[] path = data.arrows[i].GetPath();
                for (int c = 0; c < path.Length; c++)
                    pathCells.Add(Pack(path[c].x, path[c].y));
            }

            int start = Math.Max(0, skipFirst);
            for (int i = start; i < data.arrows.Length; i++)
            {
                LevelArrowData arrow = data.arrows[i];
                Vector2Int[] path = arrow.GetPath();
                if (path == null || path.Length == 0) continue;
                Vector2Int step = ArrowController.DirectionToCellStep(arrow.ParseDirection());
                Vector2Int cursor = path[path.Length - 1] + step;
                while (cursor.x >= 0 && cursor.y >= 0 && cursor.x < width && cursor.y < height)
                {
                    long key = Pack(cursor.x, cursor.y);
                    if (!pathCells.Contains(key)) dst.Add(key);
                    cursor += step;
                }
            }
        }

        private static LevelData CreateSchoolFallback(int schoolId, DifficultySpec spec, int seed)
        {
            for (int extra = 1; extra <= 48; extra++)
            {
                var extraRng = new Random(seed + extra * 9973);
                LevelData retry = TryGenerateSchool(schoolId, spec, extraRng);
                if (retry != null) return retry;
            }

            return null;
        }
    }

    public static partial class LevelSolver
    {
        public static int CountStartFree(LevelData data)
        {
            List<SimArrow> arrows;
            Dictionary<string, SimArrow> byId;
            List<SimBook> books;
            List<SimPointer> pointers;
            List<SimBackpack> backpacks;
            if (!TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks, true))
                return 0;
            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            return CollectLegal(arrows, byId, books, pointers, backpacks, width, height).Count;
        }

        public static bool TrySolveSchool(LevelData data, out string[] tokens)
        {
            tokens = null;
            List<SimArrow> arrows;
            Dictionary<string, SimArrow> byId;
            List<SimBook> books;
            List<SimPointer> pointers;
            List<SimBackpack> backpacks;
            if (!TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks, true))
                return false;

            string[] preferred = PreferredArrowOrder(data, arrows, backpacks);
            var path = new List<string>(arrows.Count + 8);
            var visited = new HashSet<string>();
            int nodes = 0;
            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            if (!SearchSchool(arrows, byId, books, pointers, backpacks, width, height, preferred, path, visited, ref nodes))
                return false;

            tokens = path.ToArray();
            return true;
        }

        public static bool BackpackSlideRequired(LevelData data)
        {
            if (data == null || data.backpacks == null || data.backpacks.Length == 0) return false;
            if (data.solution == null) return false;

            bool hasSlide = false;
            var arrowOnly = new List<string>(data.solution.Length);
            for (int i = 0; i < data.solution.Length; i++)
            {
                string token = data.solution[i];
                if (!string.IsNullOrEmpty(token) && token.IndexOf(':') >= 0)
                {
                    hasSlide = true;
                    continue;
                }

                arrowOnly.Add(token);
            }

            if (!hasSlide) return false;

            LevelData clone = LevelData.FromJson(data.ToPrettyJson());
            if (clone == null) return false;
            clone.solution = arrowOnly.ToArray();
            return !IsSolvable(clone);
        }

        public static bool ValidateSchoolShape(LevelData data, int schoolId, out string error)
        {
            error = null;
            if (data == null || data.arrows == null || data.arrows.Length == 0)
            {
                error = "нет стрелок";
                return false;
            }

            if (!LevelData.SanitizeBounds(data))
            {
                error = "SanitizeBounds";
                return false;
            }

            LevelFactory.DifficultySpec spec = LevelFactory.DifficultySpec.School(schoolId);
            if (data.gridWidth < spec.Width || data.gridHeight < spec.Height)
            {
                error = "сетка меньше School spec";
                return false;
            }

            if (data.arrows.Length < spec.MinArrows)
            {
                error = "мало стрелок";
                return false;
            }

            if (data.buttons != null && data.buttons.Length > 0)
            {
                error = "кнопки запрещены";
                return false;
            }

            if (!SchoolLevelSolver.IsSolvable(data))
            {
                error = "не solvable";
                return false;
            }

            if (CountStartFree(data) < 2)
            {
                error = "меньше 2 свободных";
                return false;
            }

            int books = data.books != null ? data.books.Length : 0;
            int pointers = data.pointers != null ? data.pointers.Length : 0;
            int backpacks = data.backpacks != null ? data.backpacks.Length : 0;

            if (schoolId <= 3)
            {
                if (books < 3) { error = "1–3: мало книг"; return false; }
                if (pointers != 0 || backpacks != 0) { error = "1–3: лишняя указка/портфель"; return false; }
            }
            else if (schoolId <= 7)
            {
                if (books < 3) { error = "4–7: мало книг"; return false; }
                if (pointers != 1) { error = "4–7: нужна одна указка"; return false; }
                if (backpacks != 0) { error = "4–7: портфель не нужен"; return false; }
            }
            else if (schoolId <= 10)
            {
                if (books < 3) { error = "8–10: мало книг"; return false; }
                if (pointers != 0) { error = "8–10: без указки"; return false; }
                if (backpacks < 1) { error = "8–10: нужен портфель"; return false; }
            }
            else if (schoolId <= 12)
            {
                if (books < 3) { error = "11–12: мало книг"; return false; }
                if (backpacks < 1) { error = "11–12: нужен портфель"; return false; }
            }
            else
            {
                if (books < 3) { error = "13–20: мало книг"; return false; }
                if (pointers < 1) { error = "13–20: нужна указка"; return false; }
                if (backpacks < 1) { error = "13–20: нужен портфель"; return false; }
                if (!BackpackSlideRequired(data)) { error = "13–20: портфель не мешает"; return false; }
            }

            if (schoolId >= 8 && schoolId <= 12 && !BackpackSlideRequired(data))
            {
                error = "портфель декорация";
                return false;
            }

            if (data.solution != null)
            {
                int slides = 0;
                for (int i = 0; i < data.solution.Length; i++)
                {
                    if (!string.IsNullOrEmpty(data.solution[i]) && data.solution[i].IndexOf(':') >= 0)
                        slides++;
                }

                if (slides > 8)
                {
                    error = "слишком много сдвигов";
                    return false;
                }
            }

            return true;
        }

        private static string[] PreferredArrowOrder(LevelData data, List<SimArrow> arrows, List<SimBackpack> backpacks)
        {
            if (data.solution != null && CountArrowTokens(data.solution, backpacks) == arrows.Count)
            {
                var ids = new List<string>(arrows.Count);
                for (int i = 0; i < data.solution.Length; i++)
                {
                    if (TryParseBackpackToken(data.solution[i], backpacks, out _, out _)) continue;
                    ids.Add(data.solution[i]);
                }

                return ids.ToArray();
            }

            var order = new string[arrows.Count];
            for (int i = 0; i < arrows.Count; i++) order[i] = arrows[i].id;
            return order;
        }

        private static bool SearchSchool(
            List<SimArrow> arrows,
            Dictionary<string, SimArrow> byId,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            int width,
            int height,
            string[] preferred,
            List<string> tokens,
            HashSet<string> visited,
            ref int nodes)
        {
            if (arrows.Count == 0) return true;
            if (++nodes > 50000) return false;

            string key = SchoolStateKey(arrows, pointers, backpacks);
            if (!visited.Add(key)) return false;

            List<SimArrow> legal = CollectLegal(arrows, byId, books, pointers, backpacks, width, height);
            SimArrow peelNext = null;
            for (int i = 0; i < preferred.Length; i++)
            {
                if (!byId.TryGetValue(preferred[i], out SimArrow arrow)) continue;
                peelNext = arrow;
                break;
            }

            if (peelNext != null && legal.Contains(peelNext))
            {
                if (PlayArrowSearch(peelNext, arrows, byId, books, pointers, backpacks, width, height, preferred, tokens, visited, ref nodes))
                    return true;
            }

            for (int i = 0; i < legal.Count; i++)
            {
                SimArrow arrow = legal[i];
                if (arrow == peelNext) continue;
                if (PlayArrowSearch(arrow, arrows, byId, books, pointers, backpacks, width, height, preferred, tokens, visited, ref nodes))
                    return true;
            }

            int slidesSoFar = 0;
            for (int t = 0; t < tokens.Count; t++)
            {
                if (tokens[t] != null && tokens[t].IndexOf(':') >= 0) slidesSoFar++;
            }

            if (legal.Count == 0 && backpacks != null && slidesSoFar < 8)
            {
                for (int b = 0; b < backpacks.Count; b++)
                {
                    SimBackpack pack = backpacks[b];
                    for (int s = 0; s < 2; s++)
                    {
                        bool plus = s == 0;
                        Vector2Int oldAnchor = pack.anchor;
                        Vector2Int[] oldCells = pack.cells;
                        if (!TrySlideBackpack(pack, plus, arrows, books, pointers, backpacks, width, height))
                            continue;

                        tokens.Add(pack.id + (plus ? ":+" : ":-"));
                        if (SearchSchool(arrows, byId, books, pointers, backpacks, width, height, preferred, tokens, visited, ref nodes))
                            return true;
                        tokens.RemoveAt(tokens.Count - 1);
                        pack.anchor = oldAnchor;
                        pack.cells = oldCells;
                    }
                }
            }

            return false;
        }

        private static bool PlayArrowSearch(
            SimArrow arrow,
            List<SimArrow> arrows,
            Dictionary<string, SimArrow> byId,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            int width,
            int height,
            string[] preferred,
            List<string> tokens,
            HashSet<string> visited,
            ref int nodes)
        {
            PointerPose[] poses = CapturePointers(pointers);
            arrows.Remove(arrow);
            byId.Remove(arrow.id);
            TryRotatePointers(pointers, arrows, books, backpacks, width, height);
            tokens.Add(arrow.id);
            if (SearchSchool(arrows, byId, books, pointers, backpacks, width, height, preferred, tokens, visited, ref nodes))
                return true;

            tokens.RemoveAt(tokens.Count - 1);
            RestorePointers(pointers, poses);
            arrows.Add(arrow);
            byId[arrow.id] = arrow;
            return false;
        }

        private static string SchoolStateKey(List<SimArrow> arrows, List<SimPointer> pointers, List<SimBackpack> backpacks)
        {
            var ids = new List<string>(arrows.Count);
            for (int i = 0; i < arrows.Count; i++) ids.Add(arrows[i].id);
            ids.Sort(StringComparer.Ordinal);
            var sb = new System.Text.StringBuilder(ids.Count * 8 + 32);
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ids[i]);
            }

            sb.Append('|');
            if (pointers != null)
            {
                for (int p = 0; p < pointers.Count; p++)
                {
                    SimPointer pointer = pointers[p];
                    sb.Append((int)pointer.dir).Append(',');
                }
            }

            sb.Append('|');
            if (backpacks != null)
            {
                for (int b = 0; b < backpacks.Count; b++)
                {
                    SimBackpack pack = backpacks[b];
                    sb.Append(pack.anchor.x).Append(',').Append(pack.anchor.y).Append(',').Append((int)pack.dir).Append(';');
                }
            }

            return sb.ToString();
        }

        private struct PointerPose
        {
            public ArrowDirection dir;
            public Vector2Int[] cells;
        }

        private static PointerPose[] CapturePointers(List<SimPointer> pointers)
        {
            if (pointers == null || pointers.Count == 0) return Array.Empty<PointerPose>();
            var poses = new PointerPose[pointers.Count];
            for (int i = 0; i < pointers.Count; i++)
            {
                SimPointer pointer = pointers[i];
                poses[i] = new PointerPose
                {
                    dir = pointer.dir,
                    cells = pointer.cells
                };
            }

            return poses;
        }

        private static void RestorePointers(List<SimPointer> pointers, PointerPose[] poses)
        {
            if (pointers == null || poses == null) return;
            int n = Math.Min(pointers.Count, poses.Length);
            for (int i = 0; i < n; i++)
            {
                pointers[i].dir = poses[i].dir;
                pointers[i].cells = poses[i].cells;
            }
        }
    }
}
