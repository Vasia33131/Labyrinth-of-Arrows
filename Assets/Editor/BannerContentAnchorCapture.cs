using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Пишет текущие экранные прямоугольники BannerContent якорями (offset = 0).
    /// Не вызывает ApplyShopLayout / ScreenLayoutBuilder и не пересобирает иерархию.
    /// </summary>
    public static class BannerContentAnchorCapture
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string FlagPath = "Library/unpuzzle_capture_banner_anchors.flag";
        private const string LogPath = "Library/unpuzzle_capture_banner_anchors.log";

        [InitializeOnLoadMethod]
        private static void AutoCaptureIfFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            EditorApplication.delayCall += RunFlaggedCapture;
        }

        [MenuItem("Tools/Unpuzzle/Save BannerContent Anchors", priority = 11)]
        public static void CaptureBothScenesMenu()
        {
            CaptureBothScenes();
        }

        private static void RunFlaggedCapture()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlaggedCapture;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= CaptureAfterPlay;
                EditorApplication.playModeStateChanged += CaptureAfterPlay;
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — запишу якоря BannerContent.");
                return;
            }

            if (!File.Exists(FlagPath)) return;
            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            CaptureBothScenes();
        }

        private static void CaptureAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= CaptureAfterPlay;
            EditorApplication.delayCall += RunFlaggedCapture;
        }

        public static void CaptureBothScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — якоря пишутся только в Edit Mode.");
                WriteLog("FAIL Play Mode");
                return;
            }

            try { File.WriteAllText(LogPath, "START\n", Encoding.UTF8); }
            catch { /* ignore */ }

            Scene active = SceneManager.GetActiveScene();
            string startPath = active.IsValid() ? active.path : "";

            var done = new HashSet<string>();
            if (IsTargetScene(startPath))
            {
                CaptureOpenScene();
                done.Add(startPath);
            }

            CaptureScenePath(MenuScenePath, done);
            CaptureScenePath(GameScenePath, done);

            if (!string.IsNullOrEmpty(startPath)
                && File.Exists(startPath)
                && SceneManager.GetActiveScene().path != startPath)
            {
                EditorSceneManager.OpenScene(startPath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            WriteLog("DONE scenes=" + done.Count);
            Debug.Log("[Unpuzzle] Якоря BannerContent записаны. Сцены: " + string.Join(", ", done));
        }

        private static bool IsTargetScene(string path)
        {
            return path == MenuScenePath || path == GameScenePath;
        }

        private static void CaptureScenePath(string path, HashSet<string> done)
        {
            if (done.Contains(path)) return;
            if (!File.Exists(path))
            {
                WriteLog("MISS " + path);
                return;
            }

            Scene current = SceneManager.GetActiveScene();
            if (!current.IsValid() || current.path != path)
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            CaptureOpenScene();
            done.Add(path);
        }

        private static void CaptureOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Unpuzzle] Нет открытой сцены для якорей.");
                WriteLog("FAIL no scene");
                return;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден в " + scene.name);
                WriteLog("FAIL no canvas " + scene.name);
                return;
            }

            Transform content = canvas.transform.Find(BannerSafeArea.ContentName);
            if (content == null) content = canvas.transform;

            var activated = new List<GameObject>();
            ActivateForCapture(Object.FindObjectOfType<ShopPanel>(true), activated);
            ActivateForCapture(Object.FindObjectOfType<SettingsPanel>(true), activated);
            ActivateForCapture(Object.FindObjectOfType<PlayModePanel>(true), activated);
            ActivateForCapture(Object.FindObjectOfType<SchoolTutorialPanel>(true), activated);
            ActivateForCapture(Object.FindObjectOfType<DailyHintsPanel>(true), activated);
            ActivateNamed(content, "WinPanel", activated);
            ActivateNamed(content, "LosePanel", activated);

            Canvas.ForceUpdateCanvases();
            var contentRect = content as RectTransform;
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            int converted = RectAnchorCapture.ConvertTree(content);

            for (int i = activated.Count - 1; i >= 0; i--)
            {
                if (activated[i] != null)
                    activated[i].SetActive(false);
            }

            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string msg = scene.name + " converted=" + converted
                + " root=" + content.name
                + " bannerContent=" + (content.name == BannerSafeArea.ContentName);
            WriteLog(msg);
            Debug.Log("[Unpuzzle] " + msg);
        }

        private static void ActivateForCapture(Component panel, List<GameObject> activated)
        {
            if (panel == null) return;
            if (panel.gameObject.activeSelf) return;
            panel.gameObject.SetActive(true);
            activated.Add(panel.gameObject);

            Canvas.ForceUpdateCanvases();
            var root = panel.transform as RectTransform;
            if (root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static void ActivateNamed(Transform root, string name, List<GameObject> activated)
        {
            if (root == null) return;
            Transform named = root.Find(name);
            if (named == null) named = AuthoredUi.FindDeep(root, name);
            if (named == null || named.gameObject.activeSelf) return;
            named.gameObject.SetActive(true);
            activated.Add(named.gameObject);
        }

        private static void WriteLog(string line)
        {
            try
            {
                File.AppendAllText(LogPath, line + "\n", Encoding.UTF8);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
