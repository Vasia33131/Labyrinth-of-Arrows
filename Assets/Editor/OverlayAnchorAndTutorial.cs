using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Пишет текущую вёрстку оверлеев якорями и кладёт школьный туториал в сцену.
    /// Работает только с уже открытой сценой — не переоткрывает файлы с диска.
    /// </summary>
    public static class OverlayAnchorAndTutorial
    {
        private const string FlagPath = "Library/unpuzzle_capture_anchors_and_tutorial.flag";
        private const string ShowFlagPath = "Library/unpuzzle_show_school_tutorial.flag";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            EditorApplication.delayCall += RunFlagged;
            EditorApplication.delayCall += RunShowFlagged;
        }

        [MenuItem("Tools/Unpuzzle/Save Overlay Anchors And Show School Tutorial", priority = 12)]
        public static void RunMenu()
        {
            CaptureAndShowTutorial();
        }

        [MenuItem("Tools/Unpuzzle/Show School Tutorial On Scene", priority = 12)]
        public static void ShowOnSceneMenu()
        {
            ShowTutorialOnOpenScene();
        }

        private static void RunFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= AfterPlay;
                EditorApplication.playModeStateChanged += AfterPlay;
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — запишу якоря и вынесу панель школы.");
                return;
            }

            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            CaptureAndShowTutorial();
        }

        private static void RunShowFlagged()
        {
            if (!File.Exists(ShowFlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunShowFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — вынесу обучение школы в сцену.");
                return;
            }

            try { File.Delete(ShowFlagPath); }
            catch { /* ignore */ }

            ShowTutorialOnOpenScene();
        }

        private static void ShowTutorialOnOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Unpuzzle] Нет открытой сцены.");
                return;
            }

            SchoolTutorialPanel tutorial = Object.FindObjectOfType<SchoolTutorialPanel>(true);
            if (tutorial == null)
            {
                Canvas canvas = Object.FindObjectOfType<Canvas>();
                if (canvas != null)
                    tutorial = SchoolTutorialPanel.FindOn(canvas.transform);
            }

            if (tutorial == null)
            {
                Debug.LogWarning("[Unpuzzle] SchoolTutorialOverlay не найден в открытой сцене.");
                return;
            }

            PlayModePanel play = Object.FindObjectOfType<PlayModePanel>(true);
            if (play != null) play.gameObject.SetActive(false);
            SettingsPanel settings = Object.FindObjectOfType<SettingsPanel>(true);
            if (settings != null) settings.gameObject.SetActive(false);
            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null) shop.gameObject.SetActive(false);
            DailyHintsPanel dailyHints = Object.FindObjectOfType<DailyHintsPanel>(true);
            if (dailyHints != null) dailyHints.gameObject.SetActive(false);

            tutorial.ShowForEditor(0);
            Selection.activeGameObject = tutorial.gameObject;
            EditorUtility.SetDirty(tutorial);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Unpuzzle] Обучение школы лежит в сцене (страница 1/5). Двигай Card, Scheme, Body, PageIndex и NextButton.");
        }

        private static void AfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AfterPlay;
            EditorApplication.delayCall += RunFlagged;
        }

        private static void CaptureAndShowTutorial()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Unpuzzle] Нет открытой сцены.");
                return;
            }

            Canvas.ForceUpdateCanvases();

            int converted = 0;
            converted += CapturePanel(Object.FindObjectOfType<PlayModePanel>(true));
            converted += CapturePanel(Object.FindObjectOfType<SettingsPanel>(true));
            converted += CapturePanel(Object.FindObjectOfType<ShopPanel>(true));
            converted += CapturePanel(Object.FindObjectOfType<DailyHintsPanel>(true));

            SchoolTutorialPanel tutorial = EmbedTutorial();
            if (tutorial != null)
                EditorUtility.SetDirty(tutorial);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Unpuzzle] Якоря записаны (" + converted + " RectTransform). Панель школы: "
                + (tutorial != null ? tutorial.gameObject.name : "нет")
                + ". Сцена " + scene.name + " сохранена.");
        }

        private static int CapturePanel(Component panel)
        {
            if (panel == null) return 0;

            bool wasActive = panel.gameObject.activeSelf;
            if (!wasActive) panel.gameObject.SetActive(true);

            Canvas.ForceUpdateCanvases();
            var root = panel.transform as RectTransform;
            if (root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);

            int converted = RectAnchorCapture.ConvertTree(panel.transform);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(panel.gameObject);

            if (!wasActive) panel.gameObject.SetActive(false);
            return converted;
        }

        private static SchoolTutorialPanel EmbedTutorial()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден.");
                return null;
            }

            if (canvas.gameObject.scene.name == GameConstants.MainMenuSceneName)
                return null;

            SchoolTutorialPanel tutorial = SchoolTutorialPanel.FindOn(canvas.transform);
            if (tutorial == null) return null;

            PlayModePanel play = Object.FindObjectOfType<PlayModePanel>(true);
            if (play != null) play.gameObject.SetActive(false);
            SettingsPanel settings = Object.FindObjectOfType<SettingsPanel>(true);
            if (settings != null) settings.gameObject.SetActive(false);
            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null) shop.gameObject.SetActive(false);
            DailyHintsPanel dailyHints = Object.FindObjectOfType<DailyHintsPanel>(true);
            if (dailyHints != null) dailyHints.gameObject.SetActive(false);

            tutorial.ShowForEditor(0);
            tutorial.transform.SetAsLastSibling();
            Selection.activeGameObject = tutorial.gameObject;
            EditorUtility.SetDirty(tutorial);
            return tutorial;
        }
    }
}
