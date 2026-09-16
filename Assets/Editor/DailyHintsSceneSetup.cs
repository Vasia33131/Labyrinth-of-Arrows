using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Кладёт панель ежедневных бонусов в открытую сцену и пишет её вёрстку якорями.
    /// </summary>
    public static class DailyHintsSceneSetup
    {
        private const string FlagPath = "Library/unpuzzle_show_daily_hints.flag";
        private const string CaptureFlagPath = "Library/unpuzzle_capture_daily_hints_anchors.flag";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            EditorApplication.delayCall += TryRunFlagged;
            if (File.Exists(CaptureFlagPath))
                EditorApplication.delayCall += TryCaptureAnchors;
        }

        [MenuItem("Tools/Unpuzzle/Put Daily Hints On Scene", priority = 11)]
        public static void RunMenu()
        {
            ShowOnOpenScene();
        }

        [MenuItem("Tools/Unpuzzle/Put Daily Hints On GameScene", priority = 11)]
        public static void RunGameSceneMenu()
        {
            ShowOnGameScene();
        }

        [MenuItem("Tools/Unpuzzle/Save Daily Hints Anchors", priority = 11)]
        public static void SaveAnchorsMenu()
        {
            CaptureAnchors();
        }

        public static void TryRunFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — вынесу ежедневные бонусы в сцену.");
                return;
            }

            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            ShowOnOpenScene();
        }

        public static void TryCaptureAnchors()
        {
            if (!File.Exists(CaptureFlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryCaptureAnchors;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — запишу якоря ежедневных бонусов.");
                return;
            }

            try { File.Delete(CaptureFlagPath); }
            catch { /* ignore */ }

            CaptureAnchors();
        }

        private static void CaptureAnchors()
        {
            DailyHintsPanel panel = Object.FindObjectOfType<DailyHintsPanel>(true);
            if (panel == null)
            {
                Debug.LogError("[Unpuzzle] DailyHintsOverlay не найден в открытой сцене.");
                return;
            }

            panel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var root = panel.transform as RectTransform;
            if (root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);

            int converted = RectAnchorCapture.ConvertTree(panel.transform);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(panel.gameObject);

            Scene scene = panel.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Transform card = panel.transform.Find("Card");
            var cardRt = card as RectTransform;
            Debug.Log("[Unpuzzle] Ежедневные бонусы записаны якорями (" + converted
                + " RectTransform). Card="
                + (cardRt != null ? cardRt.rect.size.ToString("F0") : "?")
                + " в сцене " + scene.name + ".");
        }

        private static void ShowOnOpenScene()
        {
            Scene scene = EnsureMenuOrGameScene();
            PutOnScene(scene);
        }

        private static void ShowOnGameScene()
        {
            const string gamePath = "Assets/Scenes/GameScene.unity";
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty)
                EditorSceneManager.SaveScene(active);

            Scene scene = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
            PutOnScene(scene);
        }

        private static void PutOnScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Unpuzzle] Нет открытой сцены.");
                return;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден в открытой сцене.");
                return;
            }

            HideOtherOverlays();

            DailyHintsPanel panel = DailyHintsPanel.EnsureOnCanvas(canvas.transform);
            if (panel == null)
            {
                Debug.LogError("[Unpuzzle] Не удалось создать DailyHintsOverlay.");
                return;
            }

            panel.AuthorForEditor();
            panel.transform.SetAsLastSibling();
            EditorUtility.SetDirty(panel);

            var menu = Object.FindObjectOfType<MainMenuController>();
            if (menu != null)
            {
                menu.dailyHintsPanel = panel;
                EditorUtility.SetDirty(menu);
            }

            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null)
            {
                ui.dailyHintsPanel = panel;
                EditorUtility.SetDirty(ui);
            }

            Selection.activeGameObject = panel.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Unpuzzle] Ежедневные бонусы лежат в сцене «" + scene.name
                + "». Двигай Card, Title, Message, Balance, AdButton, CloseButton, CloseXButton.");
        }

        private static Scene EnsureMenuOrGameScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded
                && (scene.name == GameConstants.MainMenuSceneName
                    || scene.name == GameConstants.GameSceneName))
                return scene;

            const string menuPath = "Assets/Scenes/MainMenu.unity";
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty)
                EditorSceneManager.SaveScene(active);

            return EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
        }

        private static void HideOtherOverlays()
        {
            PlayModePanel play = Object.FindObjectOfType<PlayModePanel>(true);
            if (play != null) play.gameObject.SetActive(false);
            SettingsPanel settings = Object.FindObjectOfType<SettingsPanel>(true);
            if (settings != null) settings.gameObject.SetActive(false);
            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null) shop.gameObject.SetActive(false);
            SchoolTutorialPanel tutorial = Object.FindObjectOfType<SchoolTutorialPanel>(true);
            if (tutorial != null) tutorial.gameObject.SetActive(false);
        }
    }
}
