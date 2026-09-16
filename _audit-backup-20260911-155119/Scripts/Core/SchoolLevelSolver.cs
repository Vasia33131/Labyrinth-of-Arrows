namespace Unpuzzle
{
    /// <summary>
    /// Гейты школьных JSON: solvable + клетки в сетке.
    /// Без PassesPuzzleFilter, LocksAreSparse и порогов id кампании.
    /// </summary>
    public static class SchoolLevelSolver
    {
        public static bool IsSolvable(LevelData data) => LevelSolver.IsSolvable(data);

        public static bool AllCellsInBounds(LevelData data) => LevelSolver.AllCellsInBounds(data);

        public static bool Passes(LevelData data)
        {
            if (data == null) return false;
            return AllCellsInBounds(data) && IsSolvable(data);
        }
    }
}
