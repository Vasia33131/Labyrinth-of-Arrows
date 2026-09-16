using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Подключает арт из Assets/Art/Sprites к уже собранным сценам и префабам.
    /// Не пересобирает иерархию и не перезаписывает существующие PNG.
    /// </summary>
    public static class UnpuzzleArtApply
    {
        private const string SpritesFolder = "Assets/Art/Sprites";
        private const string NewSpritesFolder = "Assets/Art/Sprites/New";
        private const string LibraryPath = "Assets/Resources/ArtLibrary.asset";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string CharacterPrefabPath = "Assets/Prefabs/UI/Character.prefab";

        [MenuItem("Tools/Unpuzzle/Apply Art Sprites", priority = 8)]
        public static void ApplyArtMenu()
        {
            ConfigureImportedSprites();
            FillLibraryFromNewSprites();
            ApplyToOpenOrPath(MenuScenePath, BindMainMenuScene);
            ApplyToOpenOrPath(GameScenePath, BindGameScene);
            BindCharacterPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Unpuzzle] Арт подключён к MainMenu, GameScene и Character.");
        }

        public static void ConfigureImportedSprites()
        {
            string[] files = ListSpriteFiles();
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace("\\", "/");
                if (path.EndsWith(".meta")) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "icon_gear" && File.Exists(SpritesFolder + "/icon_gear 1.png")) continue;
                if (name == "arrow_placeholder" || name == "square_placeholder") continue;
                if (path.IndexOf("/New/", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && !IsNewUiSprite(name))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.alphaIsTransparency = true;
                settings.filterMode = FilterMode.Bilinear;
                settings.spriteMeshType = SpriteMeshType.FullRect;

                if (name == "ui_rounded")
                    settings.spriteBorder = new Vector4(56f, 56f, 56f, 56f);
                else if (name == "ui_capsule" || name == "btn")
                    settings.spriteBorder = GuessCapsuleBorder(path);
                else if (name == "ui_card")
                    settings.spriteBorder = new Vector4(80f, 80f, 80f, 120f);
                else if (name == "slider_track")
                    settings.spriteBorder = new Vector4(151f, 0f, 151f, 0f);
                else if (name == "slider_fill")
                    settings.spriteBorder = new Vector4(116f, 0f, 116f, 0f);
                else if (name == "bp_strip_bg")
                    settings.spriteBorder = new Vector4(28f, 28f, 28f, 28f);
                else
                    settings.spriteBorder = Vector4.zero;

                if (IsSchoolObjectSprite(name))
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    settings.spritePivot = new Vector2(0.5f, 0.5f);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteBorder = Vector4.zero;
                }

                importer.SetTextureSettings(settings);
                if (IsSchoolObjectSprite(name))
                {
                    importer.spritePixelsPerUnit = 128;
                    importer.spritePivot = new Vector2(0.5f, 0.5f);
                }

                importer.SaveAndReimport();
            }
        }

        public static void FillLibraryFromNewSprites()
        {
            ArtLibrary art = LoadLibrary();
            if (art == null) return;

            AssignIfFound(ref art.bpStripBg, NewSpritesFolder + "/bp_strip_bg.png");
            AssignIfFound(ref art.bpSlotFree, NewSpritesFolder + "/bp_slot_free.png");
            AssignIfFound(ref art.bpSlotPremium, NewSpritesFolder + "/bp_slot_premium.png");
            AssignIfFound(ref art.bpSlotLocked, NewSpritesFolder + "/bp_slot_locked.png");
            AssignIfFound(ref art.iconCheck, NewSpritesFolder + "/icon_check.png");
            AssignIfFound(ref art.iconCoin, NewSpritesFolder + "/icon_coin.png");
            AssignIfFound(ref art.iconCoinsStack, NewSpritesFolder + "/icon_coins_stack.png");
            AssignIfFound(ref art.slotShopSelected, NewSpritesFolder + "/slot_shop_selected.png");
            AssignIfFound(ref art.schoolBook, NewSpritesFolder + "/school_book.png");
            AssignIfFound(ref art.schoolBookIcon, NewSpritesFolder + "/school_book_icon.png");
            AssignIfFound(ref art.schoolPointerPivot, NewSpritesFolder + "/school_pointer_pivot.png");
            AssignIfFound(ref art.schoolPointerBody, NewSpritesFolder + "/school_pointer_body.png");
            AssignIfFound(ref art.schoolBackpack, NewSpritesFolder + "/school_backpack.png");
            EditorUtility.SetDirty(art);
        }

        public static void BindOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == MenuScenePath) BindMainMenuScene();
            else if (scene.path == GameScenePath) BindGameScene();
        }

        private static void ApplyToOpenOrPath(string scenePath, System.Action bind)
        {
            Scene active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == scenePath;
            Scene scene = alreadyOpen ? active : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bind();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BindMainMenuScene()
        {
            SceneCanvasSetup.PrepareOpenScene();
        }

        private static void BindGameScene()
        {
            SceneCanvasSetup.PrepareOpenScene();
        }

        private static void BindCharacterPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null) return;

            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CharacterView>();
                ArtLibrary art = LoadLibrary();
                if (view != null && art != null)
                {
                    view.idleSprite = art.characterIdle;
                    view.happySprite = art.characterHappy;
                    view.sadSprite = art.characterSad;
                    var display = root.GetComponent<Image>();
                    if (display == null) display = root.AddComponent<Image>();
                    display.sprite = art.characterIdle;
                    display.color = Color.white;
                    display.preserveAspect = true;
                    display.raycastTarget = false;
                    view.display = display;
                    if (view.body != null) view.body.gameObject.SetActive(false);
                    if (view.head != null) view.head.gameObject.SetActive(false);
                    if (view.armLeft != null) view.armLeft.gameObject.SetActive(false);
                    if (view.armRight != null) view.armRight.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ArtLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
        }

        private static string[] ListSpriteFiles()
        {
            var files = new List<string>();
            AddSpriteFiles(files, SpritesFolder);
            AddSpriteFiles(files, NewSpritesFolder);
            return files.ToArray();
        }

        private static void AddSpriteFiles(List<string> files, string folder)
        {
            if (!Directory.Exists(folder)) return;
            string[] found = Directory.GetFiles(folder);
            for (int i = 0; i < found.Length; i++)
                files.Add(found[i]);
        }

        private static void AssignIfFound(ref Sprite slot, string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) slot = sprite;
        }

        private static bool IsNewUiSprite(string name)
        {
            return name == "bp_strip_bg"
                || name == "bp_slot_free"
                || name == "bp_slot_premium"
                || name == "bp_slot_locked"
                || name == "icon_check"
                || name == "icon_coin"
                || name == "icon_coins_stack"
                || name == "slot_shop_selected"
                || IsSchoolObjectSprite(name);
        }

        private static bool IsSchoolObjectSprite(string name)
        {
            return name == "school_book"
                || name == "school_book_icon"
                || name == "school_pointer_pivot"
                || name == "school_pointer_body"
                || name == "school_backpack";
        }

        private static Vector4 GuessCapsuleBorder(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return new Vector4(48f, 8f, 48f, 8f);
            float insetX = Mathf.Max(24f, tex.width * 0.28f);
            float insetY = Mathf.Max(24f, tex.height * 0.32f);
            return new Vector4(insetX, insetY, insetX, insetY);
        }
    }
}
