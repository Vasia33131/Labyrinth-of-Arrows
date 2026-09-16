using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Вешает SceneCanvas на UI Canvas, проставляет ссылки и спрайты.
    /// После этого сцену можно править руками: якоря и PNG в инспекторе.
    /// </summary>
    public static class SceneCanvasSetup
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string VersionKey = "Unpuzzle.SceneCanvasAuthoring";
        private const int Version = 1;

        [InitializeOnLoadMethod]
        private static void AutoPrepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
                PrepareBothScenes();
            };
        }

        [MenuItem("Tools/Unpuzzle/Prepare Scene Canvas (edit sprites)", priority = 3)]
        public static void PrepareBothScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Prepare Scene Canvas.");
                return;
            }

            PrepareScene(MenuScenePath);
            PrepareScene(GameScenePath);
            EditorPrefs.SetInt(VersionKey, Version);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] SceneCanvas готов. Открой UI Canvas / Canvas и перетащи спрайты в слоты.");
        }

        public static void PrepareOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == MenuScenePath) PrepareMainMenu();
            else if (scene.path == GameScenePath) PrepareGameScene();
            else return;

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void PrepareScene(string path)
        {
            if (!File.Exists(path)) return;

            Scene active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == path;
            Scene scene = alreadyOpen ? active : EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            if (path == MenuScenePath) PrepareMainMenu();
            else PrepareGameScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void PrepareMainMenu()
        {
            var menu = Object.FindObjectOfType<MainMenuController>();
            if (menu == null) return;

            SceneCanvas hub = EnsureHub(menu.gameObject);
            hub.playButton = menu.playButton;
            hub.settingsButton = menu.settingsButton;
            hub.shopButton = menu.shopButton;
            hub.settings = menu.settingsPanel;
            hub.shop = menu.shopPanel;
            hub.background = FindImage(menu.transform, "Background");

            if (menu.settingsPanel != null)
            {
                hub.settingsCloseButton = menu.settingsPanel.closeButton;
                hub.settingsCloseXButton = menu.settingsPanel.closeXButton;
                hub.settingsDimmer = FindImage(menu.settingsPanel.transform, "Dimmer");
                hub.settingsCard = FindImage(menu.settingsPanel.transform, "Card");
            }

            if (menu.shopPanel != null)
            {
                hub.shopCloseXButton = menu.shopPanel.closeXButton;
                hub.shopDimmer = FindImage(menu.shopPanel.transform, "Dimmer");
                hub.shopCard = FindImage(menu.shopPanel.transform, "Card");
            }

            FillSpritesFromLibrary(hub);
            hub.lockAuthoredLayout = true;
            hub.lockAuthoredArt = true;
            hub.ApplySprites();
            EditorUtility.SetDirty(hub);
        }

        private static void PrepareGameScene()
        {
            var ui = Object.FindObjectOfType<UIManager>();
            if (ui == null) return;

            DestroyEmptyDuplicate("TopHud");
            DestroyEmptyDuplicate("BottomHud");

            SceneCanvas hub = EnsureHub(ui.gameObject);
            var builder = Object.FindObjectOfType<ScreenLayoutBuilder>();
            hub.layoutBuilder = builder;
            if (builder != null)
            {
                hub.config = builder.config;
                builder.applyRuntimeLayout = false;
                builder.hostCanvas = ui.GetComponent<Canvas>();
                EditorUtility.SetDirty(builder);
            }

            hub.menuButton = ui.menuButton;
            hub.settingsButton = ui.settingsButton;
            hub.shopButton = ui.shopButton;
            hub.hintButton = ui.hintButton;
            hub.undoButton = ui.undoButton;
            hub.extraMoveButton = ui.extraMoveButton;
            hub.restartButton = ui.restartButton;
            hub.nextLevelButton = ui.nextLevelButton;
            hub.winRestartButton = ui.winRestartButton;
            hub.loseRestartButton = ui.loseRestartButton;
            hub.loseExtraMoveButton = ui.loseExtraMoveButton;
            hub.settings = ui.settings;
            hub.shop = ui.shop;
            hub.settingsCloseButton = ui.settings != null ? ui.settings.closeButton : ui.settingsCloseButton;
            hub.settingsCloseXButton = ui.settings != null ? ui.settings.closeXButton : null;
            hub.shopCloseXButton = ui.shop != null ? ui.shop.closeXButton : null;
            hub.winPanelImage = ui.winPanel != null ? ui.winPanel.GetComponent<Image>() : null;
            hub.losePanelImage = ui.losePanel != null ? ui.losePanel.GetComponent<Image>() : null;
            hub.levelBadge = ui.progressView != null
                ? ui.progressView.GetComponent<Image>()
                : FindImage(ui.transform, "LevelBadge");

            if (ui.settings != null)
            {
                hub.settingsDimmer = FindImage(ui.settings.transform, "Dimmer");
                hub.settingsCard = FindImage(ui.settings.transform, "Card");
            }

            if (ui.shop != null)
            {
                hub.shopDimmer = FindImage(ui.shop.transform, "Dimmer");
                hub.shopCard = FindImage(ui.shop.transform, "Card");
            }

            hub.character = Object.FindObjectOfType<CharacterView>(true);
            if (hub.character != null)
            {
                if (hub.character.display == null) hub.character.display = hub.character.GetComponent<Image>();
                if (hub.character.display == null)
                {
                    var image = hub.character.gameObject.AddComponent<Image>();
                    image.color = Color.white;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    hub.character.display = image;
                    EditorUtility.SetDirty(hub.character);
                }

                hub.characterImage = hub.character.display;
            }

            hub.candyBox = Object.FindObjectOfType<CandyBoxView>(true);
            if (hub.candyBox != null) hub.bowlImage = hub.candyBox.bowlImage;

            hub.background = EnsureBoardBackground(ui.transform);

            FillSpritesFromLibrary(hub);
            hub.lockAuthoredLayout = true;
            hub.lockAuthoredArt = true;
            hub.ApplySprites();
            EditorUtility.SetDirty(hub);
            EditorUtility.SetDirty(ui);
        }

        private static SceneCanvas EnsureHub(GameObject canvasGo)
        {
            var hub = canvasGo.GetComponent<SceneCanvas>();
            if (hub == null) hub = canvasGo.AddComponent<SceneCanvas>();
            return hub;
        }

        private static void FillSpritesFromLibrary(SceneCanvas hub)
        {
            ArtLibrary art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
            if (art == null) art = ArtLibrary.Current;
            if (art == null || hub == null) return;

            Assign(ref hub.bgMenu, art.bgMenu);
            Assign(ref hub.bgBoard, art.bgBoard);
            Assign(ref hub.bgWin, art.bgWin);
            Assign(ref hub.bgLose, art.bgLose);
            Assign(ref hub.bgDimmer, art.bgDimmer);
            Assign(ref hub.uiCard, art.uiCard);
            Assign(ref hub.uiCapsule, art.uiCapsule);
            Assign(ref hub.btn, art.btn);
            Assign(ref hub.btnHint, art.btnHint);
            Assign(ref hub.btnUndo, art.btnUndo);
            Assign(ref hub.btnRestart, art.btnRestart);
            Assign(ref hub.iconBack, art.iconBack);
            Assign(ref hub.iconGear, art.iconGear);
            Assign(ref hub.iconHint, art.iconHint);
            Assign(ref hub.iconUndo, art.iconUndo);
            Assign(ref hub.iconRestart, art.iconRestart);
            Assign(ref hub.iconClose, art.iconClose);
            Assign(ref hub.iconExtraMove, art.iconExtraMove);
            Assign(ref hub.badgeCount, art.badgeCount);
            Assign(ref hub.characterIdle, art.characterIdle);
            Assign(ref hub.characterHappy, art.characterHappy);
            Assign(ref hub.characterSad, art.characterSad);
            Assign(ref hub.bowlEmpty, art.bowl0);
            Assign(ref hub.bowl25, art.bowl25);
            Assign(ref hub.bowl50, art.bowl50);
            Assign(ref hub.bowl75, art.bowl75);
            Assign(ref hub.bowlFull, art.bowl100);
            Assign(ref hub.candyIcon, art.candyIcon);
            Assign(ref hub.iconSound, art.iconSound);
            Assign(ref hub.iconSoundOff, art.iconSoundOff);
            Assign(ref hub.iconMusic, art.iconMusic);
            Assign(ref hub.iconMusicOff, art.iconMusicOff);
        }

        private static void Assign(ref Sprite slot, Sprite value)
        {
            if (slot == null && value != null) slot = value;
        }

        private static Image EnsureBoardBackground(Transform canvas)
        {
            if (canvas == null) return null;

            Transform existing = canvas.Find("BoardBackground");
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                var go = new GameObject("BoardBackground", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                go.transform.SetAsFirstSibling();
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                image = go.AddComponent<Image>();
                image.raycastTarget = false;
            }
            else
            {
                existing.SetAsFirstSibling();
            }

            return image;
        }

        private static Image FindImage(Transform root, string name)
        {
            if (root == null) return null;
            Transform found = root.Find(name);
            if (found != null) return found.GetComponent<Image>();

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == name)
                    return images[i];
            }

            return null;
        }

        private static void DestroyEmptyDuplicate(string name)
        {
            GameObject[] all = Object.FindObjectsOfType<GameObject>();
            GameObject keep = null;
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || go.name != name) continue;
                if (go.transform.childCount > 0)
                {
                    keep = go;
                    break;
                }
            }

            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || go.name != name || go == keep) continue;
                if (go.transform.childCount == 0) Object.DestroyImmediate(go);
            }
        }
    }
}
