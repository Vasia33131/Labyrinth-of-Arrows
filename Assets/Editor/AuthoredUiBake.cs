using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Один раз собирает авторскую UI-иерархию в MainMenu и GameScene.
    /// В Play эти объекты только биндятся, не создаются.
    /// </summary>
    public static class AuthoredUiBake
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string AutoFlagPath = "Library/unpuzzle_bake_authored_ui.flag";

        [InitializeOnLoadMethod]
        private static void AutoBakeIfFlagged()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeForBake;
            EditorApplication.playModeStateChanged += OnPlayModeForBake;

            if (!File.Exists(AutoFlagPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += RunFlaggedBake;
        }

        private static void OnPlayModeForBake(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists(AutoFlagPath))
                EditorApplication.delayCall += RunFlaggedBake;
        }

        private static void RunFlaggedBake()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlaggedBake;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(AutoFlagPath)) return;

            try { File.Delete(AutoFlagPath); }
            catch { /* ignore */ }

            BakeAuthoredUi();
        }

        [MenuItem("Tools/Unpuzzle/Bake Authored UI", priority = 2)]
        public static void BakeAuthoredUi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Bake Authored UI.");
                return;
            }

            CharacterSkinSetup.EnsureAsset();
            BackgroundCatalogSetup.EnsureAsset();

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty)
                EditorSceneManager.SaveScene(active);

            BakeMainMenu();
            BakeGameScene();
            OpenScene(MenuScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] Bake Authored UI сохранён: MainMenu.unity и GameScene.unity.");
        }

        private static void BakeMainMenu()
        {
            Scene scene = OpenScene(MenuScenePath);
            if (!scene.IsValid()) return;

            ShopSetup.EmbedIntoOpenScene();
            GoldHudSetup.EmbedIntoOpenScene();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден на MainMenu.");
                return;
            }

            PlayModePanel play = PlayModePanel.EnsureOnCanvas(canvas.transform);
            if (play != null) play.gameObject.SetActive(false);

            DailyHintsPanel dailyHints = DailyHintsPanel.EnsureOnCanvas(canvas.transform);
            if (dailyHints != null)
            {
                dailyHints.AuthorForEditor();
                dailyHints.gameObject.SetActive(false);
            }

            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null)
            {
                shop.EnsureSkinListForEditor();
                CharacterSkinSetup.EnsureAuthoredSkinSlots(shop);
                shop.ApplyAuthoredShopLayout();
                shop.gameObject.SetActive(true);
            }

            var menu = Object.FindObjectOfType<MainMenuController>();
            if (menu != null)
            {
                menu.playModePanel = play;
                if (menu.shopPanel == null) menu.shopPanel = shop;
                if (menu.shopButton == null) menu.shopButton = ShopPanel.FindShopButton(canvas.transform);
                if (menu.goldCounter == null) menu.goldCounter = GoldCounterView.FindOn(canvas.transform);
                if (menu.goldText == null && menu.goldCounter != null) menu.goldText = menu.goldCounter.label;
                if (menu.dailyHintsPanel == null) menu.dailyHintsPanel = dailyHints;
                EditorUtility.SetDirty(menu);
            }

            BakeSettingsPanels();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            LogMenu(canvas.transform, play, shop, dailyHints);
        }

        private static void BakeGameScene()
        {
            Scene scene = OpenScene(GameScenePath);
            if (!scene.IsValid()) return;

            ShopSetup.EmbedIntoOpenScene();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Unpuzzle] Canvas не найден на GameScene.");
                return;
            }

            SchoolTutorialPanel tutorial = SchoolTutorialPanel.FindOn(canvas.transform);
            if (tutorial != null) tutorial.gameObject.SetActive(false);
            ShopSetup.RemoveGameSceneShopAndGold(canvas.transform);

            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null)
            {
                shop.EnsureSkinListForEditor();
                CharacterSkinSetup.EnsureAuthoredSkinSlots(shop);
                shop.ApplyAuthoredShopLayout();
                shop.gameObject.SetActive(false);
            }

            DailyHintsPanel dailyHints = DailyHintsPanel.EnsureOnCanvas(canvas.transform);
            if (dailyHints != null) dailyHints.gameObject.SetActive(false);

            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null)
            {
                UnpuzzleSetup.EnsureExtraMoveHud(ui);
                if (ui.shop == null) ui.shop = shop;
                if (ui.dailyHintsPanel == null) ui.dailyHintsPanel = dailyHints;
                ui.shopButton = null;
                ui.goldCounter = null;
                ui.goldText = null;
                EditorUtility.SetDirty(ui);
            }

            BakeSettingsPanels();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            LogGame(canvas.transform, tutorial, shop, dailyHints);
        }

        private static void BakeSettingsPanels()
        {
            SettingsPanel[] panels = Object.FindObjectsOfType<SettingsPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].AuthorForEditor();
                EditorUtility.SetDirty(panels[i]);
            }
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

        private static void LogMenu(Transform canvas, PlayModePanel play, ShopPanel shop, DailyHintsPanel dailyHints)
        {
            int skins = CountNamed(shop != null ? shop.skinList : null, "SkinSlot_");
            int backgrounds = CountNamed(shop != null ? shop.backgroundList : null, "BackgroundSlot_");
            Debug.Log("[Unpuzzle] MainMenu bake: PlayMode=" + (play != null)
                + " DailyHints=" + (dailyHints != null)
                + " skins=" + skins
                + " backgrounds=" + backgrounds
                + " Character=" + (Object.FindObjectOfType<CharacterView>(true) != null)
                + " Gold=" + (GoldCounterView.FindOn(canvas) != null));
        }

        private static void LogGame(Transform canvas, SchoolTutorialPanel tutorial, ShopPanel shop, DailyHintsPanel dailyHints)
        {
            int skins = CountNamed(shop != null ? shop.skinList : null, "SkinSlot_");
            int backgrounds = CountNamed(shop != null ? shop.backgroundList : null, "BackgroundSlot_");
            Debug.Log("[Unpuzzle] GameScene bake: Tutorial=" + (tutorial != null)
                + " Shop=" + (shop != null)
                + " DailyHints=" + (dailyHints != null)
                + " skins=" + skins
                + " backgrounds=" + backgrounds
                + " Character=" + (Object.FindObjectOfType<CharacterView>(true) != null)
                + " Gold=" + (GoldCounterView.FindOn(canvas) != null));
        }

        private static int CountNamed(Transform parent, string prefix)
        {
            if (parent == null) return 0;
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name.StartsWith(prefix)) count++;
            }

            return count;
        }
    }
}
