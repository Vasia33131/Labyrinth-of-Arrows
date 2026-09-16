using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Автосборка варианта A «коридоры HUD».
    /// Меню: Tools → Unpuzzle → Setup Visible Layout (HUD corridors)
    /// </summary>
    public static class VisibleLayoutSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string LayoutVersionKey = "Unpuzzle.VisibleLayoutVersion";
        private const int LayoutVersion = 1;

        [InitializeOnLoadMethod]
        private static void AutoEmbedAfterCompile()
        {
            EditorPrefs.SetInt(LayoutVersionKey, LayoutVersion);
        }

        [MenuItem("Tools/Unpuzzle/Setup Visible Layout (HUD corridors)", priority = 4)]
        public static void SetupVisibleLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Setup Visible Layout (HUD corridors).");
                return;
            }

            if (!File.Exists(GameScenePath))
            {
                Debug.Log("[Unpuzzle] GameScene нет — сначала полная сборка, затем коридоры HUD.");
                UnpuzzleSetup.RebuildUnpuzzleScene();
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ScreenLayoutSetup.EmbedIntoOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(LayoutVersionKey, LayoutVersion);
            SceneCanvasSetup.PrepareOpenScene();
            Validate();
            Debug.Log("[Unpuzzle] Visible Layout готов. Кнопки и спрайты правятся на UI Canvas.");
        }

        public static void Validate()
        {
            var canvas = Object.FindObjectOfType<UIManager>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] UI Canvas / UIManager не найден.");
                return;
            }

            Transform canvasT = canvas.transform;
            var topHud = canvasT.Find("TopHud") as RectTransform;
            var bottomHud = canvasT.Find("BottomHud") as RectTransform;
            var screenRoot = canvasT.Find("ScreenRoot") as RectTransform;

            if (topHud == null) Debug.LogError("[Unpuzzle] TopHud должен быть прямым ребёнком Canvas.");
            if (bottomHud == null) Debug.LogError("[Unpuzzle] BottomHud должен быть прямым ребёнком Canvas.");
            if (screenRoot == null) Debug.LogError("[Unpuzzle] ScreenRoot не собран.");

            if (topHud != null && topHud.parent != canvasT)
                Debug.LogError("[Unpuzzle] TopHud не должен быть ребёнком ScreenRoot.");
            if (bottomHud != null && bottomHud.parent != canvasT)
                Debug.LogError("[Unpuzzle] BottomHud не должен быть ребёнком ScreenRoot.");
            if (screenRoot != null && (screenRoot.Find("TopHud") != null || screenRoot.Find("BottomHud") != null))
                Debug.LogError("[Unpuzzle] HUD-коридоры оказались внутри ScreenRoot.");

            RequireChild(topHud, "MenuButton");
            RequireChild(topHud, "LevelBadge");
            RequireChild(topHud, "SettingsButton");
            RequireChild(bottomHud, "HintButton");
            RequireChild(bottomHud, "UndoButton");
            RequireChild(bottomHud, "ExtraMoveButton");
            RequireChild(bottomHud, "RestartButton");

            GameObject progress = FindInactive("ProgressRow");
            if (progress != null && progress.activeSelf)
                Debug.LogError("[Unpuzzle] ProgressRow должен быть выключен.");

            RequireHiddenLabel(screenRoot, "CharacterZone");
            RequireHiddenLabel(screenRoot, "BoardZone");
            RequireHiddenLabel(screenRoot, "CandyZone");

            var builder = Object.FindObjectOfType<ScreenLayoutBuilder>();
            if (builder == null)
            {
                Debug.LogError("[Unpuzzle] ScreenLayoutBuilder не найден.");
            }
            else
            {
                if (builder.topHud == null || builder.bottomHud == null || builder.boardPlayArea == null)
                    Debug.LogError("[Unpuzzle] На ScreenLayoutBuilder не проставлены ссылки коридоров.");
                if (builder.levelBadge == null || builder.menuButton == null || builder.settingsButton == null)
                    Debug.LogError("[Unpuzzle] TOP HUD кнопки не привязаны к builder.");
                if (builder.hintButton == null || builder.undoButton == null || builder.restartButton == null)
                    Debug.LogError("[Unpuzzle] BOTTOM HUD кнопки не привязаны к builder.");
            }

            var badge = topHud != null ? topHud.Find("LevelBadge") : null;
            if (badge == null || badge.GetComponent<LevelProgressView>() == null)
                Debug.LogError("[Unpuzzle] LevelBadge без LevelProgressView.");

            if (canvas.progressView == null || canvas.progressView.gameObject.name != "LevelBadge")
                Debug.LogError("[Unpuzzle] UIManager.progressView должен быть на LevelBadge.");

            var fitter = Object.FindObjectOfType<BoardCameraFitter>();
            if (fitter == null || fitter.boardPlayArea == null)
                Debug.LogError("[Unpuzzle] BoardCameraFitter.boardPlayArea не проставлен.");

            var gm = Object.FindObjectOfType<GameManager>();
            if (gm == null || gm.screenLayout == null || gm.orbitDirector == null)
                Debug.LogError("[Unpuzzle] GameManager потерял ссылки layout / orbit.");
        }

        private static void RequireChild(Transform parent, string name)
        {
            if (parent == null)
            {
                Debug.LogError($"[Unpuzzle] Нет родителя для {name}.");
                return;
            }

            if (parent.Find(name) == null)
                Debug.LogError($"[Unpuzzle] {name} должен быть ребёнком {parent.name}.");
        }

        private static void RequireHiddenLabel(Transform screenRoot, string zoneName)
        {
            if (screenRoot == null) return;
            Transform zone = screenRoot.Find(zoneName);
            if (zone == null) return;
            Transform label = zone.Find("Label");
            if (label != null && label.gameObject.activeSelf)
                Debug.LogError($"[Unpuzzle] Подпись зоны {zoneName} должна быть выключена.");
        }

        private static GameObject FindInactive(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != name) continue;
                if (all[i].hideFlags != HideFlags.None) continue;
                if (all[i].gameObject.scene.IsValid()) return all[i].gameObject;
            }

            return null;
        }
    }
}
