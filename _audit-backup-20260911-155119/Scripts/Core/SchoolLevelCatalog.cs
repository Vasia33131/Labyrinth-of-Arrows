using UnityEngine;

namespace Unpuzzle
{
    /// <summary>Каталог школьных JSON: Resources/Levels/School/school_XX.json. Кампанию не трогает.</summary>
    public static class SchoolLevelCatalog
    {
        public const int TargetCount = 20;

        private static int cachedCount = -1;

        public static string FileStem(int id) => $"school_{id:D2}";

        public static string ResourcePath(int id) => $"{GameConstants.SchoolLevelsResourcesPath}/{FileStem(id)}";

        public static int Count
        {
            get
            {
                if (cachedCount >= 0) return cachedCount;

                int found = 0;
                for (int i = 1; i <= TargetCount + 4; i++)
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
            if (data != null) data.books = SchoolBookResolver.Resolve(data);
            if (data != null && !SchoolLevelSolver.AllCellsInBounds(data)) return null;
            return data;
        }
    }
}
