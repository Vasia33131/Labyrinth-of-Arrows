using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Оставляет SchoolTutorialOverlay только в GameScene. Дубликат в MainMenu удаляет.
    /// GameScene-оверлей не пересобирает.
    /// </summary>
    public static class KeepGameSchoolTutorial
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string FlagPath = "Library/unpuzzle_keep_game_tutorial.flag";
        private const string LogPath = "Library/unpuzzle_keep_game_tutorial.log";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            EditorApplication.delayCall += RunFlagged;
        }

        [MenuItem("Tools/Unpuzzle/Keep GameScene School Tutorial Only", priority = 14)]
        public static void RunMenu()
        {
            RemoveMenuDuplicate();
        }

        private static void RunFlagged()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= AfterPlay;
                EditorApplication.playModeStateChanged += AfterPlay;
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — оставлю туториал только в GameScene.");
                return;
            }

            if (!File.Exists(FlagPath)) return;
            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            RemoveMenuDuplicate();
        }

        private static void AfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AfterPlay;
            EditorApplication.delayCall += RunFlagged;
        }

        public static void RemoveMenuDuplicate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode.");
                WriteLog("FAIL Play Mode");
                return;
            }

            try { File.WriteAllText(LogPath, "START\n"); }
            catch { /* ignore */ }

            Scene active = SceneManager.GetActiveScene();
            string startPath = active.IsValid() ? active.path : "";
            if (active.IsValid() && active.isDirty)
                EditorSceneManager.SaveScene(active);

            Scene menu = OpenScene(MenuScenePath);
            if (!menu.IsValid())
            {
                WriteLog("FAIL no MainMenu");
                return;
            }

            int removed = 0;
            SchoolTutorialPanel[] panels = Object.FindObjectsOfType<SchoolTutorialPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                SchoolTutorialPanel panel = panels[i];
                if (panel == null) continue;
                Object.DestroyImmediate(panel.gameObject);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            WriteLog("MainMenu removed=" + removed);

            if (startPath == GameScenePath)
                OpenScene(GameScenePath);

            Debug.Log("[Unpuzzle] SchoolTutorialOverlay только в GameScene. С MainMenu снято: " + removed);
            WriteLog("DONE");
        }

        private static Scene OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("[Unpuzzle] Нет сцены " + path);
                return default;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path == path) return active;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void WriteLog(string line)
        {
            try { File.AppendAllText(LogPath, line + "\n"); }
            catch { /* ignore */ }
        }
    }
}
