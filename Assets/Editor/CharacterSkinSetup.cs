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
    /// Создаёт Resources/CharacterSkinCatalog.asset и дописывает SkinSlot_* в сцены.
    /// Меню: Tools/Unpuzzle/Setup Character Skins.
    /// </summary>
    public static class CharacterSkinSetup
    {
        public const string AssetPath = "Assets/Resources/CharacterSkinCatalog.asset";
        public const string NewSpritesFolder = "Assets/Art/Sprites/New";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string AutoFlagPath = "Library/unpuzzle_setup_character_skins.flag";

        [InitializeOnLoadMethod]
        private static void AutoSetupIfFlagged()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeForCatalog;
            EditorApplication.playModeStateChanged += OnPlayModeForCatalog;

            if (!File.Exists(AutoFlagPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorApplication.delayCall += RunFlaggedSetup;
        }

        private static void OnPlayModeForCatalog(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => LogCatalogSprites("Play");

            if (state == PlayModeStateChange.EnteredEditMode && File.Exists(AutoFlagPath))
                EditorApplication.delayCall += RunFlaggedSetup;
        }

        private static void RunFlaggedSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlaggedSetup;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(AutoFlagPath)) return;

            try { File.Delete(AutoFlagPath); }
            catch { /* ignore */ }

            SetupCharacterSkins();
        }

        [MenuItem("Tools/Unpuzzle/Setup Character Skins", priority = 9)]
        public static void SetupCharacterSkins()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Setup Character Skins.");
                return;
            }

            CharacterSkinCatalog catalog = EnsureAsset();
            if (catalog == null)
            {
                Debug.LogError("[Unpuzzle] CharacterSkinCatalog не создался.");
                return;
            }

            EnsureSkinSlotsInScenes();
            LogCatalogSprites("после setup");
            Debug.Log("[Unpuzzle] Каталог скинов готов: " + AssetPath + ". Слоты SkinSlot_* стоят в MainMenu и GameScene.");
        }

        public static CharacterSkinCatalog EnsureAsset()
        {
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkinCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterSkinCatalog>();
                catalog.ResetToBuiltin();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.ResetToBuiltin();
            catalog.KeepOnlyBuiltinSkins();
            ApplySpritesFromNewFolder(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CharacterSkinCatalog.InvalidateCache();
            return catalog;
        }

        public static void EnsureAuthoredSkinSlots(ShopPanel panel)
        {
            if (panel == null) return;

            panel.EnsureSkinListForEditor();
            if (panel.skinList == null) return;

            IReadOnlyList<CharacterSkinData> skins = CharacterSkinCatalog.Current.Skins;
            var existing = new List<CharacterSkinSlotView>();
            panel.skinList.GetComponentsInChildren(true, existing);

            for (int i = 0; i < skins.Count; i++)
            {
                CharacterSkinData skin = skins[i];
                if (skin == null || string.IsNullOrEmpty(skin.id)) continue;
                if (HasSlot(existing, skin.id)) continue;

                CharacterSkinSlotView created = CharacterSkinSlotView.Create(panel.skinList, skin);
                if (created != null) existing.Add(created);
            }

            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(panel.skinList.gameObject);
        }

        private static void EnsureSkinSlotsInScenes()
        {
            EmbedIntoScene(GameScenePath, false);
            EmbedIntoScene(MenuScenePath, true);
            AssetDatabase.SaveAssets();
        }

        private static void EmbedIntoScene(string scenePath, bool menu)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("[Unpuzzle] Нет сцены " + scenePath + ".");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ShopSetup.EmbedIntoOpenScene();

            ShopPanel panel = Object.FindObjectOfType<ShopPanel>(true);
            EnsureAuthoredSkinSlots(panel);
            EnsureCharacterViewInOpenScene(menu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureCharacterViewInOpenScene(bool menu)
        {
            CharacterView view = Object.FindObjectOfType<CharacterView>(true);
            if (view == null && menu)
                view = CreateMenuCharacter();
            if (view == null) return;

            if (view.display == null)
            {
                var image = view.GetComponent<Image>();
                if (image == null) image = view.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
                view.display = image;
            }

            var hub = Object.FindObjectOfType<SceneCanvas>();
            if (hub != null)
            {
                hub.character = view;
                hub.characterImage = view.display;
                EditorUtility.SetDirty(hub);
            }

            EditorUtility.SetDirty(view);
        }

        private static CharacterView CreateMenuCharacter()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("Character", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 310f);
            rt.sizeDelta = new Vector2(220f, 220f);
            rt.localScale = Vector3.one;

            Transform play = canvas.transform.Find("PlayButton");
            if (play != null) go.transform.SetSiblingIndex(play.GetSiblingIndex());

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.type = Image.Type.Simple;

            ArtLibrary art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
            if (art != null && art.characterIdle != null)
                image.sprite = art.characterIdle;

            var view = go.AddComponent<CharacterView>();
            view.root = rt;
            view.display = image;
            if (art != null)
            {
                view.idleSprite = art.characterIdle;
                view.happySprite = art.characterHappy;
                view.sadSprite = art.characterSad;
            }

            return view;
        }

        private static bool HasSlot(List<CharacterSkinSlotView> slots, string id)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterSkinSlotView slot = slots[i];
                if (slot == null) continue;
                if (slot.skinId == id) return true;
                if (slot.gameObject.name == "SkinSlot_" + id) return true;
            }

            return false;
        }

        private static void ApplySpritesFromNewFolder(CharacterSkinCatalog catalog)
        {
            if (catalog == null) return;

            AssignSprites(catalog, CharacterSkinCatalog.WitchId,
                "skin_witch_idle", "skin_witch_happy", "skin_witch_sad");
            AssignSprites(catalog, CharacterSkinCatalog.DarkElfId,
                "skin_dark_elf_idle", "skin_dark_elf_happy", "skin_dark_elf_sad");
            AssignSprites(catalog, CharacterSkinCatalog.CyberpunkId,
                "skin_cyberpunk_idle", "skin_cyberpunk_happy", "skin_cyberpunk_sad");
            AssignSprites(catalog, CharacterSkinCatalog.TeacherId,
                "skin_teacher_idle", "skin_teacher_happy", "skin_teacher_sad");

            CharacterSkinData classic = catalog.Get(GameConstants.DefaultSkinId);
            ArtLibrary art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
            if (classic != null && art != null)
            {
                classic.idle = art.characterIdle;
                classic.happy = art.characterHappy;
                classic.sad = art.characterSad;
            }
        }

        private static void AssignSprites(CharacterSkinCatalog catalog, string id,
            string idleName, string happyName, string sadName)
        {
            CharacterSkinData skin = catalog.Get(id);
            if (skin == null) return;

            Sprite idle = LoadSprite(idleName);
            Sprite happy = LoadSprite(happyName);
            Sprite sad = LoadSprite(sadName);
            if (idle != null) skin.idle = idle;
            if (happy != null) skin.happy = happy;
            if (sad != null) skin.sad = sad;
        }

        private static Sprite LoadSprite(string fileNameWithoutExtension)
        {
            string path = NewSpritesFolder + "/" + fileNameWithoutExtension + ".png";
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Unpuzzle] Нет спрайта скина: " + path);
                return null;
            }

            EnsureSpriteImporter(path);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            string[] guids = AssetDatabase.FindAssets(fileNameWithoutExtension + " t:Sprite");
            for (int i = 0; i < guids.Length; i++)
            {
                string found = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(found)) continue;
                if (Path.GetFileNameWithoutExtension(found) != fileNameWithoutExtension) continue;
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(found);
                if (sprite != null) return sprite;
            }

            return null;
        }

        private static void EnsureSpriteImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single)
                return;

            ConfigureSpriteImporter(path);
        }

        private static void ConfigureSpriteImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

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
            settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        public static void LogCatalogSprites(string reason)
        {
            CharacterSkinCatalog.InvalidateCache();
            CharacterSkinCatalog loaded = Resources.Load<CharacterSkinCatalog>(CharacterSkinCatalog.ResourceName);
            if (loaded == null)
            {
                Debug.LogError("[Unpuzzle] CharacterSkinCatalog не грузится из Resources (" + reason + ").");
                return;
            }

            IReadOnlyList<CharacterSkinData> skins = loaded.Skins;
            int missing = 0;
            for (int i = 0; i < skins.Count; i++)
            {
                CharacterSkinData skin = skins[i];
                if (skin == null) continue;
                bool ok = skin.idle != null && skin.happy != null && skin.sad != null;
                if (!ok) missing++;
                Debug.Log("[Unpuzzle] Скин " + skin.id + " idle=" + (skin.idle != null) +
                    " happy=" + (skin.happy != null) + " sad=" + (skin.sad != null) + " (" + reason + ")");
            }

            if (missing == 0)
                Debug.Log("[Unpuzzle] Каталог скинов с диска, все спрайты на месте (" + reason + ").");
            else
                Debug.LogError("[Unpuzzle] В каталоге пустые спрайты: " + missing + " (" + reason + ").");
        }
    }
}
