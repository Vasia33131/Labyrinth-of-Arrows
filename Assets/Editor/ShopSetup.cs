using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Ставит кнопку «Магазин» на MainMenu и оверлей ShopOverlay на MainMenu и GameScene.
    /// Меню: Tools/Unpuzzle/Setup Shop
    /// </summary>
    public static class ShopSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Unpuzzle/Setup Shop", priority = 8)]
        public static void SetupShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Setup Shop.");
                return;
            }

            CharacterSkinSetup.EnsureAsset();
            BackgroundCatalogSetup.EnsureAsset();
            EmbedIntoScene(GameScenePath, false);
            EmbedIntoScene(MenuScenePath, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] Магазин: кнопка на MainMenu, оверлей на MainMenu и GameScene.");
        }

        [MenuItem("Tools/Unpuzzle/Bake Shop Layout", priority = 9)]
        public static void BakeShopLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Bake Shop Layout.");
                return;
            }

            BakeShopLayoutInScene(MenuScenePath, true);
            BakeShopLayoutInScene(GameScenePath, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] Вёрстка магазина записана в MainMenu и GameScene.");
        }

        private static void BakeShopLayoutInScene(string scenePath, bool menu)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("[Unpuzzle] Нет сцены " + scenePath + ".");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EmbedIntoLoadedScene(menu);
            ShopPanel panel = Object.FindObjectOfType<ShopPanel>(true);
            if (panel != null)
            {
                panel.EnsureSkinListForEditor();
                CharacterSkinSetup.EnsureAuthoredSkinSlots(panel);
                panel.ApplyAuthoredShopLayout();
                EditorUtility.SetDirty(panel);
                EditorUtility.SetDirty(panel.gameObject);
                panel.gameObject.SetActive(menu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void EmbedIntoOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            bool menu = scene.path == MenuScenePath || Object.FindObjectOfType<MainMenuController>() != null;
            EmbedIntoLoadedScene(menu);
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
                Debug.LogError("[Unpuzzle] Canvas не найден — магазин не поставлен.");
                return;
            }

            ShopPanel panel = ShopPanel.EnsureOnCanvas(canvas.transform);
            Button shopButton = null;
            if (menu)
                shopButton = ShopPanel.EnsureShopButton(canvas.transform, true);
            else
                RemoveGameSceneShopAndGold(canvas.transform);

            if (panel == null || (menu && shopButton == null))
            {
                Debug.LogError("[Unpuzzle] ShopOverlay / ShopButton не собрались.");
                return;
            }

            panel.RepairAuthoredHierarchy();
            CharacterSkinSetup.EnsureAuthoredSkinSlots(panel);
            panel.ApplyAuthoredShopLayout();
            panel.gameObject.SetActive(menu);

            if (menu)
            {
                var menuUi = Object.FindObjectOfType<MainMenuController>();
                if (menuUi != null)
                {
                    menuUi.shopPanel = panel;
                    menuUi.shopButton = shopButton;
                    EditorUtility.SetDirty(menuUi);
                }
            }
            else
            {
                var ui = Object.FindObjectOfType<UIManager>();
                if (ui != null)
                {
                    ui.shop = panel;
                    ui.shopButton = null;
                    ui.goldText = null;
                    ui.goldCounter = null;
                    EditorUtility.SetDirty(ui);
                }
            }

            var hub = canvas.GetComponent<SceneCanvas>();
            if (hub != null)
            {
                hub.shop = panel;
                hub.shopButton = menu ? shopButton : null;
                hub.shopDimmer = FindImage(panel.transform, "Dimmer");
                hub.shopCard = FindImage(panel.transform, "Card");
                hub.shopCloseXButton = panel.closeXButton;
                EditorUtility.SetDirty(hub);
            }

            EditorUtility.SetDirty(panel);
            if (shopButton != null) EditorUtility.SetDirty(shopButton);
        }

        public static void RemoveGameSceneShopAndGold(Transform canvas)
        {
            if (canvas == null) return;

            Button shopButton = ShopPanel.FindShopButton(canvas);
            if (shopButton != null)
                Object.DestroyImmediate(shopButton.gameObject);

            GoldCounterView[] counters = canvas.GetComponentsInChildren<GoldCounterView>(true);
            for (int i = 0; i < counters.Length; i++)
            {
                GoldCounterView view = counters[i];
                if (view == null) continue;
                if (view.GetComponentInParent<ShopPanel>(true) != null) continue;
                Object.DestroyImmediate(view.gameObject);
            }

            Transform namedGold = canvas.Find(GoldCounterView.ObjectName);
            if (namedGold != null && namedGold.GetComponentInParent<ShopPanel>(true) == null)
                Object.DestroyImmediate(namedGold.gameObject);
        }

        private static Image FindImage(Transform root, string name)
        {
            if (root == null) return null;
            Transform found = root.Find(name);
            return found != null ? found.GetComponent<Image>() : null;
        }
    }
}
