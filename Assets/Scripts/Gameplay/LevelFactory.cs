using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Unpuzzle
{
    /// <summary>
    /// Процедурная генерация клубка: длинные ортогональные L/U/зигзаги вплотную
    /// в соседних клетках, минимум дыр в bounding box. Геометрия сначала
    /// (каркас, затем коридоры), затем ориентация кончиков и порядок снятия.
    /// </summary>
    public static partial class LevelFactory
    {
        private const int CompactFillPercent = 72;

        public static LevelData Generate(int levelId) => Generate(levelId, 0, out _);

        public static LevelData Generate(int levelId, int seed, out bool usedFallback)
        {
            if (levelId == 1)
            {
                usedFallback = false;
                return DemoLevel.Create();
            }

            if (seed == 0) seed = levelId * 7919 + 42;
            DifficultySpec spec = DifficultySpec.For(levelId);

            for (int wave = 0; wave < 4; wave++)
            {
                var rng = new Random(seed + wave * 104729);
                int attempts = spec.MinArrows >= 12 ? 380 : 260;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    LevelData data = TryGenerate(levelId, spec, rng, true);
                    if (data == null) continue;

                    usedFallback = false;
                    return data;
                }
            }

            for (int wave = 0; wave < 2; wave++)
            {
                var rng = new Random(seed + 7771 + wave * 13007);
                for (int attempt = 0; attempt < 180; attempt++)
                {
                    LevelData data = TryGenerate(levelId, spec, rng, false);
                    if (data == null) continue;

                    usedFallback = false;
                    return data;
                }
            }

            usedFallback = true;
            return CreateFallback(levelId, spec);
        }

        private static LevelData TryGenerate(int levelId, DifficultySpec spec, Random rng, bool strictPuzzle)
        {
            int width = spec.Width;
            int height = spec.Height;
            int target = spec.PickArrowCount(rng);

            List<List<Cell>> geometry = BuildTangle(width, height, target, spec, rng);
            if (geometry == null || geometry.Count < spec.MinArrows) return null;
            if (!IsCompactTangle(geometry)) return null;
            if (HasOutOfBounds(geometry, width, height)) return null;

            List<GenArrow> solution = PeelSolution(geometry, width, height);
            if (solution == null || solution.Count < spec.MinArrows) return null;

            ApplyLocks(solution, spec, rng);
            ApplyColors(solution, spec, rng);
            var occupied = new HashSet<long>();
            for (int i = 0; i < solution.Count; i++) Occupy(occupied, solution[i].path);
            List<GenButton> buttons = PlaceButtons(occupied, width, height, spec, rng);

            LevelData data = ToLevelData(levelId, spec, solution, buttons);
            if (!LevelData.SanitizeBounds(data)) return null;
            if (data.gridWidth > width || data.gridHeight > height) return null;
            if (!LevelSolver.IsSolvable(data))
            {
                data.buttons = Array.Empty<LevelButtonData>();
                if (!LevelSolver.IsSolvable(data)) return null;
            }

            return LevelSolver.PassesPuzzleFilter(data, rng, strictPuzzle) ? data : null;
        }

        // ----------------------------------------------------------- tangle

        private static List<List<Cell>> BuildTangle(int width, int height, int target, DifficultySpec spec, Random rng)
        {
            Region region = PickRegion(width, height, target, spec, rng);
            var paths = new List<List<Cell>>(target);
            var occupied = new HashSet<long>();

            int roll = rng.Next(100);
            if (target >= 10)
            {
                if (roll < 38)
                    ComposeNestedHooks(paths, occupied, region, target, spec, rng);
                else if (roll < 68)
                    ComposeSpineAndNests(paths, occupied, region, target, spec, rng);
                else if (roll < 88)
                    ComposeSpiralBands(paths, occupied, region, target, spec, rng);
                else
                    ComposeGapLanes(paths, occupied, region, target, spec, rng);
            }
            else if (roll < 42)
                ComposeNestedHooks(paths, occupied, region, target, spec, rng);
            else if (roll < 78)
                ComposeSpineAndNests(paths, occupied, region, target, spec, rng);
            else
                ComposeSpiralBands(paths, occupied, region, target, spec, rng);

            FillCorridors(paths, occupied, region, width, height, target, spec, rng);
            if (paths.Count < spec.MaxArrows && !IsCompactTangle(paths))
                FillCorridors(paths, occupied, region, width, height, spec.MaxArrows, spec, rng);

            return paths.Count >= spec.MinArrows ? paths : null;
        }

        private static Region PickRegion(int width, int height, int target, DifficultySpec spec, Random rng)
        {
            int span = Math.Max(0, spec.MaxPathLen - spec.MinPathLen);
            int lo = spec.MinPathLen + span * 2 / 3;
            int genAvg = Math.Max(spec.MinPathLen, (lo + spec.MaxPathLen + 1) / 2);
            int expectedCells = target * genAvg;
            int wantArea = Clamp(
                (expectedCells * 100 + CompactFillPercent - 1) / CompactFillPercent,
                expectedCells + 1,
                width * height);

            int minW = Math.Min(width, target <= 4 ? 4 : target <= 8 ? 5 : 6);
            int minH = Math.Min(height, target <= 4 ? 4 : target <= 8 ? 6 : 7);

            int rw = Clamp((int)Math.Round(Math.Sqrt(wantArea * width / (double)Math.Max(1, height))), minW, width);
            int rh = Clamp((wantArea + rw - 1) / rw, minH, height);
            while (rw * rh > wantArea && (rw > minW || rh > minH))
            {
                bool shrinkW = rw >= rh && rw > minW;
                int nextArea = shrinkW ? (rw - 1) * rh : rw * (rh - 1);
                if (nextArea < expectedCells) break;
                if (shrinkW) rw--;
                else if (rh > minH) rh--;
                else if (rw > minW) rw--;
                else break;
            }

            while (rw * rh < expectedCells && (rw < width || rh < height))
            {
                if (rh <= rw && rh < height) rh++;
                else if (rw < width) rw++;
                else if (rh < height) rh++;
                else break;
            }

            rw = Clamp(rw, 1, width);
            rh = Clamp(rh, 1, height);
            int x0 = (width - rw) / 2;
            int y0 = (height - rh) / 2;
            if (width - rw > 0 && rng.NextDouble() < 0.3) x0 = rng.Next(0, width - rw + 1);
            if (height - rh > 0 && rng.NextDouble() < 0.3) y0 = rng.Next(0, height - rh + 1);
            return new Region(x0, y0, x0 + rw - 1, y0 + rh - 1);
        }

        /// <summary>Каркас: длинная вертикаль/горизонталь на краю, шапка, затем вложенные L/U/зигзаги в кармане.</summary>
        private static void ComposeSpineAndNests(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int target, DifficultySpec spec, Random rng)
        {
            bool vertical = rng.Next(2) == 0;
            bool highSide = rng.Next(2) == 0;
            List<Cell> spine = MakeEdgeSpine(region, vertical, highSide, spec, rng);
            if (!TryCommit(paths, occupied, spine, region)) return;

            Region pocket = ShrinkFromSpine(region, vertical, highSide);
            List<Cell> cap = MakeCap(pocket, vertical, highSide, spec, rng);
            TryCommit(paths, occupied, cap, region);

            for (int inset = 1; inset <= 8 && paths.Count < target; inset++)
            {
                List<Cell> inner = MakeInnerLane(pocket, vertical, highSide, inset, spec, rng);
                if (!TryCommit(paths, occupied, inner, region)) break;
            }

            while (paths.Count < target)
            {
                if (TryPlaceMotif(paths, occupied, pocket, region, spec, rng)) continue;
                break;
            }
        }

        /// <summary>Несколько U/C одного раскрытия вплотную (inset +1).</summary>
        private static void ComposeNestedHooks(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int target, DifficultySpec spec, Random rng)
        {
            ArrowDirection open = (ArrowDirection)rng.Next(4);
            for (int inset = 0; inset <= 8 && paths.Count < target; inset++)
            {
                List<Cell> hook = MakeNestedHook(region, inset, open, spec, rng);
                if (!TryCommit(paths, occupied, hook, region)) break;
            }

            while (paths.Count < target)
            {
                if (TryPlaceMotif(paths, occupied, region, region, spec, rng)) continue;
                break;
            }
        }

        /// <summary>Кольца спирали вплотную, каждое кольцо — своя ломаная, не нарезка одной нити.</summary>
        private static void ComposeSpiralBands(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int target, DifficultySpec spec, Random rng)
        {
            ArrowDirection open = (ArrowDirection)rng.Next(4);
            for (int inset = 0; inset <= 8 && paths.Count < target; inset++)
            {
                List<Cell> band = MakeFrameBand(region, inset, open, spec, rng);
                if (!TryCommit(paths, occupied, band, region)) break;
            }

            while (paths.Count < target)
            {
                if (TryPlaceMotif(paths, occupied, region, region, spec, rng)) continue;
                break;
            }
        }

        /// <summary>Плотный клубок: соседние дорожки вплотную, крюки в остатке.</summary>
        private static void ComposeGapLanes(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int target, DifficultySpec spec, Random rng)
        {
            bool vertical = rng.Next(2) == 0;
            int gap = 1;
            if (vertical)
            {
                int xStart = region.x0;
                for (int x = xStart; x <= region.x1 && paths.Count < target; x += gap)
                    StackLanes(paths, occupied, region, spec, rng, target, x, true);
            }
            else
            {
                int yStart = region.y0;
                for (int y = yStart; y <= region.y1 && paths.Count < target; y += gap)
                    StackLanes(paths, occupied, region, spec, rng, target, y, false);
            }

            while (paths.Count < target)
            {
                if (TryPlaceMotif(paths, occupied, region, region, spec, rng)) continue;
                break;
            }
        }

        private static void StackLanes(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            DifficultySpec spec, Random rng, int target, int axis, bool vertical)
        {
            int cursor = vertical ? region.y0 : region.x0;
            int limit = vertical ? region.y1 : region.x1;
            int stall = 0;
            while (cursor <= limit && paths.Count < target && stall < 8)
            {
                int remaining = limit - cursor + 1;
                if (remaining < spec.MinPathLen) break;

                int len = Math.Min(PickLongPathLen(spec, rng), remaining);
                List<Cell> path;
                if (vertical)
                    path = MakeLane(new Cell(axis, cursor + len - 1), ArrowDirection.Down, len, spec, rng, region);
                else
                    path = MakeLane(new Cell(cursor, axis), ArrowDirection.Right, len, spec, rng, region);

                if (TryCommit(paths, occupied, path, region))
                {
                    cursor += len;
                    stall = 0;
                }
                else
                {
                    cursor++;
                    stall++;
                }
            }
        }

        private static List<Cell> MakeLane(Cell start, ArrowDirection along, int len, DifficultySpec spec, Random rng, Region region)
        {
            if (spec.MaxTurns < 1 || rng.Next(8) == 0)
                return Walk(start, along, len);

            int arm = rng.Next(1, Math.Max(2, Math.Min(3, len - spec.MinPathLen + 2)));
            int run = len - arm;
            if (run < 2) return Walk(start, along, len);

            ArrowDirection toward = rng.Next(2) == 0 ? TurnCw(along) : TurnCcw(along);
            List<Cell> hooked = MakeL(start, along, run, toward, arm);
            if (hooked == null) return Walk(start, along, len);
            for (int i = 0; i < hooked.Count; i++)
            {
                if (!region.Contains(hooked[i])) return Walk(start, along, len);
            }

            return hooked;
        }

        private static void FillCorridors(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int width, int height, int target, DifficultySpec spec, Random rng)
        {
            int fail = 0;
            int failLimit = Math.Max(96, target * 22);
            while (paths.Count < target && fail < failLimit)
            {
                if (TryPlaceMotif(paths, occupied, region, region, spec, rng)
                    || TryGrowCorridor(paths, occupied, region, width, height, spec, rng))
                {
                    fail = 0;
                    continue;
                }

                fail++;
            }
        }

        private static List<Cell> MakeEdgeSpine(Region r, bool vertical, bool highSide, DifficultySpec spec, Random rng)
        {
            int len = PickLongPathLen(spec, rng);
            if (vertical)
            {
                int x = highSide ? r.x1 : r.x0;
                int maxLen = r.Height;
                len = Math.Min(len, maxLen);
                if (len < spec.MinPathLen) return null;
                int yTop = r.y1 - rng.Next(0, Math.Max(1, r.Height - len + 1));
                return Walk(new Cell(x, yTop), ArrowDirection.Down, len);
            }

            int y = highSide ? r.y1 : r.y0;
            len = Math.Min(len, r.Width);
            if (len < spec.MinPathLen) return null;
            int xLeft = r.x0 + rng.Next(0, Math.Max(1, r.Width - len + 1));
            return Walk(new Cell(xLeft, y), ArrowDirection.Right, len);
        }

        private static Region ShrinkFromSpine(Region r, bool vertical, bool highSide)
        {
            if (vertical)
            {
                if (highSide) return new Region(r.x0, r.y0, Math.Max(r.x0, r.x1 - 1), r.y1);
                return new Region(Math.Min(r.x1, r.x0 + 1), r.y0, r.x1, r.y1);
            }

            if (highSide) return new Region(r.x0, r.y0, r.x1, Math.Max(r.y0, r.y1 - 1));
            return new Region(r.x0, Math.Min(r.y1, r.y0 + 1), r.x1, r.y1);
        }

        private static List<Cell> MakeInnerLane(
            Region pocket, bool spineVertical, bool highSide, int inset, DifficultySpec spec, Random rng)
        {
            int len = PickLongPathLen(spec, rng);
            if (spineVertical)
            {
                int x = highSide ? pocket.x1 - inset : pocket.x0 + inset;
                if (x < pocket.x0 || x > pocket.x1) return null;
                int yStart = pocket.y1 - Math.Min(inset / 2, 1);
                int run = Math.Min(len - 1, Math.Max(2, yStart - pocket.y0 - 1));
                if (run < 2) return null;
                int arm = Math.Max(1, len - run);
                ArrowDirection along = ArrowDirection.Down;
                ArrowDirection toward = highSide ? ArrowDirection.Left : ArrowDirection.Right;
                if (rng.Next(3) == 0)
                    return Walk(new Cell(x, yStart), along, Math.Min(len, yStart - pocket.y0 + 1));
                return MakeL(new Cell(x, yStart), along, run, toward, arm);
            }

            int y = highSide ? pocket.y1 - inset : pocket.y0 + inset;
            if (y < pocket.y0 || y > pocket.y1) return null;
            int xStart = pocket.x0 + Math.Min(inset / 2, 1);
            int runH = Math.Min(len - 1, Math.Max(2, pocket.x1 - xStart - 1));
            if (runH < 2) return null;
            int armH = Math.Max(1, len - runH);
            ArrowDirection alongH = ArrowDirection.Right;
            ArrowDirection towardV = highSide ? ArrowDirection.Down : ArrowDirection.Up;
            if (rng.Next(3) == 0)
                return Walk(new Cell(xStart, y), alongH, Math.Min(len, pocket.x1 - xStart + 1));
            return MakeL(new Cell(xStart, y), alongH, runH, towardV, armH);
        }

        private static List<Cell> MakeCap(Region pocket, bool spineVertical, bool highSide, DifficultySpec spec, Random rng)
        {
            int len = PickLongPathLen(spec, rng);
            if (spineVertical)
            {
                int y = rng.Next(2) == 0 ? pocket.y1 : pocket.y0;
                ArrowDirection along = highSide ? ArrowDirection.Left : ArrowDirection.Right;
                int xStart = highSide ? pocket.x1 : pocket.x0;
                int hook = rng.Next(2, Math.Max(3, len - 1));
                hook = Math.Min(hook, len - 1);
                ArrowDirection inward = y == pocket.y1 ? ArrowDirection.Down : ArrowDirection.Up;
                if (rng.Next(3) == 0)
                    return Walk(new Cell(xStart, y), along, len);
                return MakeL(new Cell(xStart, y), inward, hook, along, len - hook);
            }

            int x = rng.Next(2) == 0 ? pocket.x1 : pocket.x0;
            ArrowDirection alongV = highSide ? ArrowDirection.Down : ArrowDirection.Up;
            int yStart = highSide ? pocket.y1 : pocket.y0;
            int arm = rng.Next(2, Math.Max(3, len - 1));
            arm = Math.Min(arm, len - 1);
            ArrowDirection inwardH = x == pocket.x1 ? ArrowDirection.Left : ArrowDirection.Right;
            if (rng.Next(3) == 0)
                return Walk(new Cell(x, yStart), alongV, len);
            return MakeL(new Cell(x, yStart), inwardH, arm, alongV, len - arm);
        }

        private static List<Cell> MakeNestedHook(Region r, int inset, ArrowDirection open, DifficultySpec spec, Random rng)
        {
            int x0 = r.x0 + inset;
            int y0 = r.y0 + inset;
            int x1 = r.x1 - inset;
            int y1 = r.y1 - inset;
            if (x1 - x0 < 2 || y1 - y0 < 2) return null;

            int len = PickLongPathLen(spec, rng);
            int a, b, c;
            SplitThree(len, spec.MaxTurns >= 2, rng, out a, out b, out c);

            switch (open)
            {
                case ArrowDirection.Right:
                    return MakeU(new Cell(x0 + a - 1, y0), ArrowDirection.Left, a, ArrowDirection.Up, b, ArrowDirection.Right, c);
                case ArrowDirection.Left:
                    return MakeU(new Cell(x1 - a + 1, y0), ArrowDirection.Right, a, ArrowDirection.Up, b, ArrowDirection.Left, c);
                case ArrowDirection.Up:
                    return MakeU(new Cell(x0, y0 + a - 1), ArrowDirection.Down, a, ArrowDirection.Right, b, ArrowDirection.Up, c);
                default:
                    return MakeU(new Cell(x0, y1 - a + 1), ArrowDirection.Up, a, ArrowDirection.Right, b, ArrowDirection.Down, c);
            }
        }

        private static List<Cell> MakeFrameBand(Region r, int inset, ArrowDirection open, DifficultySpec spec, Random rng)
        {
            int x0 = r.x0 + inset;
            int y0 = r.y0 + inset;
            int x1 = r.x1 - inset;
            int y1 = r.y1 - inset;
            if (x1 - x0 < 2 || y1 - y0 < 2) return null;

            int len = PickLongPathLen(spec, rng);
            var full = new List<Cell>(32);
            switch (open)
            {
                case ArrowDirection.Right:
                    AppendWalk(full, new Cell(x1, y0), ArrowDirection.Left, x1 - x0 + 1);
                    AppendWalk(full, new Cell(x0, y0 + 1), ArrowDirection.Up, y1 - y0);
                    AppendWalk(full, new Cell(x0 + 1, y1), ArrowDirection.Right, x1 - x0);
                    break;
                case ArrowDirection.Left:
                    AppendWalk(full, new Cell(x0, y0), ArrowDirection.Right, x1 - x0 + 1);
                    AppendWalk(full, new Cell(x1, y0 + 1), ArrowDirection.Up, y1 - y0);
                    AppendWalk(full, new Cell(x1 - 1, y1), ArrowDirection.Left, x1 - x0);
                    break;
                case ArrowDirection.Up:
                    AppendWalk(full, new Cell(x0, y1), ArrowDirection.Down, y1 - y0 + 1);
                    AppendWalk(full, new Cell(x0 + 1, y0), ArrowDirection.Right, x1 - x0);
                    AppendWalk(full, new Cell(x1, y0 + 1), ArrowDirection.Up, y1 - y0);
                    break;
                default:
                    AppendWalk(full, new Cell(x0, y0), ArrowDirection.Up, y1 - y0 + 1);
                    AppendWalk(full, new Cell(x0 + 1, y1), ArrowDirection.Right, x1 - x0);
                    AppendWalk(full, new Cell(x1, y1 - 1), ArrowDirection.Down, y1 - y0);
                    break;
            }

            return TakeInterestingSubpath(full, spec.MinPathLen, len);
        }

        private static bool TryPlaceMotif(
            List<List<Cell>> paths, HashSet<long> occupied, Region pocket, Region bounds,
            DifficultySpec spec, Random rng)
        {
            int len = PickLongPathLen(spec, rng);
            int kind = rng.Next(100);
            List<Cell> best = null;
            int bestScore = int.MinValue;
            Region spawn = occupied.Count == 0
                ? pocket
                : ExpandClamp(OccupiedBounds(occupied, pocket), 1, pocket);

            for (int trial = 0; trial < 56; trial++)
            {
                List<Cell> motif;
                if (kind < 36)
                    motif = RandomL(spawn, len, rng);
                else if (kind < 68)
                    motif = RandomU(spawn, len, spec, rng);
                else if (kind < 94)
                    motif = RandomZigzag(spawn, len, spec, rng);
                else if (kind < 98 && spec.MaxTurns >= 3)
                    motif = RandomFrame(spawn, len, rng);
                else
                    motif = RandomStraight(spawn, len, rng);

                if (!IsValidPolyline(motif, spec.MinPathLen, spec.MaxPathLen)) continue;
                if (Overlaps(motif, occupied)) continue;
                if (occupied.Count > 0 && !TouchesOccupied(motif, occupied)) continue;

                int score = ScoreNest(motif, occupied, bounds, paths.Count == 0);
                score += rng.Next(0, 5);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = motif;
                }
            }

            int need = occupied.Count == 0 ? 0 : (paths.Count >= 8 ? 10 : 14);
            if (best == null || bestScore < need) return false;
            return TryCommit(paths, occupied, best, bounds);
        }

        private static bool TryGrowCorridor(
            List<List<Cell>> paths, HashSet<long> occupied, Region region,
            int width, int height, DifficultySpec spec, Random rng)
        {
            var seeds = new List<Cell>(32);
            CollectGrowSeeds(seeds, occupied, region, paths.Count == 0);
            if (seeds.Count == 0) return false;
            Shuffle(seeds, rng);

            int targetLen = PickLongPathLen(spec, rng);
            int take = Math.Min(seeds.Count, 18);
            for (int s = 0; s < take; s++)
            {
                List<Cell> grown = GrowPath(seeds[s], occupied, region, width, height, targetLen, spec, rng);
                if (TryCommit(paths, occupied, grown, region)) return true;
            }

            return false;
        }

        private static void CollectGrowSeeds(List<Cell> dst, HashSet<long> occupied, Region region, bool first)
        {
            dst.Clear();
            Region box = occupied.Count == 0 || first ? region : OccupiedBounds(occupied, region);
            box = ExpandClamp(box, 1, region);

            for (int y = box.y0; y <= box.y1; y++)
            {
                for (int x = box.x0; x <= box.x1; x++)
                {
                    if (occupied.Contains(Pack(x, y))) continue;
                    if (!first && !TouchesOccupiedAt(x, y, occupied)) continue;
                    dst.Add(new Cell(x, y));
                }
            }

            if (dst.Count == 0)
            {
                for (int y = region.y0; y <= region.y1; y++)
                {
                    for (int x = region.x0; x <= region.x1; x++)
                    {
                        if (!occupied.Contains(Pack(x, y))) dst.Add(new Cell(x, y));
                    }
                }
            }
        }

        private static List<Cell> GrowPath(
            Cell start, HashSet<long> occupied, Region region, int width, int height,
            int targetLen, DifficultySpec spec, Random rng)
        {
            if (occupied.Contains(Pack(start.x, start.y))) return null;

            var path = new List<Cell>(targetLen) { start };
            var used = new HashSet<long>(occupied) { Pack(start.x, start.y) };
            ArrowDirection heading = PickHeading(start, used, region, rng);
            int turns = 0;

            while (path.Count < targetLen)
            {
                Cell last = path[path.Count - 1];
                var options = new List<GrowOpt>(3);

                ConsiderStep(options, last, heading, heading, used, occupied, region, width, height, 0);
                if (turns < spec.MaxTurns)
                {
                    ConsiderStep(options, last, TurnCw(heading), heading, used, occupied, region, width, height, 1);
                    ConsiderStep(options, last, TurnCcw(heading), heading, used, occupied, region, width, height, 1);
                }

                if (options.Count == 0) break;

                GrowOpt pick = WeightedPick(options, rng);
                if (pick.dir != heading) turns++;
                heading = pick.dir;
                path.Add(pick.cell);
                used.Add(Pack(pick.cell.x, pick.cell.y));
            }

            return path.Count >= spec.MinPathLen ? path : null;
        }

        private static void ConsiderStep(
            List<GrowOpt> options, Cell last, ArrowDirection dir, ArrowDirection heading,
            HashSet<long> used, HashSet<long> occupied, Region region,
            int width, int height, int turnCost)
        {
            Cell next = Offset(last, dir);
            if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height) return;
            if (!region.Contains(next)) return;
            if (used.Contains(Pack(next.x, next.y))) return;

            int score = dir == heading ? 8 : 7;
            score -= turnCost;
            score += AdjacentFlushBonus(last, next, occupied);
            score += CountOccupiedNeighbors(next.x, next.y, occupied, 1) * 4;
            options.Add(new GrowOpt(next, dir, Math.Max(1, score)));
        }

        private static GrowOpt WeightedPick(List<GrowOpt> options, Random rng)
        {
            int total = 0;
            for (int i = 0; i < options.Count; i++) total += options[i].score;
            int pick = rng.Next(total);
            for (int i = 0; i < options.Count; i++)
            {
                pick -= options[i].score;
                if (pick < 0) return options[i];
            }

            return options[options.Count - 1];
        }

        private static ArrowDirection PickHeading(Cell start, HashSet<long> used, Region region, Random rng)
        {
            var dirs = new[] { ArrowDirection.Up, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Left };
            Shuffle(dirs, rng);
            ArrowDirection best = dirs[0];
            int bestRun = -1;
            for (int i = 0; i < dirs.Length; i++)
            {
                int run = 0;
                Cell c = Offset(start, dirs[i]);
                while (region.Contains(c) && !used.Contains(Pack(c.x, c.y)) && run < 12)
                {
                    run++;
                    c = Offset(c, dirs[i]);
                }

                int bonus = AdjacentFlushBonus(start, Offset(start, dirs[i]), used);
                if (run * 3 + bonus > bestRun)
                {
                    bestRun = run * 3 + bonus;
                    best = dirs[i];
                }
            }

            return best;
        }

        private static List<Cell> RandomStraight(Region r, int len, Random rng)
        {
            var dirs = new[] { ArrowDirection.Up, ArrowDirection.Right, ArrowDirection.Down, ArrowDirection.Left };
            ArrowDirection dir = dirs[rng.Next(4)];
            Cell start = RandomCell(r, rng);
            return Walk(start, dir, len);
        }

        private static List<Cell> RandomL(Region r, int len, Random rng)
        {
            if (len < 3) return null;
            int a = rng.Next(2, len);
            int b = len - a;
            if (b < 1) return null;
            ArrowDirection first = (ArrowDirection)rng.Next(4);
            ArrowDirection second = rng.Next(2) == 0 ? TurnCw(first) : TurnCcw(first);
            return MakeL(RandomCell(r, rng), first, a, second, b);
        }

        private static List<Cell> RandomU(Region r, int len, DifficultySpec spec, Random rng)
        {
            if (len < 4 || spec.MaxTurns < 2) return RandomL(r, len, rng);
            int a, b, c;
            SplitThree(len, true, rng, out a, out b, out c);
            ArrowDirection first = (ArrowDirection)rng.Next(4);
            ArrowDirection turn = rng.Next(2) == 0 ? TurnCw(first) : TurnCcw(first);
            ArrowDirection third = rng.Next(2) == 0 ? TurnCw(turn) : TurnCcw(turn);
            if (third == first) third = Opposite(first);
            return MakeU(RandomCell(r, rng), first, a, turn, b, third, c);
        }

        private static List<Cell> RandomZigzag(Region r, int len, DifficultySpec spec, Random rng)
        {
            if (len < 4 || spec.MaxTurns < 2) return RandomL(r, len, rng);
            int a, b, c;
            SplitThree(len, true, rng, out a, out b, out c);
            ArrowDirection first = (ArrowDirection)rng.Next(4);
            ArrowDirection mid = rng.Next(2) == 0 ? TurnCw(first) : TurnCcw(first);
            ArrowDirection third = first;
            return MakeU(RandomCell(r, rng), first, a, mid, b, third, c);
        }

        private static List<Cell> RandomFrame(Region r, int len, Random rng)
        {
            if (len < 6) return RandomL(r, len, rng);
            int a = Math.Max(2, len / 4);
            int b = Math.Max(1, (len - a) / 3);
            int c = Math.Max(1, (len - a - b) / 2);
            int d = len - a - b - c;
            if (d < 1) return null;
            ArrowDirection d0 = (ArrowDirection)rng.Next(4);
            ArrowDirection d1 = TurnCw(d0);
            return TraceDirs(RandomCell(r, rng), new[] { d0, d1, Opposite(d0), Opposite(d1) }, new[] { a, b, c, d });
        }

        private static List<Cell> MakeL(Cell start, ArrowDirection first, int a, ArrowDirection second, int b)
        {
            if (a < 1 || b < 1) return null;
            var path = new List<Cell>(a + b);
            AppendWalk(path, start, first, a);
            if (path.Count == 0) return null;
            Cell corner = path[path.Count - 1];
            AppendWalk(path, Offset(corner, second), second, b);
            return path;
        }

        private static List<Cell> MakeU(
            Cell start, ArrowDirection d0, int a, ArrowDirection d1, int b, ArrowDirection d2, int c)
        {
            return TraceDirs(start, new[] { d0, d1, d2 }, new[] { a, b, c });
        }

        private static List<Cell> TraceDirs(Cell start, ArrowDirection[] dirs, int[] lengths)
        {
            var path = new List<Cell>(16);
            Cell cursor = start;
            for (int s = 0; s < dirs.Length; s++)
            {
                int n = lengths[s];
                if (s == 0)
                {
                    AppendWalk(path, cursor, dirs[s], n);
                }
                else
                {
                    if (path.Count == 0) return null;
                    cursor = Offset(path[path.Count - 1], dirs[s]);
                    AppendWalk(path, cursor, dirs[s], n);
                }
            }

            return path;
        }

        private static List<Cell> Walk(Cell start, ArrowDirection dir, int len)
        {
            var path = new List<Cell>(len);
            AppendWalk(path, start, dir, len);
            return path;
        }

        private static void AppendWalk(List<Cell> path, Cell start, ArrowDirection dir, int count)
        {
            Cell c = start;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) c = Offset(c, dir);
                if (path.Count > 0)
                {
                    Cell last = path[path.Count - 1];
                    if (last.x == c.x && last.y == c.y) continue;
                }

                path.Add(c);
            }
        }

        private static List<Cell> TakeInterestingSubpath(List<Cell> full, int minLen, int wantLen)
        {
            if (full == null || full.Count < minLen) return null;
            DedupAdjacent(full);
            wantLen = Math.Min(wantLen, full.Count);
            if (wantLen < minLen) return null;

            int bestStart = 0;
            int bestTurns = -1;
            for (int s = 0; s + wantLen <= full.Count; s++)
            {
                int turns = CountTurnsRange(full, s, wantLen);
                if (turns > bestTurns)
                {
                    bestTurns = turns;
                    bestStart = s;
                }
            }

            return full.GetRange(bestStart, wantLen);
        }

        private static void SplitThree(int len, bool preferU, Random rng, out int a, out int b, out int c)
        {
            a = Math.Max(1, len / 3);
            c = Math.Max(1, len / 3);
            b = len - a - c;
            if (b < 1)
            {
                b = 1;
                a = Math.Max(1, (len - 1) / 2);
                c = len - a - b;
            }

            if (preferU && rng.Next(2) == 0)
            {
                int t = a;
                a = c;
                c = t;
            }

            if (rng.Next(3) == 0 && a > 1 && c + 1 < len)
            {
                a--;
                c++;
            }
        }

        // ------------------------------------------------------- commit / peel

        private static bool TryCommit(List<List<Cell>> paths, HashSet<long> occupied, List<Cell> path, Region bounds)
        {
            if (!IsValidPolyline(path, 3, 24)) return false;
            if (Overlaps(path, occupied)) return false;

            int inside = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (!bounds.Contains(path[i])) return false;
                inside++;
            }

            if (inside != path.Count) return false;

            paths.Add(ClonePath(path));
            Occupy(occupied, path);
            return true;
        }

        private static List<GenArrow> PeelSolution(List<List<Cell>> geometry, int width, int height)
        {
            int n = geometry.Count;
            var occupied = new HashSet<long>();
            for (int i = 0; i < n; i++) Occupy(occupied, geometry[i]);

            var remaining = new bool[n];
            for (int i = 0; i < n; i++) remaining[i] = true;

            var oriented = new List<Cell>[n];
            var dirs = new ArrowDirection[n];
            var order = new List<int>(n);
            int nodes = 0;

            if (!PeelDfs(geometry, remaining, n, occupied, width, height, order, oriented, dirs, 0, ref nodes))
                return null;

            var solution = new List<GenArrow>(n);
            for (int i = 0; i < order.Count; i++)
            {
                int idx = order[i];
                List<Cell> path = oriented[idx];
                solution.Add(new GenArrow
                {
                    id = $"a{i}",
                    x = path[0].x,
                    y = path[0].y,
                    dir = dirs[idx],
                    color = ArrowColor.Grey,
                    path = path
                });
            }

            return solution;
        }

        private static bool PeelDfs(
            List<List<Cell>> geometry, bool[] remaining, int n, HashSet<long> occupied,
            int width, int height, List<int> order, List<Cell>[] oriented, ArrowDirection[] dirs,
            int depth, ref int nodes)
        {
            if (order.Count == n) return true;
            if (++nodes > 12000) return false;

            var cands = new List<PeelCand>(n * 2);
            for (int idx = 0; idx < n; idx++)
            {
                if (!remaining[idx]) continue;
                for (int orient = 0; orient < 2; orient++)
                {
                    List<Cell> path = orient == 0 ? geometry[idx] : Reversed(geometry[idx]);
                    if (!TryTipDir(path, out ArrowDirection dir)) continue;
                    if (!PathCanExit(path, dir, occupied, width, height)) continue;
                    cands.Add(new PeelCand(idx, ExitScore(path, dir, width, height), path, dir));
                }
            }

            if (cands.Count == 0) return false;

            cands.Sort((a, b) => b.score.CompareTo(a.score));
            int tryMax = cands.Count;
            if (n >= 14) tryMax = Math.Min(cands.Count, depth < 3 ? 5 : 3);
            else if (n >= 8) tryMax = Math.Min(cands.Count, 6);

            int seen = 0;
            for (int t = 0; t < cands.Count && seen < tryMax; t++)
            {
                PeelCand cand = cands[t];
                if (!remaining[cand.idx]) continue;
                seen++;

                remaining[cand.idx] = false;
                Free(occupied, geometry[cand.idx]);
                order.Add(cand.idx);
                oriented[cand.idx] = cand.path;
                dirs[cand.idx] = cand.dir;

                if (PeelDfs(geometry, remaining, n, occupied, width, height, order, oriented, dirs, depth + 1, ref nodes))
                    return true;

                order.RemoveAt(order.Count - 1);
                Occupy(occupied, geometry[cand.idx]);
                remaining[cand.idx] = true;
            }

            return false;
        }

        private static int ExitScore(List<Cell> path, ArrowDirection dir, int width, int height)
        {
            Step(dir, out int dx, out int dy);
            int score = 0;
            Cell tip = path[path.Count - 1];
            int dist = 0;
            int cx = tip.x;
            int cy = tip.y;
            while (cx >= 0 && cy >= 0 && cx < width && cy < height)
            {
                dist++;
                cx += dx;
                cy += dy;
            }

            score += Math.Max(0, 12 - dist);
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].x == 0 || path[i].y == 0 || path[i].x == width - 1 || path[i].y == height - 1)
                    score += 2;
            }

            return score;
        }

        private static bool PathCanExit(List<Cell> path, ArrowDirection dir, HashSet<long> occupied, int width, int height)
        {
            if (path == null || path.Count == 0) return false;

            var self = new HashSet<long>(path.Count);
            for (int i = 0; i < path.Count; i++) self.Add(Pack(path[i].x, path[i].y));

            Step(dir, out int dx, out int dy);
            Cell tip = path[path.Count - 1];
            int cx = tip.x + dx;
            int cy = tip.y + dy;
            while (cx >= 0 && cy >= 0 && cx < width && cy < height)
            {
                long key = Pack(cx, cy);
                if (occupied.Contains(key) && !self.Contains(key)) return false;
                cx += dx;
                cy += dy;
            }

            return true;
        }

        private static bool IsCompactTangle(List<List<Cell>> paths)
        {
            if (paths == null || paths.Count == 0) return false;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int cells = 0;
            var occ = new HashSet<long>();
            for (int p = 0; p < paths.Count; p++)
            {
                List<Cell> path = paths[p];
                if (path.Count < 3) return false;
                cells += path.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Cell c = path[i];
                    if (!occ.Add(Pack(c.x, c.y))) return false;
                    if (c.x < minX) minX = c.x;
                    if (c.y < minY) minY = c.y;
                    if (c.x > maxX) maxX = c.x;
                    if (c.y > maxY) maxY = c.y;
                }
            }

            int area = (maxX - minX + 1) * (maxY - minY + 1);
            if (area <= 0) return false;
            if (cells * 100 < area * CompactFillPercent) return false;
            return true;
        }

        // ----------------------------------------------------- locks / colors

        private static void ApplyLocks(List<GenArrow> solution, DifficultySpec spec, Random rng)
        {
            if (!spec.Locks || solution.Count < 4) return;

            int lockCount = spec.PickLockCount(rng, solution.Count);
            if (lockCount <= 0) return;

            var childOrder = new List<int>(solution.Count - 1);
            for (int i = 1; i < solution.Count; i++) childOrder.Add(i);
            Shuffle(childOrder, rng);

            int placed = 0;
            for (int n = 0; n < childOrder.Count && placed < lockCount; n++)
            {
                int childIndex = childOrder[n];
                GenArrow child = solution[childIndex];
                if (child.lockParents.Count > 0) continue;

                int parentIndex = rng.Next(0, childIndex);
                if (parentIndex == childIndex - 1 && childIndex >= 2 && rng.NextDouble() < 0.75)
                    parentIndex = rng.Next(0, childIndex - 1);

                child.lockParents.Clear();
                child.lockParents.Add(solution[parentIndex].id);
                placed++;
            }

            int minFree = spec.MinFreeArrows;
            EnsureMinFree(solution, minFree);
        }

        private static void EnsureMinFree(List<GenArrow> solution, int minFree)
        {
            if (solution.Count < minFree) return;

            int free = 0;
            for (int i = 0; i < solution.Count; i++)
            {
                if (solution[i].lockParents.Count == 0) free++;
            }

            for (int i = solution.Count - 1; i >= 0 && free < minFree; i--)
            {
                if (solution[i].lockParents.Count == 0) continue;
                solution[i].lockParents.Clear();
                free++;
            }
        }

        private static bool HasOutOfBounds(List<List<Cell>> paths, int width, int height)
        {
            if (paths == null) return true;
            for (int p = 0; p < paths.Count; p++)
            {
                List<Cell> path = paths[p];
                if (path == null || path.Count == 0) return true;
                for (int i = 0; i < path.Count; i++)
                {
                    Cell c = path[i];
                    if (c.x < 0 || c.y < 0 || c.x >= width || c.y >= height) return true;
                }
            }

            return false;
        }

        private static void ApplyColors(List<GenArrow> solution, DifficultySpec spec, Random rng)
        {
            if (solution.Count == 0) return;

            if (!spec.Colors)
            {
                var palette = spec.ExtraPalette
                    ? new[]
                    {
                        ArrowColor.DarkBlue, ArrowColor.Blue, ArrowColor.Grey, ArrowColor.Green,
                        ArrowColor.Red, ArrowColor.Yellow, ArrowColor.DarkBlue
                    }
                    : new[]
                    {
                        ArrowColor.DarkBlue, ArrowColor.Blue, ArrowColor.Red, ArrowColor.Grey, ArrowColor.DarkBlue
                    };
                for (int i = 0; i < solution.Count; i++)
                    solution[i].color = palette[i % palette.Length];
                return;
            }

            int blueCount = Math.Max(1, solution.Count * spec.BluePercent / 100);
            int redCount = Math.Max(1, solution.Count * spec.RedPercent / 100);
            if (blueCount + redCount > solution.Count)
                redCount = Math.Max(1, solution.Count - blueCount);

            for (int i = 0; i < solution.Count; i++)
            {
                if (i < blueCount)
                {
                    solution[i].color = rng.NextDouble() < 0.18 ? ArrowColor.Grey : ArrowColor.Blue;
                    continue;
                }

                int fromEnd = solution.Count - i;
                if (fromEnd <= redCount)
                {
                    solution[i].color = ArrowColor.Red;
                    continue;
                }

                if (spec.ExtraPalette && rng.NextDouble() < 0.35)
                {
                    solution[i].color = rng.NextDouble() < 0.5 ? ArrowColor.Green : ArrowColor.Yellow;
                    continue;
                }

                solution[i].color = rng.NextDouble() < 0.45 ? ArrowColor.DarkBlue : ArrowColor.Grey;
            }
        }

        private static List<GenButton> PlaceButtons(HashSet<long> occupied, int width, int height, DifficultySpec spec, Random rng)
        {
            var result = new List<GenButton>();
            if (!spec.Buttons) return result;

            var empty = new List<Cell>();
            CollectEmpty(empty, occupied, width, height);
            if (empty.Count == 0) return result;

            Shuffle(empty, rng);
            int count = Math.Min(spec.PickButtonCount(rng), empty.Count);
            var rotations = new[] { SwitchRotation.Rotate90CW, SwitchRotation.Rotate90CCW, SwitchRotation.Rotate180 };

            for (int i = 0; i < count; i++)
            {
                Cell cell = empty[i];
                result.Add(new GenButton
                {
                    x = cell.x,
                    y = cell.y,
                    rotation = rotations[rng.Next(rotations.Length)],
                    oneShot = rng.NextDouble() < 0.4,
                    oncePerArrow = true,
                    affectOnlyTargetColor = spec.Colors && rng.NextDouble() < 0.35,
                    targetColor = rng.NextDouble() < 0.5 ? ArrowColor.Blue : ArrowColor.Red
                });
            }

            return result;
        }

        // ----------------------------------------------------------- packing

        private static LevelData ToLevelData(int levelId, DifficultySpec spec, List<GenArrow> solution, List<GenButton> buttons)
        {
            var arrows = new LevelArrowData[solution.Count];
            var ids = new string[solution.Count];
            for (int i = 0; i < solution.Count; i++)
            {
                GenArrow a = solution[i];
                ids[i] = a.id;
                int[] pathX;
                int[] pathY;
                CopyPath(a.path, out pathX, out pathY);
                arrows[i] = new LevelArrowData
                {
                    id = a.id,
                    x = a.x,
                    y = a.y,
                    dir = a.dir.ToString(),
                    color = a.color.ToString(),
                    locked = false,
                    lockParents = a.lockParents.Count > 0 ? a.lockParents.ToArray() : Array.Empty<string>(),
                    pathX = pathX,
                    pathY = pathY
                };
            }

            var buttonData = new LevelButtonData[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
            {
                GenButton b = buttons[i];
                buttonData[i] = new LevelButtonData
                {
                    x = b.x,
                    y = b.y,
                    rotation = b.rotation.ToString(),
                    oneShot = b.oneShot,
                    oncePerArrow = b.oncePerArrow,
                    affectOnlyTargetColor = b.affectOnlyTargetColor,
                    targetColor = b.targetColor.ToString()
                };
            }

            int slack = spec.PickMoveSlack(solution.Count);
            return new LevelData
            {
                id = levelId,
                gridWidth = spec.Width,
                gridHeight = spec.Height,
                movesLimit = Math.Max(1, solution.Count + slack),
                solution = ids,
                arrows = arrows,
                buttons = buttonData
            };
        }

        private static LevelData CreateFallback(int levelId, DifficultySpec spec)
        {
            int w = Math.Max(6, spec.Width);
            int h = Math.Max(8, spec.Height);
            int xR = w - 1;
            int xL = 1;
            int yT = h - 1;

            var vertical = Walk(new Cell(xR, yT - 1), ArrowDirection.Down, Math.Min(5, h - 3));
            var top = MakeL(new Cell(xL, yT - 1), ArrowDirection.Up, 2, ArrowDirection.Right, Math.Min(4, w - 3));
            var inner = MakeL(new Cell(xL, yT - 4), ArrowDirection.Up, 3, ArrowDirection.Right, 2);
            var red = MakeU(new Cell(xL, yT - 6), ArrowDirection.Right, 2, ArrowDirection.Down, 1, ArrowDirection.Right, 1);

            var raw = new List<List<Cell>>();
            if (IsValidPolyline(vertical, 3, 8)) raw.Add(vertical);
            if (IsValidPolyline(top, 3, 8)) raw.Add(top);
            if (IsValidPolyline(inner, 3, 8)) raw.Add(inner);
            if (spec.MinArrows >= 4 && IsValidPolyline(red, 3, 8)) raw.Add(red);

            List<GenArrow> solution = PeelSolution(raw, w, h);
            if (solution == null || solution.Count < 3)
            {
                solution = new List<GenArrow>
                {
                    MakeGen("vertical", vertical, ArrowDirection.Down, ArrowColor.DarkBlue),
                    MakeGen("topZig", top, ArrowDirection.Right, ArrowColor.DarkBlue),
                    MakeGen("blueL", inner, ArrowDirection.Up, ArrowColor.Blue)
                };
            }

            for (int i = 0; i < solution.Count; i++)
            {
                if (string.IsNullOrEmpty(solution[i].id) || solution[i].id.StartsWith("a"))
                    solution[i].id = $"a{i}";
            }

            return ToLevelData(levelId, new DifficultySpec
            {
                Width = w,
                Height = h,
                MinSlack = Math.Max(2, spec.MinSlack),
                MaxSlack = Math.Max(4, spec.MaxSlack),
                MinFreeArrows = 2,
                LockPercentMax = 0
            }, solution, new List<GenButton>());
        }

        private static GenArrow MakeGen(string id, List<Cell> path, ArrowDirection dir, ArrowColor color)
        {
            return new GenArrow
            {
                id = id,
                x = path[0].x,
                y = path[0].y,
                dir = dir,
                color = color,
                path = path
            };
        }

        // -------------------------------------------------------------- utils

        private static bool IsValidPolyline(List<Cell> path, int minLen, int maxLen)
        {
            if (path == null || path.Count < minLen || path.Count > maxLen) return false;
            var seen = new HashSet<long>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Cell c = path[i];
                if (!seen.Add(Pack(c.x, c.y))) return false;
                if (i == 0) continue;
                Cell p = path[i - 1];
                int md = Math.Abs(c.x - p.x) + Math.Abs(c.y - p.y);
                if (md != 1) return false;
            }

            return true;
        }

        private static bool Overlaps(List<Cell> path, HashSet<long> occupied)
        {
            if (path == null) return true;
            for (int i = 0; i < path.Count; i++)
            {
                if (occupied.Contains(Pack(path[i].x, path[i].y))) return true;
            }

            return false;
        }

        private static bool TouchesOccupied(List<Cell> path, HashSet<long> occupied)
        {
            if (path == null || occupied == null || occupied.Count == 0) return true;
            for (int i = 0; i < path.Count; i++)
            {
                if (TouchesOccupiedAt(path[i].x, path[i].y, occupied)) return true;
            }

            return false;
        }

        private static bool TouchesOccupiedAt(int x, int y, HashSet<long> occupied)
        {
            return occupied.Contains(Pack(x + 1, y))
                || occupied.Contains(Pack(x - 1, y))
                || occupied.Contains(Pack(x, y + 1))
                || occupied.Contains(Pack(x, y - 1));
        }

        private static int ScoreNest(List<Cell> path, HashSet<long> occupied, Region bounds, bool first)
        {
            int score = 8;
            for (int i = 0; i < path.Count; i++)
            {
                Cell c = path[i];
                if (bounds.Contains(c)) score += 4;
                else score -= 6;
                score += CountOccupiedNeighbors(c.x, c.y, occupied, 1) * 5;
                score += AdjacentFlushBonus(c, i + 1 < path.Count ? path[i + 1] : c, occupied);
                if (first)
                {
                    if (c.x == bounds.x0 || c.x == bounds.x1 || c.y == bounds.y0 || c.y == bounds.y1)
                        score += 3;
                }
            }

            return score;
        }

        private static int AdjacentFlushBonus(Cell a, Cell b, HashSet<long> occupied)
        {
            int dx = b.x - a.x;
            int dy = b.y - a.y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return 0;
            int px = -dy;
            int py = dx;
            int bonus = 0;
            if (occupied.Contains(Pack(a.x + px, a.y + py)) && occupied.Contains(Pack(b.x + px, b.y + py)))
                bonus += 8;
            if (occupied.Contains(Pack(a.x - px, a.y - py)) && occupied.Contains(Pack(b.x - px, b.y - py)))
                bonus += 8;
            return bonus;
        }

        private static int CountOccupiedNeighbors(int x, int y, HashSet<long> occupied, int radius)
        {
            int n = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (occupied.Contains(Pack(x + dx, y + dy))) n++;
                }
            }

            return n;
        }

        private static void Occupy(HashSet<long> occupied, List<Cell> path)
        {
            if (path == null) return;
            for (int i = 0; i < path.Count; i++) occupied.Add(Pack(path[i].x, path[i].y));
        }

        private static void Free(HashSet<long> occupied, List<Cell> path)
        {
            if (path == null) return;
            for (int i = 0; i < path.Count; i++) occupied.Remove(Pack(path[i].x, path[i].y));
        }

        private static Region OccupiedBounds(HashSet<long> occupied, Region fallback)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (long key in occupied)
            {
                int x = (int)(key >> 32);
                int y = (int)(key & 0xffffffff);
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (minX == int.MaxValue) return fallback;
            return new Region(minX, minY, maxX, maxY);
        }

        private static Region ExpandClamp(Region box, int pad, Region limit)
        {
            return new Region(
                Math.Max(limit.x0, box.x0 - pad),
                Math.Max(limit.y0, box.y0 - pad),
                Math.Min(limit.x1, box.x1 + pad),
                Math.Min(limit.y1, box.y1 + pad));
        }

        private static void CollectEmpty(List<Cell> dst, HashSet<long> occupied, int width, int height)
        {
            dst.Clear();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!occupied.Contains(Pack(x, y))) dst.Add(new Cell(x, y));
                }
            }
        }

        private static Cell RandomCell(Region r, Random rng)
        {
            return new Cell(rng.Next(r.x0, r.x1 + 1), rng.Next(r.y0, r.y1 + 1));
        }

        private static int PickLongPathLen(DifficultySpec spec, Random rng)
        {
            int span = Math.Max(0, spec.MaxPathLen - spec.MinPathLen);
            int lo = spec.MinPathLen + span * 2 / 3;
            if (lo > spec.MaxPathLen) lo = spec.MaxPathLen;
            if (lo < spec.MinPathLen) lo = spec.MinPathLen;
            return rng.Next(lo, spec.MaxPathLen + 1);
        }

        private static Cell Offset(Cell c, ArrowDirection dir)
        {
            Step(dir, out int dx, out int dy);
            return new Cell(c.x + dx, c.y + dy);
        }

        private static List<Cell> Reversed(List<Cell> path)
        {
            var copy = new List<Cell>(path.Count);
            for (int i = path.Count - 1; i >= 0; i--) copy.Add(path[i]);
            return copy;
        }

        private static List<Cell> ClonePath(List<Cell> path)
        {
            var copy = new List<Cell>(path.Count);
            for (int i = 0; i < path.Count; i++) copy.Add(path[i]);
            return copy;
        }

        private static void CopyPath(List<Cell> path, out int[] pathX, out int[] pathY)
        {
            pathX = new int[path.Count];
            pathY = new int[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                pathX[i] = path[i].x;
                pathY[i] = path[i].y;
            }
        }

        private static bool TryTipDir(List<Cell> path, out ArrowDirection dir)
        {
            dir = ArrowDirection.Up;
            if (path == null || path.Count < 2) return false;
            Cell a = path[path.Count - 2];
            Cell b = path[path.Count - 1];
            int dx = b.x - a.x;
            int dy = b.y - a.y;
            if (dx == 0 && dy == 1) { dir = ArrowDirection.Up; return true; }
            if (dx == 1 && dy == 0) { dir = ArrowDirection.Right; return true; }
            if (dx == 0 && dy == -1) { dir = ArrowDirection.Down; return true; }
            if (dx == -1 && dy == 0) { dir = ArrowDirection.Left; return true; }
            return false;
        }

        private static int CountTurnsRange(List<Cell> path, int start, int len)
        {
            int turns = 0;
            int px = 0, py = 0;
            bool has = false;
            for (int i = 1; i < len; i++)
            {
                int dx = path[start + i].x - path[start + i - 1].x;
                int dy = path[start + i].y - path[start + i - 1].y;
                if (has && (dx != px || dy != py)) turns++;
                px = dx;
                py = dy;
                has = true;
            }

            return turns;
        }

        private static void DedupAdjacent(List<Cell> path)
        {
            for (int i = path.Count - 1; i > 0; i--)
            {
                if (path[i].x == path[i - 1].x && path[i].y == path[i - 1].y)
                    path.RemoveAt(i);
            }
        }

        public static ArrowDirection TurnCw(ArrowDirection dir) => (ArrowDirection)(((int)dir + 1) % 4);

        public static ArrowDirection TurnCcw(ArrowDirection dir) => (ArrowDirection)(((int)dir + 3) % 4);

        public static ArrowDirection Opposite(ArrowDirection dir) => (ArrowDirection)(((int)dir + 2) % 4);

        public static void Step(ArrowDirection dir, out int dx, out int dy)
        {
            switch (dir)
            {
                case ArrowDirection.Up: dx = 0; dy = 1; return;
                case ArrowDirection.Right: dx = 1; dy = 0; return;
                case ArrowDirection.Down: dx = 0; dy = -1; return;
                default: dx = -1; dy = 0; return;
            }
        }

        public static ArrowDirection Rotate(ArrowDirection dir, SwitchRotation rotation)
        {
            int steps;
            switch (rotation)
            {
                case SwitchRotation.Rotate90CW: steps = 1; break;
                case SwitchRotation.Rotate90CCW: steps = 3; break;
                case SwitchRotation.Rotate180: steps = 2; break;
                default: steps = 0; break;
            }

            return (ArrowDirection)(((int)dir + steps) % 4);
        }

        public static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;

        private static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private static void Shuffle<T>(T[] array, Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = array[i];
                array[i] = array[j];
                array[j] = tmp;
            }
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private struct Cell
        {
            public int x, y;
            public Cell(int x, int y) { this.x = x; this.y = y; }
        }

        private struct Region
        {
            public int x0, y0, x1, y1;
            public Region(int x0, int y0, int x1, int y1)
            {
                this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1;
            }

            public int Width => x1 - x0 + 1;
            public int Height => y1 - y0 + 1;
            public bool Contains(Cell c) => c.x >= x0 && c.y >= y0 && c.x <= x1 && c.y <= y1;
        }

        private struct GrowOpt
        {
            public Cell cell;
            public ArrowDirection dir;
            public int score;
            public GrowOpt(Cell cell, ArrowDirection dir, int score)
            {
                this.cell = cell;
                this.dir = dir;
                this.score = score;
            }
        }

        private struct PeelCand
        {
            public int idx;
            public int score;
            public List<Cell> path;
            public ArrowDirection dir;
            public PeelCand(int idx, int score, List<Cell> path, ArrowDirection dir)
            {
                this.idx = idx;
                this.score = score;
                this.path = path;
                this.dir = dir;
            }
        }

        private sealed class GenArrow
        {
            public string id;
            public int x, y;
            public ArrowDirection dir;
            public ArrowColor color;
            public List<Cell> path;
            public readonly List<string> lockParents = new List<string>();
        }

        private sealed class GenButton
        {
            public int x, y;
            public SwitchRotation rotation;
            public bool oneShot;
            public bool oncePerArrow;
            public bool affectOnlyTargetColor;
            public ArrowColor targetColor;
        }

        public sealed class DifficultySpec
        {
            public int MinArrows, MaxArrows;
            public int Width, Height;
            public int MinPathLen, MaxPathLen, MaxTurns;
            public bool Locks, Colors, Buttons, WeaveBias, ExtraPalette;
            public int MinLocks, MaxLocks, MinButtons, MaxButtons;
            public int MinSlack, MaxSlack, BluePercent, RedPercent;
            public int LockPercentMax, MinFreeArrows;
            public float WeaveChance;

            /// <summary>
            /// Школа с 1-го: сетка 14×18, как поздний сложный уровень. Slack 0–1, минимум 2 свободные, без кнопок.
            /// </summary>
            public static DifficultySpec School(int id)
            {
                if (id <= 6)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 24, MaxArrows = 26, Width = 14, Height = 18,
                        MinPathLen = 6, MaxPathLen = 10, MaxTurns = 5,
                        Locks = true, MinLocks = 6, MaxLocks = 10, LockPercentMax = 42,
                        ExtraPalette = true, WeaveBias = true, WeaveChance = 0.94f,
                        MinSlack = 0, MaxSlack = 1, MinFreeArrows = 2,
                        Buttons = false, MinButtons = 0, MaxButtons = 0
                    };
                }

                if (id <= 13)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 26, MaxArrows = 30, Width = 14, Height = 18,
                        MinPathLen = 6, MaxPathLen = 11, MaxTurns = 6,
                        Locks = true, MinLocks = 7, MaxLocks = 12, LockPercentMax = 44,
                        ExtraPalette = true, WeaveBias = true, WeaveChance = 0.95f,
                        MinSlack = 0, MaxSlack = 1, MinFreeArrows = 2,
                        Buttons = false, MinButtons = 0, MaxButtons = 0
                    };
                }

                return new DifficultySpec
                {
                    MinArrows = 26, MaxArrows = 32, Width = 14, Height = 18,
                    MinPathLen = 6, MaxPathLen = 11, MaxTurns = 6,
                    Locks = true, MinLocks = 7, MaxLocks = 12, LockPercentMax = 44,
                    ExtraPalette = true, WeaveBias = true, WeaveChance = 0.96f,
                    MinSlack = 0, MaxSlack = 1, MinFreeArrows = 2,
                    Buttons = false, MinButtons = 0, MaxButtons = 0
                };
            }

            public static DifficultySpec For(int id)
            {
                if (id <= 10)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 3, MaxArrows = 4, Width = 6, Height = 8,
                        MinPathLen = 4, MaxPathLen = 6, MaxTurns = 3,
                        WeaveBias = true, WeaveChance = 0.55f,
                        MinSlack = 1, MaxSlack = 2,
                        MinFreeArrows = 2, LockPercentMax = 0
                    };
                }

                if (id <= 25)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 5, MaxArrows = 7, Width = 7, Height = 9,
                        MinPathLen = 5, MaxPathLen = 8, MaxTurns = 4,
                        WeaveBias = true, WeaveChance = 0.7f,
                        MinSlack = 0, MaxSlack = 2,
                        MinFreeArrows = 3
                    };
                }

                if (id <= 50)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 8, MaxArrows = 12, Width = 8, Height = 11,
                        MinPathLen = 5, MaxPathLen = 9, MaxTurns = 4,
                        ExtraPalette = true,
                        WeaveBias = true, WeaveChance = 0.8f,
                        MinSlack = 0, MaxSlack = 1,
                        MinFreeArrows = 3
                    };
                }

                if (id <= 75)
                {
                    return new DifficultySpec
                    {
                        MinArrows = 12, MaxArrows = 18, Width = 10, Height = 14,
                        MinPathLen = 5, MaxPathLen = 9, MaxTurns = 5,
                        ExtraPalette = true,
                        WeaveBias = true, WeaveChance = 0.85f,
                        MinSlack = 0, MaxSlack = 1,
                        MinFreeArrows = 4
                    };
                }

                return new DifficultySpec
                {
                    MinArrows = 16, MaxArrows = 24, Width = 12, Height = 16,
                    MinPathLen = 5, MaxPathLen = 10, MaxTurns = 5,
                    ExtraPalette = true,
                    WeaveBias = true, WeaveChance = 0.9f,
                    MinSlack = 0, MaxSlack = 1,
                    MinFreeArrows = 4
                };
            }

            public int PickArrowCount(Random rng)
            {
                return rng.Next(MinArrows, MaxArrows + 1);
            }

            public int PickLockCount(Random rng, int arrowCount)
            {
                if (!Locks) return 0;
                int minFree = Math.Max(2, MinFreeArrows);
                int percentCap = Math.Max(0, arrowCount * Math.Max(0, LockPercentMax) / 100);
                int max = Math.Min(MaxLocks, Math.Min(percentCap, arrowCount - minFree));
                int min = Math.Min(MinLocks, max);
                if (max <= 0) return 0;
                if (min < 0) min = 0;
                return rng.Next(min, max + 1);
            }

            public int PickButtonCount(Random rng) => rng.Next(MinButtons, MaxButtons + 1);

            public int PickMoveSlack(int arrowCount)
            {
                if (MaxSlack <= MinSlack) return MinSlack;
                return MinSlack + (arrowCount % (MaxSlack - MinSlack + 1));
            }
        }
    }

    /// <summary>
    /// Правила как в игре: коридор (цвет не блокирует). Луч от кончика до края сетки.
    /// Книга блокирует клетку, пока жив ключ; после хода-ключа книги нет.
    /// Указка блокирует все свои клетки; после Success поворачивается, если новые клетки свободны.
    /// Портфель — две клетки-блокеры. Сдвиг в решении: токены bp1:+ / bp1:- (шаг вдоль оси / против).
    /// </summary>
    public static partial class LevelSolver
    {
        public static bool IsSolvable(LevelData data)
        {
            List<SimArrow> arrows;
            Dictionary<string, SimArrow> byId;
            List<SimBook> books;
            List<SimPointer> pointers;
            List<SimBackpack> backpacks;
            if (!TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks, true)) return false;

            string[] order = data.solution;
            int arrowTokens = CountArrowTokens(order, backpacks);
            bool schoolish = HasSchoolProps(data);
            if (order == null || arrowTokens != arrows.Count)
            {
                if (schoolish) return false;
                order = new string[arrows.Count];
                for (int i = 0; i < arrows.Count; i++) order[i] = arrows[i].id;
            }

            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);

            for (int step = 0; step < order.Length; step++)
            {
                string token = order[step];
                if (TryParseBackpackToken(token, backpacks, out SimBackpack pack, out bool plus))
                {
                    if (!TrySlideBackpack(pack, plus, arrows, books, pointers, backpacks, width, height))
                        return false;
                    continue;
                }

                if (!byId.TryGetValue(token, out SimArrow arrow)) return false;
                if (!arrows.Contains(arrow)) return false;
                if (!PathClear(arrow, arrows, books, pointers, backpacks, width, height)) return false;

                arrows.Remove(arrow);
                byId.Remove(arrow.id);
                TryRotatePointers(pointers, arrows, books, backpacks, width, height);
            }

            return arrows.Count == 0;
        }

        public static bool AllCellsInBounds(LevelData data)
        {
            if (data == null || data.arrows == null) return false;
            int width = data.gridWidth;
            int height = data.gridHeight;
            if (width <= 0 || height <= 0) return false;

            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData src = data.arrows[i];
                int[] pathX;
                int[] pathY;
                ReadPath(src, out pathX, out pathY);
                for (int c = 0; c < pathX.Length; c++)
                {
                    if (pathX[c] < 0 || pathY[c] < 0 || pathX[c] >= width || pathY[c] >= height)
                        return false;
                }
            }

            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData book = data.books[i];
                    if (book == null) continue;
                    if (book.x < 0 || book.y < 0 || book.x >= width || book.y >= height)
                        return false;
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
                        if (cells[c].x < 0 || cells[c].y < 0 || cells[c].x >= width || cells[c].y >= height)
                            return false;
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
                        if (cells[c].x < 0 || cells[c].y < 0 || cells[c].x >= width || cells[c].y >= height)
                            return false;
                    }
                }
            }

            return data.arrows.Length > 0;
        }

        /// <summary>
        /// ≥2 легальных цели, несколько свободных, задуманный порядок проходит,
        /// случайный порядок часто нет, полная цепочка замков запрещена.
        /// Только кампания. Школа — SchoolLevelSolver (IsSolvable + AllCellsInBounds).
        /// </summary>
        public static bool PassesPuzzleFilter(LevelData data, Random rng) =>
            PassesPuzzleFilter(data, rng, true);

        public static bool PassesPuzzleFilter(LevelData data, Random rng, bool strict)
        {
            if (data == null || rng == null) return false;
            if (!AllCellsInBounds(data)) return false;
            if (!IsSolvable(data)) return false;
            if (!LocksAreSparse(data)) return false;

            List<SimArrow> arrows;
            Dictionary<string, SimArrow> byId;
            List<SimBook> books;
            List<SimPointer> pointers;
            List<SimBackpack> backpacks;
            if (!TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks)) return false;

            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            int unlocked = arrows.Count;
            List<SimArrow> legal = CollectLegal(arrows, byId, books, pointers, backpacks, width, height);
            if (unlocked < 2 || legal.Count < 2) return false;
            if (legal.Count >= unlocked) return false;

            int trials = data.arrows.Length >= 12 ? 10 : 14;
            int spamFails = 0;
            for (int i = 0; i < trials; i++)
            {
                if (!PlayRandom(data, rng, false)) spamFails++;
            }

            if (!strict) return true;

            int id = data.id;
            if (id <= 10) return true;
            if (id <= 25) return spamFails >= Math.Max(2, trials / 4);
            if (id <= 50) return spamFails >= trials / 3;
            return spamFails >= trials / 2;
        }

        private static bool LocksAreSparse(LevelData data)
        {
            if (data.arrows == null) return false;
            int locked = 0;
            var ids = new HashSet<string>();
            for (int i = 0; i < data.arrows.Length; i++)
            {
                string id = string.IsNullOrEmpty(data.arrows[i].id) ? $"a{i}" : data.arrows[i].id;
                if (!ids.Add(id)) return false;
            }

            for (int i = 0; i < data.arrows.Length; i++)
            {
                string[] parents = data.arrows[i].GetLockParents();
                if (parents.Length == 0) continue;
                if (parents.Length > 1) return false;
                if (string.IsNullOrEmpty(parents[0]) || !ids.Contains(parents[0])) return false;
                string self = string.IsNullOrEmpty(data.arrows[i].id) ? $"a{i}" : data.arrows[i].id;
                if (parents[0] == self) return false;
                locked++;
            }

            int maxLocks = Math.Max(0, (data.arrows.Length * 25 + 99) / 100);
            if (locked > Math.Max(maxLocks, 1) && data.arrows.Length > 4) return false;
            if (data.arrows.Length - locked < 2) return false;
            return true;
        }

        private static bool PlayRandom(LevelData data, Random rng, bool legalOnly)
        {
            List<SimArrow> arrows;
            Dictionary<string, SimArrow> byId;
            List<SimBook> books;
            List<SimPointer> pointers;
            List<SimBackpack> backpacks;
            if (!TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks)) return false;

            int width = Math.Max(1, data.gridWidth);
            int height = Math.Max(1, data.gridHeight);
            int movesLeft = Math.Max(1, data.movesLimit);
            int guard = arrows.Count * 10 + 8;

            while (arrows.Count > 0 && guard-- > 0)
            {
                if (legalOnly)
                {
                    List<SimArrow> legal = CollectLegal(arrows, byId, books, pointers, backpacks, width, height);
                    if (legal.Count == 0) return false;
                    SimArrow pick = legal[rng.Next(legal.Count)];
                    arrows.Remove(pick);
                    byId.Remove(pick.id);
                    TryRotatePointers(pointers, arrows, books, backpacks, width, height);
                    movesLeft--;
                    if (arrows.Count > 0 && movesLeft <= 0) return false;
                    continue;
                }

                SimArrow spam = arrows[rng.Next(arrows.Count)];
                if (!PathClear(spam, arrows, books, pointers, backpacks, width, height))
                {
                    movesLeft--;
                    if (movesLeft <= 0) return false;
                    continue;
                }

                arrows.Remove(spam);
                byId.Remove(spam.id);
                TryRotatePointers(pointers, arrows, books, backpacks, width, height);
                movesLeft--;
                if (arrows.Count > 0 && movesLeft <= 0) return false;
            }

            return arrows.Count == 0;
        }

        private static bool TryParseBackpackToken(string token, List<SimBackpack> backpacks, out SimBackpack pack, out bool plus)
        {
            pack = null;
            plus = true;
            if (string.IsNullOrEmpty(token) || backpacks == null) return false;
            int sep = token.LastIndexOf(':');
            if (sep <= 0 || sep != token.Length - 2) return false;
            char sign = token[token.Length - 1];
            if (sign != '+' && sign != '-') return false;
            plus = sign == '+';
            string id = token.Substring(0, sep);
            for (int i = 0; i < backpacks.Count; i++)
            {
                if (backpacks[i].id != id) continue;
                pack = backpacks[i];
                return true;
            }

            return false;
        }

        private static int CountArrowTokens(string[] order, List<SimBackpack> backpacks)
        {
            if (order == null) return 0;
            int n = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (TryParseBackpackToken(order[i], backpacks, out _, out _)) continue;
                n++;
            }

            return n;
        }

        public static bool HasSchoolProps(LevelData data)
        {
            if (data == null) return false;
            if (data.books != null && data.books.Length > 0) return true;
            if (data.pointers != null && data.pointers.Length > 0) return true;
            if (data.backpacks != null && data.backpacks.Length > 0) return true;
            return false;
        }

        /// <summary>Челнок: + вдоль оси JSON, − против. Не крутит указку.</summary>
        private static bool TrySlideBackpack(
            SimBackpack pack,
            bool plus,
            List<SimArrow> arrows,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            int width,
            int height)
        {
            if (pack == null || pack.cells == null || pack.cells.Length != BackpackController.CellCount) return false;

            ArrowDirection slide = plus ? pack.dir : BackpackController.Opposite(pack.dir);
            LevelFactory.Step(slide, out int dx, out int dy);
            Vector2Int newAnchor = new Vector2Int(pack.anchor.x + dx, pack.anchor.y + dy);
            Vector2Int[] next = BackpackController.BuildCells(newAnchor, pack.dir);
            if (next == null || next.Length != BackpackController.CellCount) return false;

            var occupied = CollectOccupied(arrows, books, pointers, backpacks, pack);
            for (int i = 0; i < next.Length; i++)
            {
                Vector2Int cell = next[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height) return false;
                if (occupied.Contains(LevelFactory.Pack(cell.x, cell.y))) return false;
            }

            pack.anchor = newAnchor;
            pack.cells = next;
            return true;
        }

        private static HashSet<long> CollectOccupied(
            List<SimArrow> arrows,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            SimBackpack skip)
        {
            var occupied = new HashSet<long>();
            if (arrows != null)
            {
                for (int a = 0; a < arrows.Count; a++)
                {
                    SimArrow arrow = arrows[a];
                    for (int i = 0; i < arrow.pathX.Length; i++)
                        occupied.Add(LevelFactory.Pack(arrow.pathX[i], arrow.pathY[i]));
                }
            }

            if (books != null)
            {
                var aliveIds = new HashSet<string>();
                if (arrows != null)
                {
                    for (int a = 0; a < arrows.Count; a++) aliveIds.Add(arrows[a].id);
                }

                for (int b = 0; b < books.Count; b++)
                {
                    SimBook book = books[b];
                    if (string.IsNullOrEmpty(book.keyArrowId) || !aliveIds.Contains(book.keyArrowId)) continue;
                    occupied.Add(LevelFactory.Pack(book.x, book.y));
                }
            }

            if (pointers != null)
            {
                for (int p = 0; p < pointers.Count; p++)
                {
                    SimPointer pointer = pointers[p];
                    if (pointer.cells == null) continue;
                    for (int c = 0; c < pointer.cells.Length; c++)
                        occupied.Add(LevelFactory.Pack(pointer.cells[c].x, pointer.cells[c].y));
                }
            }

            if (backpacks == null) return occupied;
            for (int b = 0; b < backpacks.Count; b++)
            {
                SimBackpack pack = backpacks[b];
                if (pack == null || pack == skip || pack.cells == null) continue;
                for (int c = 0; c < pack.cells.Length; c++)
                    occupied.Add(LevelFactory.Pack(pack.cells[c].x, pack.cells[c].y));
            }

            return occupied;
        }

        private static bool TryBuild(
            LevelData data,
            out List<SimArrow> arrows,
            out Dictionary<string, SimArrow> byId,
            out List<SimBook> books,
            out List<SimPointer> pointers,
            out List<SimBackpack> backpacks)
        {
            return TryBuild(data, out arrows, out byId, out books, out pointers, out backpacks, false);
        }

        private static bool TryBuild(
            LevelData data,
            out List<SimArrow> arrows,
            out Dictionary<string, SimArrow> byId,
            out List<SimBook> books,
            out List<SimPointer> pointers,
            out List<SimBackpack> backpacks,
            bool requireAllProps)
        {
            arrows = null;
            byId = null;
            books = null;
            pointers = null;
            backpacks = null;
            if (data == null || data.arrows == null || data.arrows.Length == 0) return false;

            arrows = new List<SimArrow>(data.arrows.Length);
            byId = new Dictionary<string, SimArrow>(data.arrows.Length);
            for (int i = 0; i < data.arrows.Length; i++)
            {
                LevelArrowData src = data.arrows[i];
                string id = string.IsNullOrEmpty(src.id) ? $"a{i}" : src.id;
                int[] pathX;
                int[] pathY;
                ReadPath(src, out pathX, out pathY);
                var sim = new SimArrow
                {
                    id = id,
                    x = src.x,
                    y = src.y,
                    dir = src.ParseDirection(),
                    color = src.ParseColor(),
                    lockParents = src.GetLockParents(),
                    usedButtons = new HashSet<int>(),
                    pathX = pathX,
                    pathY = pathY
                };
                arrows.Add(sim);
                if (byId.ContainsKey(id)) return false;
                byId[id] = sim;
            }

            books = new List<SimBook>();
            if (data.books != null)
            {
                for (int i = 0; i < data.books.Length; i++)
                {
                    LevelBookData src = data.books[i];
                    if (src == null) continue;
                    if (string.IsNullOrEmpty(src.blockedArrowId) || string.IsNullOrEmpty(src.keyArrowId)) continue;
                    if (!byId.ContainsKey(src.blockedArrowId) || !byId.ContainsKey(src.keyArrowId)) continue;
                    if (src.blockedArrowId == src.keyArrowId) continue;
                    SimArrow blockedSim;
                    if (!byId.TryGetValue(src.blockedArrowId, out blockedSim)) continue;
                    if (!LockParentsContain(blockedSim, src.keyArrowId)) continue;
                    if (!SimOnExitRay(blockedSim, src.x, src.y, Math.Max(1, data.gridWidth), Math.Max(1, data.gridHeight)))
                        continue;
                    books.Add(new SimBook
                    {
                        id = string.IsNullOrEmpty(src.id) ? $"book{i}" : src.id,
                        x = src.x,
                        y = src.y,
                        blockedArrowId = src.blockedArrowId,
                        keyArrowId = src.keyArrowId
                    });
                }
            }

            pointers = new List<SimPointer>();
            if (data.pointers != null)
            {
                var used = new HashSet<long>();
                for (int a = 0; a < arrows.Count; a++)
                {
                    SimArrow arrow = arrows[a];
                    for (int c = 0; c < arrow.pathX.Length; c++)
                        used.Add(LevelFactory.Pack(arrow.pathX[c], arrow.pathY[c]));
                }

                for (int b = 0; b < books.Count; b++)
                    used.Add(LevelFactory.Pack(books[b].x, books[b].y));

                int width = Math.Max(1, data.gridWidth);
                int height = Math.Max(1, data.gridHeight);
                for (int i = 0; i < data.pointers.Length; i++)
                {
                    LevelPointerData src = data.pointers[i];
                    if (src == null) continue;
                    Vector2Int pivot = new Vector2Int(src.pivotX, src.pivotY);
                    int length = Math.Max(1, src.length);
                    ArrowDirection dir = src.ParseDirection();
                    Vector2Int[] cells = PointerController.BuildCells(pivot, length, dir);
                    bool valid = cells != null && cells.Length > 0;
                    if (valid)
                    {
                        for (int c = 0; c < cells.Length; c++)
                        {
                            if (cells[c].x < 0 || cells[c].y < 0 || cells[c].x >= width || cells[c].y >= height)
                            {
                                valid = false;
                                break;
                            }

                            if (!used.Add(LevelFactory.Pack(cells[c].x, cells[c].y)))
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    if (!valid)
                    {
                        if (requireAllProps) return false;
                        continue;
                    }

                    pointers.Add(new SimPointer
                    {
                        id = string.IsNullOrEmpty(src.id) ? $"p{i}" : src.id,
                        pivot = pivot,
                        length = length,
                        dir = dir,
                        rotateCW = src.rotateCW,
                        cells = cells
                    });
                }
            }

            backpacks = new List<SimBackpack>();
            if (data.backpacks != null)
            {
                var usedPack = new HashSet<long>();
                for (int a = 0; a < arrows.Count; a++)
                {
                    SimArrow arrow = arrows[a];
                    for (int c = 0; c < arrow.pathX.Length; c++)
                        usedPack.Add(LevelFactory.Pack(arrow.pathX[c], arrow.pathY[c]));
                }

                for (int b = 0; b < books.Count; b++)
                    usedPack.Add(LevelFactory.Pack(books[b].x, books[b].y));

                for (int p = 0; p < pointers.Count; p++)
                {
                    SimPointer pointer = pointers[p];
                    if (pointer.cells == null) continue;
                    for (int c = 0; c < pointer.cells.Length; c++)
                        usedPack.Add(LevelFactory.Pack(pointer.cells[c].x, pointer.cells[c].y));
                }

                int widthB = Math.Max(1, data.gridWidth);
                int heightB = Math.Max(1, data.gridHeight);
                for (int i = 0; i < data.backpacks.Length; i++)
                {
                    LevelBackpackData src = data.backpacks[i];
                    if (src == null)
                    {
                        if (requireAllProps) return false;
                        continue;
                    }

                    Vector2Int anchor = new Vector2Int(src.x, src.y);
                    ArrowDirection dir = src.ParseDirection();
                    Vector2Int[] cells = BackpackController.BuildCells(anchor, dir);
                    bool valid = cells != null && cells.Length == BackpackController.CellCount;
                    if (valid)
                    {
                        for (int c = 0; c < cells.Length; c++)
                        {
                            if (cells[c].x < 0 || cells[c].y < 0 || cells[c].x >= widthB || cells[c].y >= heightB)
                            {
                                valid = false;
                                break;
                            }

                            if (!usedPack.Add(LevelFactory.Pack(cells[c].x, cells[c].y)))
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    if (!valid)
                    {
                        if (requireAllProps) return false;
                        continue;
                    }

                    backpacks.Add(new SimBackpack
                    {
                        id = string.IsNullOrEmpty(src.id) ? $"bp{i}" : src.id,
                        anchor = anchor,
                        dir = dir,
                        cells = cells
                    });
                }
            }

            if (requireAllProps)
            {
                int authoredBooks = CountSpawnableBooks(data.books, byId, Math.Max(1, data.gridWidth), Math.Max(1, data.gridHeight));
                int authoredPointers = CountAuthored(data.pointers);
                int authoredBackpacks = CountAuthored(data.backpacks);
                if (books.Count != authoredBooks) return false;
                if (pointers.Count != authoredPointers) return false;
                if (backpacks.Count != authoredBackpacks) return false;
            }

            return true;
        }

        private static int CountAuthored(LevelBookData[] items)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) n++;
            }

            return n;
        }

        private static int CountSpawnableBooks(LevelBookData[] items, Dictionary<string, SimArrow> byId, int width, int height)
        {
            if (items == null || byId == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                LevelBookData src = items[i];
                if (src == null) continue;
                if (string.IsNullOrEmpty(src.blockedArrowId) || string.IsNullOrEmpty(src.keyArrowId)) continue;
                SimArrow blocked;
                if (!byId.TryGetValue(src.blockedArrowId, out blocked)) continue;
                if (!byId.ContainsKey(src.keyArrowId)) continue;
                if (src.blockedArrowId == src.keyArrowId) continue;
                if (!LockParentsContain(blocked, src.keyArrowId)) continue;
                if (!SimOnExitRay(blocked, src.x, src.y, width, height)) continue;
                n++;
            }

            return n;
        }

        private static bool SimOnExitRay(SimArrow blocked, int x, int y, int width, int height)
        {
            if (blocked == null || blocked.pathX == null || blocked.pathY == null || blocked.pathX.Length == 0)
                return false;

            LevelFactory.Step(blocked.dir, out int dx, out int dy);
            int cx = blocked.pathX[blocked.pathX.Length - 1] + dx;
            int cy = blocked.pathY[blocked.pathY.Length - 1] + dy;
            while (cx >= 0 && cy >= 0 && cx < width && cy < height)
            {
                if (cx == x && cy == y) return true;
                cx += dx;
                cy += dy;
            }

            return false;
        }

        private static bool LockParentsContain(SimArrow arrow, string keyId)
        {
            if (arrow == null || string.IsNullOrEmpty(keyId) || arrow.lockParents == null) return false;
            for (int i = 0; i < arrow.lockParents.Length; i++)
            {
                if (arrow.lockParents[i] == keyId) return true;
            }

            return false;
        }

        private static int CountAuthored(LevelPointerData[] items)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) n++;
            }

            return n;
        }

        private static int CountAuthored(LevelBackpackData[] items)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) n++;
            }

            return n;
        }

        private static List<SimArrow> CollectLegal(
            List<SimArrow> arrows,
            Dictionary<string, SimArrow> byId,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            int width,
            int height)
        {
            var legal = new List<SimArrow>(arrows.Count);
            for (int i = 0; i < arrows.Count; i++)
            {
                SimArrow arrow = arrows[i];
                if (!PathClear(arrow, arrows, books, pointers, backpacks, width, height)) continue;
                legal.Add(arrow);
            }

            return legal;
        }

        private static void ReadPath(LevelArrowData src, out int[] pathX, out int[] pathY)
        {
            if (src.pathX != null && src.pathY != null && src.pathX.Length > 0 && src.pathX.Length == src.pathY.Length)
            {
                pathX = src.pathX;
                pathY = src.pathY;
                return;
            }

            pathX = new[] { src.x };
            pathY = new[] { src.y };
        }

        private static bool ColorAllowed(ArrowColor color, List<SimArrow> arrows)
        {
            if (color != ArrowColor.Red) return true;
            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i].color == ArrowColor.Blue) return false;
            }

            return true;
        }

        private static bool PathClear(
            SimArrow arrow,
            List<SimArrow> arrows,
            List<SimBook> books,
            List<SimPointer> pointers,
            List<SimBackpack> backpacks,
            int width,
            int height)
        {
            if (arrow.pathX == null || arrow.pathX.Length == 0) return false;

            var occupied = new HashSet<long>(arrows.Count * 4);
            var self = new HashSet<long>(arrow.pathX.Length);
            for (int i = 0; i < arrow.pathX.Length; i++) self.Add(LevelFactory.Pack(arrow.pathX[i], arrow.pathY[i]));

            for (int a = 0; a < arrows.Count; a++)
            {
                SimArrow other = arrows[a];
                for (int i = 0; i < other.pathX.Length; i++)
                    occupied.Add(LevelFactory.Pack(other.pathX[i], other.pathY[i]));
            }

            if (books != null)
            {
                var aliveIds = new HashSet<string>(arrows.Count);
                for (int a = 0; a < arrows.Count; a++) aliveIds.Add(arrows[a].id);

                for (int b = 0; b < books.Count; b++)
                {
                    SimBook book = books[b];
                    if (string.IsNullOrEmpty(book.keyArrowId) || !aliveIds.Contains(book.keyArrowId)) continue;
                    occupied.Add(LevelFactory.Pack(book.x, book.y));
                }
            }

            if (pointers != null)
            {
                for (int p = 0; p < pointers.Count; p++)
                {
                    SimPointer pointer = pointers[p];
                    if (pointer.cells == null) continue;
                    for (int c = 0; c < pointer.cells.Length; c++)
                        occupied.Add(LevelFactory.Pack(pointer.cells[c].x, pointer.cells[c].y));
                }
            }

            if (backpacks != null)
            {
                for (int b = 0; b < backpacks.Count; b++)
                {
                    SimBackpack pack = backpacks[b];
                    if (pack.cells == null) continue;
                    for (int c = 0; c < pack.cells.Length; c++)
                        occupied.Add(LevelFactory.Pack(pack.cells[c].x, pack.cells[c].y));
                }
            }

            LevelFactory.Step(arrow.dir, out int dx, out int dy);
            int tip = arrow.pathX.Length - 1;
            int cx = arrow.pathX[tip] + dx;
            int cy = arrow.pathY[tip] + dy;
            while (cx >= 0 && cy >= 0 && cx < width && cy < height)
            {
                long key = LevelFactory.Pack(cx, cy);
                if (occupied.Contains(key) && !self.Contains(key)) return false;
                cx += dx;
                cy += dy;
            }

            return true;
        }

        private static void TryRotatePointers(
            List<SimPointer> pointers,
            List<SimArrow> arrows,
            List<SimBook> books,
            List<SimBackpack> backpacks,
            int width,
            int height)
        {
            if (pointers == null || pointers.Count == 0) return;

            var occupied = new HashSet<long>();
            if (arrows != null)
            {
                for (int a = 0; a < arrows.Count; a++)
                {
                    SimArrow arrow = arrows[a];
                    for (int i = 0; i < arrow.pathX.Length; i++)
                        occupied.Add(LevelFactory.Pack(arrow.pathX[i], arrow.pathY[i]));
                }
            }

            if (books != null)
            {
                var aliveIds = new HashSet<string>();
                if (arrows != null)
                {
                    for (int a = 0; a < arrows.Count; a++) aliveIds.Add(arrows[a].id);
                }

                for (int b = 0; b < books.Count; b++)
                {
                    SimBook book = books[b];
                    if (string.IsNullOrEmpty(book.keyArrowId) || !aliveIds.Contains(book.keyArrowId)) continue;
                    occupied.Add(LevelFactory.Pack(book.x, book.y));
                }
            }

            for (int p = 0; p < pointers.Count; p++)
            {
                SimPointer pointer = pointers[p];
                if (pointer.cells == null) continue;
                for (int c = 0; c < pointer.cells.Length; c++)
                    occupied.Add(LevelFactory.Pack(pointer.cells[c].x, pointer.cells[c].y));
            }

            if (backpacks != null)
            {
                for (int b = 0; b < backpacks.Count; b++)
                {
                    SimBackpack pack = backpacks[b];
                    if (pack.cells == null) continue;
                    for (int c = 0; c < pack.cells.Length; c++)
                        occupied.Add(LevelFactory.Pack(pack.cells[c].x, pack.cells[c].y));
                }
            }

            for (int p = 0; p < pointers.Count; p++)
            {
                SimPointer pointer = pointers[p];
                if (pointer.cells == null || pointer.cells.Length == 0) continue;

                Vector2Int[] next = PointerController.RotateCellsAroundPivot(pointer.cells, pointer.pivot, pointer.rotateCW);
                bool can = next != null && next.Length == pointer.cells.Length;
                if (can)
                {
                    for (int c = 0; c < next.Length; c++)
                    {
                        if (next[c].x < 0 || next[c].y < 0 || next[c].x >= width || next[c].y >= height)
                        {
                            can = false;
                            break;
                        }

                        long key = LevelFactory.Pack(next[c].x, next[c].y);
                        if (!occupied.Contains(key)) continue;
                        bool self = false;
                        for (int s = 0; s < pointer.cells.Length; s++)
                        {
                            if (pointer.cells[s] == next[c])
                            {
                                self = true;
                                break;
                            }
                        }

                        if (!self)
                        {
                            can = false;
                            break;
                        }
                    }
                }

                if (!can) continue;

                for (int c = 0; c < pointer.cells.Length; c++)
                    occupied.Remove(LevelFactory.Pack(pointer.cells[c].x, pointer.cells[c].y));

                pointer.dir = PointerController.RotateDir(pointer.dir, pointer.rotateCW);
                pointer.cells = next;
                for (int c = 0; c < next.Length; c++)
                    occupied.Add(LevelFactory.Pack(next[c].x, next[c].y));
            }
        }

        private static void ApplyGravity(List<SimArrow> arrows, List<SimButton> buttons, int removedX, int removedY)
        {
            var column = new List<SimArrow>();
            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i].pathX.Length > 1) continue;
                if (arrows[i].x == removedX && arrows[i].y > removedY) column.Add(arrows[i]);
            }

            column.Sort((a, b) => a.y.CompareTo(b.y));

            int expected = removedY + 1;
            for (int i = 0; i < column.Count; i++)
            {
                SimArrow arrow = column[i];
                if (arrow.y != expected) break;

                int newY = arrow.y - 1;
                TryRotateThroughButtons(arrow, buttons, removedX, newY);
                arrow.y = newY;
                if (arrow.pathY.Length == 1) arrow.pathY[0] = newY;
                expected = arrow.y + 2;
            }
        }

        private static void TryRotateThroughButtons(SimArrow arrow, List<SimButton> buttons, int x, int passedY)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                SimButton button = buttons[i];
                if (button.used && button.oneShot) continue;
                if (button.x != x || button.y != passedY) continue;
                if (button.affectOnlyTargetColor && arrow.color != button.targetColor) continue;
                if (button.oncePerArrow && !arrow.usedButtons.Add(i)) continue;

                arrow.dir = LevelFactory.Rotate(arrow.dir, button.rotation);
                button.used = true;
                if (button.oncePerArrow) break;
            }
        }

        private sealed class SimArrow
        {
            public string id;
            public int x, y;
            public ArrowDirection dir;
            public ArrowColor color;
            public string[] lockParents;
            public HashSet<int> usedButtons;
            public int[] pathX;
            public int[] pathY;
        }

        private sealed class SimBook
        {
            public string id;
            public int x, y;
            public string blockedArrowId;
            public string keyArrowId;
        }

        private sealed class SimPointer
        {
            public string id;
            public Vector2Int pivot;
            public int length;
            public ArrowDirection dir;
            public bool rotateCW;
            public Vector2Int[] cells;
        }

        private sealed class SimBackpack
        {
            public string id;
            public Vector2Int anchor;
            public ArrowDirection dir;
            public Vector2Int[] cells;
        }

        private sealed class SimButton
        {
            public int x, y;
            public SwitchRotation rotation;
            public bool oneShot;
            public bool oncePerArrow;
            public bool affectOnlyTargetColor;
            public ArrowColor targetColor;
            public bool used;
        }
    }
}
