using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Ставит счётчик золота на MainMenu.
    /// Меню: Tools/Unpuzzle/Setup Gold HUD
    /// </summary>
    public static class GoldHudSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Unpuzzle/Setup Gold HUD", priority = 7)]
        public static void SetupGoldHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Setup Gold HUD.");
                return;
            }

            EmbedIntoScene(MenuScenePath, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] Счётчик золота стоит на MainMenu.");
        }

        public static void EmbedIntoOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            bool menu = scene.path == MenuScenePath || Object.FindObjectOfType<MainMenuController>() != null;
            if (!menu) return;
            EmbedIntoLoadedScene(true);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EmbedIntoScene(string scenePath, bool menu)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Unpuzzle] Нет сцены {scenePath}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EmbedIntoLoadedScene(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EmbedIntoLoadedScene(bool menu)
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден — золото не поставлено.");
                return;
            }

            GoldCounterView view = GoldCounterView.EnsureOnCanvas(canvas.transform, menu);
            if (view == null || view.label == null)
            {
                Debug.LogError("[Unpuzzle] GoldText не собрался.");
                return;
            }

            if (menu)
            {
                var menuUi = Object.FindObjectOfType<MainMenuController>();
                if (menuUi != null)
                {
                    menuUi.goldText = view.label;
                    menuUi.goldCounter = view;
                    EditorUtility.SetDirty(menuUi);
                }
            }
            else
            {
                var ui = Object.FindObjectOfType<UIManager>();
                if (ui != null)
                {
                    ui.goldText = view.label;
                    ui.goldCounter = view;
                    EditorUtility.SetDirty(ui);
                }
            }

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(view.label);
        }
    }
}
