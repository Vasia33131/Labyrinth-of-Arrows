using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unpuzzle.EditorTools
{
    public static class LevelGeneratorMenu
    {
        public const string LevelsFolder = "Assets/Resources/Levels";

        [MenuItem("Tools/Unpuzzle/Generate 100 Levels", priority = 10)]
        public static void Generate100Levels()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Levels");

            string absDir = Path.Combine(Application.dataPath, "Resources/Levels");
            Directory.CreateDirectory(absDir);

            int fallbacks = 0;
            try
            {
                for (int i = 1; i <= LevelCatalog.TargetCount; i++)
                {
                    EditorUtility.DisplayProgressBar("Unpuzzle", $"Генерация уровня {i}/{LevelCatalog.TargetCount}", i / (float)LevelCatalog.TargetCount);

                    LevelData data;
                    bool usedFallback = false;
                    if (i == 1) data = DemoLevel.Create();
                    else data = LevelFactory.Generate(i, 0, out usedFallback);
                    if (usedFallback) fallbacks++;

                    string path = Path.Combine(absDir, LevelCatalog.FileStem(i) + ".json");
                    File.WriteAllText(path, data.ToPrettyJson());
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LevelCatalog.InvalidateCount();
            AssetDatabase.Refresh();

            LogLevelSnapshot(1);
            LogLevelSnapshot(20);
            LogLevelSnapshot(80);
            Debug.Log($"[Unpuzzle] Сгенерировано {LevelCatalog.TargetCount} уровней в {LevelsFolder}. Fallback: {fallbacks}.");
        }

        private static void LogLevelSnapshot(int id)
        {
            string path = Path.Combine(Application.dataPath, "Resources/Levels", LevelCatalog.FileStem(id) + ".json");
            if (!File.Exists(path)) return;
            LevelData data = LevelData.FromJson(File.ReadAllText(path));
            if (data == null || data.arrows == null) return;
            int locked = 0;
            int parents = 0;
            for (int i = 0; i < data.arrows.Length; i++)
            {
                string[] p = data.arrows[i].GetLockParents();
                if (p.Length > 0) locked++;
                parents += p.Length;
            }

            Debug.Log($"[Unpuzzle] level {id}: arrows={data.arrows.Length} grid={data.gridWidth}x{data.gridHeight} moves={data.movesLimit} locked={locked} parentRefs={parents}");
        }

        [MenuItem("Tools/Unpuzzle/Setup Levels (Step 5)", priority = 6)]
        public static void SetupLevels()
        {
            if (Directory.GetFiles(Path.Combine(Application.dataPath, "Resources/Levels"), "level_*.json").Length < LevelCatalog.TargetCount)
                Generate100Levels();

            var gameManager = Object.FindObjectOfType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning("[Unpuzzle] Открой Assets/Scenes/GameScene.unity и повтори Setup Levels (Step 5).");
                return;
            }

            gameManager.autoSpawnTestLayout = false;
            gameManager.startLevelIndex = 1;
            gameManager.useSavedProgress = true;
            if (gameManager.levelGenerator == null)
                gameManager.levelGenerator = gameManager.GetComponent<LevelGenerator>();

            var generator = gameManager.levelGenerator;
            if (generator != null)
            {
                generator.useTestGrid = false;
                if (generator.levelRoot == null) generator.levelRoot = gameManager.levelRoot;
            }

            EditorUtility.SetDirty(gameManager);
            if (generator != null) EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[Unpuzzle] ШАГ 5: Play грузит Resources/Levels/level_01.json. Next Level → следующий индекс.");
        }

        public const string SchoolFolder = "Assets/Resources/Levels/School";

        [MenuItem("Tools/Unpuzzle/Generate School Levels", priority = 11)]
        public static void GenerateSchoolLevels()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Levels"))
                AssetDatabase.CreateFolder("Assets/Resources", "Levels");
            if (!AssetDatabase.IsValidFolder(SchoolFolder))
                AssetDatabase.CreateFolder("Assets/Resources/Levels", "School");

            string absDir = Path.Combine(Application.dataPath, "Resources/Levels/School");
            Directory.CreateDirectory(absDir);

            int fallbacks = 0;
            int failed = 0;
            try
            {
                for (int i = 1; i <= SchoolLevelCatalog.TargetCount; i++)
                {
                    EditorUtility.DisplayProgressBar("Unpuzzle School", $"Школа {i}/{SchoolLevelCatalog.TargetCount}", i / (float)SchoolLevelCatalog.TargetCount);

                    bool usedFallback;
                    LevelData data = LevelFactory.GenerateSchool(i, 0, out usedFallback);
                    if (usedFallback) fallbacks++;

                    string err = null;
                    if (data == null || !LevelSolver.ValidateSchoolShape(data, i, out err))
                    {
                        data = TryDecorateCampaignSeed(i, out usedFallback);
                        if (usedFallback) fallbacks++;
                    }

                    if (data == null || !LevelSolver.ValidateSchoolShape(data, i, out err))
                    {
                        failed++;
                        Debug.LogError($"[Unpuzzle] school_{i:D2} не прошёл солвер: {err}");
                        continue;
                    }

                    string path = Path.Combine(absDir, SchoolLevelCatalog.FileStem(i) + ".json");
                    File.WriteAllText(path, data.ToPrettyJson());
                    int bookN = data.books != null ? data.books.Length : 0;
                    int pointerN = data.pointers != null ? data.pointers.Length : 0;
                    int packN = data.backpacks != null ? data.backpacks.Length : 0;
                    Debug.Log($"[Unpuzzle] school {i}: arrows={data.arrows.Length} books={bookN} pointers={pointerN} backpacks={packN} moves={data.movesLimit} solvable=1");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            SchoolLevelCatalog.InvalidateCount();
            AssetDatabase.Refresh();
            Debug.Log($"[Unpuzzle] Школа: записано в {SchoolFolder}. Fallback: {fallbacks}, ошибок: {failed}.");
        }

        private static LevelData TryDecorateCampaignSeed(int schoolId, out bool usedFallback)
        {
            usedFallback = true;
            int campaignId = 76 + (schoolId * 3 + 1) % 25;
            string path = Path.Combine(Application.dataPath, "Resources/Levels", LevelCatalog.FileStem(campaignId) + ".json");
            if (!File.Exists(path)) return null;

            LevelData data = LevelData.FromJson(File.ReadAllText(path));
            if (data == null) return null;
            data.id = schoolId;
            data.buttons = System.Array.Empty<LevelButtonData>();
            data.pointers = null;
            data.backpacks = null;
            data.books = null;
            if (!LevelData.SanitizeBounds(data)) return null;

            for (int seed = 1; seed <= 24; seed++)
            {
                LevelData clone = LevelData.FromJson(data.ToPrettyJson());
                clone.id = schoolId;
                if (LevelFactory.TryDecorateSchool(clone, schoolId, schoolId * 917 + seed) &&
                    LevelSolver.ValidateSchoolShape(clone, schoolId, out _))
                    return clone;
            }

            return null;
        }
    }

    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        private static int previewIndex = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ШАГ 5 — уровни", EditorStyles.boldLabel);
            previewIndex = EditorGUILayout.IntSlider("Preview / Load index", previewIndex, 1, LevelCatalog.TargetCount);

            if (GUILayout.Button("Generate 100 Levels"))
                LevelGeneratorMenu.Generate100Levels();

            var generator = (LevelGenerator)target;
            if (GUILayout.Button($"Preview Level {previewIndex}"))
            {
                if (generator.arrowPrefab == null)
                {
                    Debug.LogWarning("[LevelGenerator] Назначь Arrow Prefab.");
                    return;
                }

                generator.Build(previewIndex);
                EditorUtility.SetDirty(generator);
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            if (Application.isPlaying && GUILayout.Button($"Play Level {previewIndex}"))
            {
                if (GameManager.Instance != null) GameManager.Instance.LoadLevel(previewIndex);
            }
        }
    }
}
