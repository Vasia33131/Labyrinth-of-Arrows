using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Собирает префаб Arrow, GameScene, MainMenu и HUD одним кликом.
    /// Меню: Tools/Unpuzzle/Rebuild Unpuzzle Scene
    /// </summary>
    public static class UnpuzzleSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ArrowPrefabPath = "Assets/Prefabs/Arrow.prefab";
        private const string SettingsPrefabPath = "Assets/Prefabs/UI/SettingsPanel.prefab"; // legacy, удаляется при конвертации
        private const string HeadSpritePath = "Assets/Art/Sprites/arrow_head.png";
        private const string RoundedSpritePath = "Assets/Art/Sprites/ui_rounded.png";
        private const string PathMaterialPath = "Assets/Art/Materials/ArrowPath.mat";
        private const string ColorPriorityPath = "Assets/Settings/ColorPriorityConfig.asset";
        private const string DemoLevelPath = "Assets/Resources/Levels/level_01.json";
        private const string RebuildVersionKey = "Unpuzzle.RebuildVersion";
        private const int RebuildVersion = 1;
        private const int PixelsPerUnit = 128;
        private const int DefaultMovesLimit = 8;

        private static readonly string FlagPath = Path.Combine("Library", "unpuzzle_rebuild.flag");
        private static readonly string SetupMenuFlagPath = Path.Combine("Library", "unpuzzle_setup_main_menu.flag");

        [InitializeOnLoadMethod]
        private static void AutoRebuildIfNeeded()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            bool setupMenu = File.Exists(SetupMenuFlagPath);
            if (setupMenu)
            {
                try { File.Delete(SetupMenuFlagPath); }
                catch { /* ignore */ }

                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += () =>
                    {
                        EditorApplication.delayCall += SetupMainMenu;
                    };
                    if (!File.Exists(FlagPath) && EditorPrefs.GetInt(RebuildVersionKey, 0) >= RebuildVersion)
                        return;
                }
            }

            bool flagged = File.Exists(FlagPath);
            if (flagged)
            {
                try { File.Delete(FlagPath); }
                catch { /* ignore */ }
            }

            bool stale = EditorPrefs.GetInt(RebuildVersionKey, 0) < RebuildVersion;
            if (!flagged && !stale) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorPrefs.SetInt(RebuildVersionKey, RebuildVersion);
            SessionState.SetBool("Unpuzzle.VerifyPlay", true);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    RebuildUnpuzzleScene();
                };
            };
        }

        private static int verifyFrames;
        private static bool verifyTicking;

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                TryRunPendingSetupMainMenu();

            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool("Unpuzzle.VerifyPlay", false)) return;
            SessionState.SetBool("Unpuzzle.VerifyPlay", false);
            verifyFrames = 0;
            if (verifyTicking) return;
            verifyTicking = true;
            EditorApplication.update += TickVerifyPlayMode;
        }

        internal static void TryRunPendingSetupMainMenu()
        {
            if (!File.Exists(SetupMenuFlagPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(SetupMenuFlagPath); }
            catch { /* ignore */ }
            EditorApplication.delayCall += SetupMainMenu;
        }

        private static void TickVerifyPlayMode()
        {
            verifyFrames++;
            if (verifyFrames < 10) return;

            EditorApplication.update -= TickVerifyPlayMode;
            verifyTicking = false;
            VerifyPlayMode();
        }

        private static void VerifyPlayMode()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[Unpuzzle] Play Mode: GameManager не поднялся.");
                return;
            }

            gm.LoadLevel(1);

            int free = 0;
            ArrowController firstFree = null;
            IReadOnlyList<ArrowController> arrows = gm.ActiveArrows;
            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow == null || arrow.IsRemoving) continue;
                if (!arrow.CanExit()) continue;
                free++;
                if (firstFree == null) firstFree = arrow;
            }

            Debug.Log($"[Unpuzzle] Play Mode: стрелок {gm.ArrowsLeft}, свободных {free}, ходов {gm.MovesLeft}.");
            if (firstFree == null)
            {
                Debug.LogError("[Unpuzzle] Play Mode: нет свободной стрелки в demo level_01.");
                return;
            }

            Debug.Log($"[Unpuzzle] Play Mode: тап по свободной '{firstFree.name}'.");
            gm.OnArrowTapped(firstFree);
        }

        [MenuItem("Tools/Unpuzzle/Setup Project", priority = 0)]
        public static void SetupAll()
        {
            RebuildUnpuzzleScene();
        }

        [MenuItem("Tools/Unpuzzle/Setup Main Menu", priority = 1)]
        public static void SetupMainMenu()
        {
            EnsureFolders();
            ApplyYandexPlayerSettings();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath) == null)
                CreateArtAssets();
            AssetDatabase.Refresh();

            BuildMainMenuScene();
            WireSettingsIntoGameScene();
            DeleteLegacySettingsPrefab();
            ConfigureMenuBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(MenuScenePath);
            GoldHudSetup.EmbedIntoOpenScene();
            ShopSetup.EmbedIntoOpenScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            ValidateMainMenuScene();
            Debug.Log("[Unpuzzle] MainMenu собран: сцена, SettingsOverlay как объект сцены, Build Settings (0=MainMenu, 1=GameScene).");
        }

        [MenuItem("Tools/Unpuzzle/Convert Settings Prefab To Scene Objects", priority = 9)]
        public static void ConvertSettingsToSceneObjects()
        {
            UnpackSettingsInScene(MenuScenePath);
            UnpackSettingsInScene(GameScenePath);
            DeleteLegacySettingsPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Unpuzzle] SettingsOverlay распакован в объекты сцены, префаб удалён.");
        }

        [MenuItem("Tools/Unpuzzle/Rebuild Unpuzzle Scene", priority = 1)]
        public static void RebuildUnpuzzleScene()
        {
            EnsureFolders();
            EnsureLayer(GameConstants.ArrowLayerName);
            ApplyYandexPlayerSettings();
            CreateArtAssets();
            WriteDemoLevel();
            DisableColorPriority();
            AssetDatabase.Refresh();

            GameObject arrowPrefab = CreateArrowPrefab();
            BuildMainMenuScene();
            BuildGameScene(arrowPrefab);
            DeleteLegacySettingsPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(GameScenePath);
            ScreenLayoutSetup.EmbedIntoOpenScene();
            GoldHudSetup.EmbedIntoOpenScene();
            ShopSetup.EmbedIntoOpenScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            ValidateBuiltScene();
            Debug.Log("[Unpuzzle] Сцена пересобрана: GameScene, три зоны (Персонаж / Поле / Конфеты), орбита стрелок.");

            if (SessionState.GetBool("Unpuzzle.VerifyPlay", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
        }

        [MenuItem("Tools/Unpuzzle/Setup Rules (Step 3)", priority = 2)]
        public static void SetupRules()
        {
            ColorPriorityConfig config = DisableColorPriority();
            var gameManager = Object.FindObjectOfType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning($"[Unpuzzle] Открой {GameScenePath} и повтори.");
                return;
            }

            gameManager.colorPriority = null;
            if (gameManager.movesLimit <= 0) gameManager.movesLimit = DefaultMovesLimit;
            EditorUtility.SetDirty(gameManager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeObject = config;
            Debug.Log("[Unpuzzle] Цветовой приоритет выключен (классический Unpuzzle).");
        }

        [MenuItem("Tools/Unpuzzle/Setup Bonuses (Step 4)", priority = 3)]
        public static void SetupBonuses()
        {
            var gameManager = Object.FindObjectOfType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning($"[Unpuzzle] Открой {GameScenePath} и повтори.");
                return;
            }

            var bonusSystem = gameManager.GetComponent<BonusSystem>();
            if (bonusSystem == null) bonusSystem = gameManager.gameObject.AddComponent<BonusSystem>();
            bonusSystem.gameManager = gameManager;
            gameManager.bonuses = bonusSystem;
            bonusSystem.hintCharges = 0;
            bonusSystem.undoCharges = 0;
            bonusSystem.extraMoveCharges = 0;
            if (gameManager.uiManager != null) gameManager.uiManager.bonuses = bonusSystem;
            EditorUtility.SetDirty(gameManager);
            EditorUtility.SetDirty(bonusSystem);
            EditorSceneManager.SaveOpenScenes();
        }

        // ------------------------------------------------------------ folders / settings

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Scripts", "Assets/Scripts/Core", "Assets/Scripts/Gameplay", "Assets/Scripts/UI",
                "Assets/Editor", "Assets/Prefabs", "Assets/Prefabs/UI", "Assets/Scenes",
                "Assets/Resources", "Assets/Resources/Levels",
                "Assets/Art", "Assets/Art/Sprites", "Assets/Art/Materials", "Assets/Settings"
            };

            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        private static void ApplyYandexPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
        }

        private static ColorPriorityConfig DisableColorPriority()
        {
            var config = AssetDatabase.LoadAssetAtPath<ColorPriorityConfig>(ColorPriorityPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ColorPriorityConfig>();
                AssetDatabase.CreateAsset(config, ColorPriorityPath);
            }

            config.rulesEnabled = false;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void WriteDemoLevel()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DemoLevelPath));
            File.WriteAllText(DemoLevelPath, DemoLevel.Create().ToPrettyJson());
            AssetDatabase.ImportAsset(DemoLevelPath);
            LevelCatalog.InvalidateCount();
        }

        private static void EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return;

            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        // ------------------------------------------------------------ art

        private static void CreateArtAssets()
        {
            WritePngIfMissing(HeadSpritePath, ArrowSpriteFactory.MakeTriangleTexture(PixelsPerUnit));
            WritePngIfMissing(RoundedSpritePath, MakeRoundedRectTexture(256, 56));
            WritePngIfMissing("Assets/Art/Sprites/icon_back.png", MakeChevronTexture(128, true));
            WritePngIfMissing("Assets/Art/Sprites/icon_gear.png", MakeGearTexture(128));
            WritePngIfMissing("Assets/Art/Sprites/icon_hint.png", MakeBulbTexture(128));
            WritePngIfMissing("Assets/Art/Sprites/icon_undo.png", MakeUndoTexture(128));
            WritePngIfMissing("Assets/Art/Sprites/icon_restart.png", MakeRestartTexture(128));

            ConfigureSpriteImporter(HeadSpritePath, Vector4.zero, new Vector2(0.5f, ArrowSpriteFactory.TrianglePivotY));
            ConfigureSpriteImporter(RoundedSpritePath, new Vector4(56, 56, 56, 56), new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter("Assets/Art/Sprites/icon_back.png", Vector4.zero, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter("Assets/Art/Sprites/icon_gear.png", Vector4.zero, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter("Assets/Art/Sprites/icon_hint.png", Vector4.zero, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter("Assets/Art/Sprites/icon_undo.png", Vector4.zero, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter("Assets/Art/Sprites/icon_restart.png", Vector4.zero, new Vector2(0.5f, 0.5f));
            UnpuzzleArtApply.ConfigureImportedSprites();

            CreatePathMaterial();
        }

        private static void WritePng(string path, Texture2D tex)
        {
            WritePngIfMissing(path, tex, overwrite: true);
        }

        private static void WritePngIfMissing(string path, Texture2D tex, bool overwrite = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!overwrite && File.Exists(path))
            {
                Object.DestroyImmediate(tex);
                return;
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureSpriteImporter(string path, Vector4 border, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = border;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Material CreatePathMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[Unpuzzle] Не найден шейдер для LineRenderer.");
                return null;
            }

            var mat = new Material(shader) { name = "ArrowPath" };
            AssetDatabase.CreateAsset(mat, PathMaterialPath);
            return mat;
        }

        private static Texture2D MakeRoundedRectTexture(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= radius && x < size - radius || y >= radius && y < size - radius;
                    if (!inside)
                    {
                        int cx = x < radius ? radius : size - radius - 1;
                        int cy = y < radius ? radius : size - radius - 1;
                        float dx = x - cx;
                        float dy = y - cy;
                        inside = dx * dx + dy * dy <= radius * radius;
                    }

                    pixels[y * size + x] = inside ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeChevronTexture(int size, bool left)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            float thickness = 0.10f;

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float px = left ? 1f - nx : nx;
                    float dist = Mathf.Abs(ny - 0.5f) - (px - 0.28f) * 0.95f;
                    bool inArm = Mathf.Abs(dist) < thickness && px > 0.28f && px < 0.78f;
                    pixels[y * size + x] = inArm ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeGearTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            float cx = 0.5f, cy = 0.5f;

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = nx - cx;
                    float dy = ny - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float tooth = 0.34f + 0.08f * Mathf.Max(0f, Mathf.Cos(ang * 6f));
                    bool ring = r < tooth && r > 0.16f;
                    pixels[y * size + x] = ring ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeBulbTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = nx - 0.5f;
                    float dy = ny - 0.58f;
                    bool bulb = dx * dx + dy * dy < 0.22f * 0.22f;
                    bool baseRect = ny > 0.18f && ny < 0.40f && Mathf.Abs(nx - 0.5f) < 0.12f;
                    pixels[y * size + x] = (bulb || baseRect) ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeUndoTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = nx - 0.52f;
                    float dy = ny - 0.48f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool arc = r > 0.22f && r < 0.32f && !(nx > 0.52f && ny > 0.48f);
                    bool head = ny > 0.62f && ny < 0.86f && nx > 0.18f && nx < 0.48f
                                && Mathf.Abs((ny - 0.74f) - (0.48f - nx) * 0.4f) < 0.10f;
                    pixels[y * size + x] = (arc || head) ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeRestartTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = nx - 0.5f;
                    float dy = ny - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = r > 0.22f && r < 0.32f && !(nx > 0.5f && ny > 0.5f);
                    bool head = nx > 0.50f && nx < 0.78f && ny > 0.50f && ny < 0.78f
                                && Mathf.Abs((ny - 0.58f) - (nx - 0.62f)) < 0.10f;
                    pixels[y * size + x] = (ring || head) ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------ prefab

        private static GameObject CreateArrowPrefab()
        {
            var go = new GameObject("Arrow");
            GameConstants.SetLayerSafe(go, GameConstants.ArrowLayerName);

            var pathGo = new GameObject("Path");
            pathGo.transform.SetParent(go.transform, false);
            var line = pathGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, Vector3.down * 0.42f);
            line.SetPosition(1, Vector3.up * 0.32f);
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 12;
            line.numCornerVertices = 12;
            line.startWidth = GameConstants.PathWidthInCells;
            line.endWidth = GameConstants.PathWidthInCells;
            line.sortingOrder = 10;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
            Color grey = ArrowController.ColorToUnityColor(ArrowColor.Grey);
            line.startColor = grey;
            line.endColor = grey;

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(go.transform, false);
            var head = headGo.AddComponent<SpriteRenderer>();
            head.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeadSpritePath);
            if (head.sprite == null) head.sprite = ArrowSpriteFactory.GetTriangle();
            head.color = grey;
            head.sortingOrder = 12;
            AssignUrpSpriteMaterial(head);

            var arrow = go.AddComponent<ArrowController>();
            arrow.pathRenderer = line;
            arrow.headRenderer = head;
            arrow.pathMaterial = line.sharedMaterial;
            arrow.direction = ArrowDirection.Up;
            arrow.RebuildVisual();

            if (go.GetComponent<Rigidbody2D>() != null)
                Object.DestroyImmediate(go.GetComponent<Rigidbody2D>());
            if (go.GetComponent<Collider2D>() != null)
                Object.DestroyImmediate(go.GetComponent<Collider2D>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, ArrowPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void AssignUrpSpriteMaterial(SpriteRenderer renderer)
        {
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (unlit != null) renderer.sharedMaterial = unlit;
            else if (lit != null) renderer.sharedMaterial = lit;
        }

        // -------------------------------------------------------------- scenes

        private static void BuildGameScene(GameObject arrowPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera cam = CreateCamera();
            var levelRoot = new GameObject("LevelRoot");

            var gmGo = new GameObject("GameManager");
            var gameManager = gmGo.AddComponent<GameManager>();
            var levelGenerator = gmGo.AddComponent<LevelGenerator>();
            var inputHandler = gmGo.AddComponent<InputHandler>();
            var bonusSystem = gmGo.AddComponent<BonusSystem>();

            UIManager uiManager = CreateGameCanvas();

            ArrowController arrowController = arrowPrefab != null ? arrowPrefab.GetComponent<ArrowController>() : null;

            gameManager.levelRoot = levelRoot.transform;
            gameManager.levelGenerator = levelGenerator;
            gameManager.inputHandler = inputHandler;
            gameManager.uiManager = uiManager;
            gameManager.arrowPrefab = arrowController;
            gameManager.autoSpawnTestLayout = false;
            gameManager.startLevelIndex = 1;
            gameManager.useSavedProgress = true;
            gameManager.colorPriority = null;
            gameManager.movesLimit = DefaultMovesLimit;
            gameManager.bonuses = bonusSystem;

            bonusSystem.gameManager = gameManager;
            bonusSystem.hintCharges = 0;
            bonusSystem.undoCharges = 0;
            bonusSystem.extraMoveCharges = 0;

            uiManager.gameManager = gameManager;
            uiManager.bonuses = bonusSystem;

            levelGenerator.useTestGrid = false;
            levelGenerator.levelRoot = levelRoot.transform;
            levelGenerator.arrowPrefab = arrowController;
            levelGenerator.gridSize = new Vector2Int(6, 8);
            levelGenerator.cellSize = GameConstants.CellSize;

            inputHandler.gameCamera = cam;
            inputHandler.gameManager = gameManager;

            EnsureEventSystem();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            ConfigureMenuBuildSettings();
        }

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            GameObject canvasGo = CreateOverlayCanvas("Canvas");
            var menu = canvasGo.AddComponent<MainMenuController>();

            Sprite menuBg = LoadArt("bg_menu");
            GameObject background = CreateSliced(canvasGo.transform, "Background",
                menuBg, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Stretch(background.GetComponent<RectTransform>());
            var bgImage = background.GetComponent<Image>();
            bgImage.raycastTarget = false;
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;

            CreateDecorArrows(canvasGo.transform);

            Text title = CreateText(canvasGo.transform, "Title", "Уберите стрелки",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(920f, 160f),
                TextAnchor.MiddleCenter, 76, GameConstants.NavyText, FontStyle.Bold);

            Text level = CreateText(canvasGo.transform, "LevelLabel", "Уровень 1",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(920f, 70f),
                TextAnchor.MiddleCenter, 40, new Color(GameConstants.NavyText.r, GameConstants.NavyText.g, GameConstants.NavyText.b, 0.72f));

            Button play = CreateHudButton(canvasGo.transform, "PlayButton", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                new Vector2(756f, 160f), Color.white, null, "Играть", Color.white, 56, FontStyle.Bold);

            Sprite gearIcon = LoadArt("icon_gear");
            Button settingsBtn = CreateHudButton(canvasGo.transform, "SettingsButton", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f),
                new Vector2(756f, 140f), Color.white, null, "Настройки", GameConstants.NavyText, 44);
            if (gearIcon != null)
            {
                var iconGo = new GameObject("GearIcon", typeof(RectTransform));
                iconGo.transform.SetParent(settingsBtn.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(72f, 0f);
                iconRt.sizeDelta = new Vector2(56f, 56f);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = gearIcon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            SettingsPanel settings = InstantiateSettingsOverlay(canvasGo.transform);
            menu.goldCounter = GoldCounterView.EnsureOnCanvas(canvasGo.transform, true);
            if (menu.goldCounter != null) menu.goldText = menu.goldCounter.label;
            ShopPanel shop = ShopPanel.EnsureOnCanvas(canvasGo.transform);
            Button shopBtn = ShopPanel.EnsureShopButton(canvasGo.transform, true);
            DailyHintsPanel dailyHints = DailyHintsPanel.EnsureOnCanvas(canvasGo.transform);

            menu.titleText = title;
            menu.levelLabel = level;
            menu.playButton = play;
            menu.settingsButton = settingsBtn;
            menu.settingsPanel = settings;
            menu.shopPanel = shop;
            menu.shopButton = shopBtn;
            menu.dailyHintsPanel = dailyHints;

            EnsureEventSystem();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            ConfigureMenuBuildSettings();
        }

        private static Camera CreateCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f; // стартовое; в рантайме LevelGenerator.FitCameraToBoard подгоняет сетку
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConstants.BoardBackground;
            cam.allowHDR = false;
            if (camGo.GetComponent<UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        private static GameObject CreateOverlayCanvas(string name)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo;
        }

        private static UIManager CreateGameCanvas()
        {
            GameObject canvasGo = CreateOverlayCanvas("UI Canvas");
            var ui = canvasGo.AddComponent<UIManager>();
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Sprite backIcon = LoadArt("icon_back");
            Sprite gearIcon = LoadArt("icon_gear");
            Sprite hintIcon = LoadArt("icon_hint");
            Sprite undoIcon = LoadArt("icon_undo");
            Sprite restartIcon = LoadArt("icon_restart");

            var layout = ScreenLayoutConfig.CreateDefault();
            RectTransform topHud = CreateEmptyStretch(canvasGo.transform, "TopHud");
            RectTransform bottomHud = CreateEmptyStretch(canvasGo.transform, "BottomHud");

            ui.menuButton = CreateIconButton(topHud, "MenuButton",
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(layout.hudSideButtonWidth, layout.hudControlHeight),
                Color.white, null, Color.white, backIcon);

            ui.settingsButton = CreateIconButton(topHud, "SettingsButton",
                new Vector2(1f, 0.5f), Vector2.zero, new Vector2(layout.hudSideButtonWidth, layout.hudControlHeight),
                Color.white, gearIcon);

            Sprite capsule = LoadArt("ui_capsule");
            GameObject badge = CreateSliced(topHud, "LevelBadge", capsule != null ? capsule : rounded, GameConstants.HudDark,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(420f, layout.hudControlHeight));
            var badgeProgress = badge.AddComponent<LevelProgressView>();
            badgeProgress.trackImage = badge.GetComponent<Image>();
            badgeProgress.fillColor = GameConstants.ProgressGreen;
            badgeProgress.trackColor = GameConstants.HudDark;
            ui.progressView = badgeProgress;
            ui.levelText = CreateText(badge.transform, "LevelText", "Уровень 1",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(400f, 70f),
                TextAnchor.MiddleCenter, 40, Color.white);
            ui.movesText = CreateText(badge.transform, "MovesText", "Ходы 8",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(400f, 50f),
                TextAnchor.MiddleCenter, 28, new Color(1f, 1f, 1f, 0.82f));
            ui.goldCounter = null;
            ui.goldText = null;

            ui.messageText = CreateText(canvasGo.transform, "MessageText", string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 90f),
                TextAnchor.MiddleCenter, 40, new Color(0.90f, 0.28f, 0.24f));
            ui.messageText.enabled = false;

            ui.hintText = CreateText(canvasGo.transform, "HintText", string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f),
                TextAnchor.MiddleCenter, 32, GameConstants.HudDark);
            ui.hintText.enabled = false;

            float bottomSize = layout.hudBottomButtonSize;
            float bottomStep = bottomSize + layout.hudBottomButtonGap;
            ui.hintButton = CreateIconButton(bottomHud, "HintButton",
                new Vector2(0.5f, 0.5f), new Vector2(-1.5f * bottomStep, 0f), new Vector2(bottomSize, bottomSize),
                Color.white, hintIcon, Color.white, LoadArt("btn_hint"));
            ui.hintCountText = CreateBadge(ui.hintButton.transform, "0");

            ui.undoButton = CreateIconButton(bottomHud, "UndoButton",
                new Vector2(0.5f, 0.5f), new Vector2(-0.5f * bottomStep, 0f), new Vector2(bottomSize, bottomSize),
                Color.white, undoIcon, Color.white, LoadArt("btn_undo"));
            ui.undoCountText = CreateBadge(ui.undoButton.transform, "0");

            ui.extraMoveButton = CreateIconButton(bottomHud, "ExtraMoveButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f * bottomStep, 0f), new Vector2(bottomSize, bottomSize),
                Color.white, LoadArt("icon_extra_move"), Color.white, LoadArt("btn"));
            ui.extraMoveCountText = CreateBadge(ui.extraMoveButton.transform, "0");

            ui.restartButton = CreateIconButton(bottomHud, "RestartButton",
                new Vector2(0.5f, 0.5f), new Vector2(1.5f * bottomStep, 0f), new Vector2(bottomSize, bottomSize),
                Color.white, restartIcon, Color.white, LoadArt("btn_restart"));

            ui.settings = InstantiateSettingsOverlay(canvasGo.transform);
            ui.settingsPanel = ui.settings != null ? ui.settings.gameObject : null;
            ui.shop = ShopPanel.EnsureOnCanvas(canvasGo.transform);
            ui.dailyHintsPanel = DailyHintsPanel.EnsureOnCanvas(canvasGo.transform);
            ui.shopButton = null;
            ui.winPanel = CreateEndPanel(canvasGo.transform, "WinPanel", "ПОБЕДА",
                new Color(0.16f, 0.55f, 0.32f, 0.94f), out ui.winRestartButton, out ui.nextLevelButton, true);
            ui.losePanel = CreateEndPanel(canvasGo.transform, "LosePanel", "ХОДЫ ЗАКОНЧИЛИСЬ",
                new Color(0.55f, 0.16f, 0.16f, 0.94f), out ui.loseRestartButton, out _, false);

            ui.loseExtraMoveButton = CreateHudButton(ui.losePanel.transform, "ExtraMoveButton",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(480f, 130f),
                Color.white, LoadArt("icon_extra_move"), "+1 ход", Color.white, 44);

            ui.winPanel.SetActive(false);
            ui.losePanel.SetActive(false);
            return ui;
        }

        private static SettingsPanel InstantiateSettingsOverlay(Transform canvas)
        {
            Transform existing = canvas.Find("SettingsOverlay");
            if (existing != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(existing.gameObject))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        PrefabUtility.GetOutermostPrefabInstanceRoot(existing.gameObject),
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                var existingPanel = existing.GetComponent<SettingsPanel>();
                if (existingPanel != null)
                {
                    existing.gameObject.SetActive(false);
                    return existingPanel;
                }

                Object.DestroyImmediate(existing.gameObject);
            }

            var overlay = new GameObject("SettingsOverlay", typeof(RectTransform));
            overlay.transform.SetParent(canvas, false);
            FillSettingsOverlay(overlay);
            overlay.SetActive(false);
            return overlay.GetComponent<SettingsPanel>();
        }

        private static void UnpackSettingsInScene(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SettingsPanel[] panels = Object.FindObjectsOfType<SettingsPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                SettingsPanel panel = panels[i];
                if (panel == null) continue;

                GameObject go = panel.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                panel.gameObject.name = "SettingsOverlay";
            }

            SceneCanvasSetup.PrepareOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void DeleteLegacySettingsPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath) == null) return;
            AssetDatabase.DeleteAsset(SettingsPrefabPath);
        }

        private static void FillSettingsOverlay(GameObject overlay)
        {
            Stretch(overlay.GetComponent<RectTransform>());

            var panel = overlay.GetComponent<SettingsPanel>();
            if (panel == null) panel = overlay.AddComponent<SettingsPanel>();

            var audio = overlay.GetComponent<AudioSource>();
            if (audio == null) audio = overlay.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            panel.musicSource = audio;

            var dimmerGo = new GameObject("Dimmer", typeof(RectTransform));
            dimmerGo.transform.SetParent(overlay.transform, false);
            Stretch(dimmerGo.GetComponent<RectTransform>());
            var dimmerImage = dimmerGo.AddComponent<Image>();
            dimmerImage.sprite = LoadArt("bg_dimmer");
            dimmerImage.color = Color.white;
            dimmerImage.type = Image.Type.Simple;
            var dimmerBtn = dimmerGo.AddComponent<Button>();
            dimmerBtn.targetGraphic = dimmerImage;
            dimmerBtn.transition = Selectable.Transition.None;

            Sprite cardSprite = LoadArt("ui_card");
            GameObject card = CreateSliced(overlay.transform, "Card",
                cardSprite != null ? cardSprite : AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 920f));
            var cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            cardImage.color = Color.white;
            if (cardSprite != null) cardImage.type = Image.Type.Simple;

            CreateText(card.transform, "Title", "Настройки",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(560f, 72f),
                TextAnchor.MiddleCenter, 52, GameConstants.NavyText, FontStyle.Bold);

            Button closeX = CreateHudButton(card.transform, "CloseXButton",
                new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(110f, 110f),
                Color.white, null, string.Empty, Color.white, 48, FontStyle.Normal, LoadArt("icon_close"));

            Toggle sound = CreateLabeledToggle(card.transform, "SoundToggle", new Vector2(0f, 70f), "Звук");
            Toggle music = CreateLabeledToggle(card.transform, "MusicToggle", new Vector2(0f, -80f), "Музыка");
            Image soundIcon = SettingsPanel.EnsureToggleIcon(sound);
            Image musicIcon = SettingsPanel.EnsureToggleIcon(music);
            if (soundIcon != null) soundIcon.sprite = LoadArt("icon_sound");
            if (musicIcon != null) musicIcon.sprite = LoadArt("icon_music");

            Button close = CreateHudButton(card.transform, "CloseButton",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(400f, 152f),
                Color.white, null, "Закрыть", GameConstants.NavyText, 44);

            Transform soundState = sound.transform.Find("Track/Checkmark/State");
            Transform musicState = music.transform.Find("Track/Checkmark/State");

            panel.dimmerButton = dimmerBtn;
            panel.closeButton = close;
            panel.closeXButton = closeX;
            panel.soundToggle = sound;
            panel.musicToggle = music;
            panel.soundStateLabel = soundState != null ? soundState.GetComponent<Text>() : null;
            panel.musicStateLabel = musicState != null ? musicState.GetComponent<Text>() : null;
            panel.soundIcon = soundIcon;
            panel.musicIcon = musicIcon;
            panel.musicSource = audio;
        }

        private static Toggle CreateLabeledToggle(Transform parent, string name, Vector2 pos, string label)
        {
            Sprite rowFace = LoadArt("btn");
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            GameObject row = CreateSliced(parent, name, rowFace != null ? rowFace : rounded, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(680f, 148f));
            var rowImage = row.GetComponent<Image>();
            if (rowImage != null)
            {
                rowImage.raycastTarget = true;
                rowImage.type = Image.Type.Simple;
                rowImage.preserveAspect = false;
            }

            CreateText(row.transform, "Label", label,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(230f, 140f),
                TextAnchor.MiddleLeft, 42, GameConstants.NavyText, FontStyle.Bold);

            GameObject track = CreateSliced(row.transform, "Track",
                rounded, new Color(1f, 1f, 1f, 0f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(360f, 138f));
            var trackImage = track.GetComponent<Image>();
            if (trackImage != null)
            {
                trackImage.type = Image.Type.Simple;
                trackImage.preserveAspect = true;
                trackImage.color = new Color(1f, 1f, 1f, 0f);
                trackImage.raycastTarget = false;
            }

            GameObject check = CreateSliced(track.transform, "Checkmark", rounded, new Color(1f, 1f, 1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 80f));
            var checkImage = check.GetComponent<Image>();
            if (checkImage != null)
            {
                checkImage.enabled = false;
                checkImage.raycastTarget = false;
            }
            CreateText(check.transform, "State", "Вкл",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 80f),
                TextAnchor.MiddleCenter, 32, GameConstants.NavyText, FontStyle.Bold);

            var toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = rowImage;
            toggle.graphic = null;
            toggle.isOn = true;
            toggle.transition = Selectable.Transition.None;
            var nav = toggle.navigation;
            nav.mode = Navigation.Mode.None;
            toggle.navigation = nav;
            return toggle;
        }

        private static void CreateDecorArrows(Transform canvas)
        {
            var root = new GameObject("DecorArrows", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.ignoreParentGroups = false;

            Color red = ArrowController.ColorToUnityColor(ArrowColor.Red);
            Color blue = ArrowController.ColorToUnityColor(ArrowColor.Blue);
            Color navy = ArrowController.ColorToUnityColor(ArrowColor.DarkBlue);
            red.a = 0.88f;
            blue.a = 0.82f;
            navy.a = 0.78f;

            AddUiPolylineArrow(root.transform, "DecorArrow1", red, 16f,
                new Vector2(-390f, -640f), new Vector2(-200f, -470f), new Vector2(-40f, -540f));
            AddUiPolylineArrow(root.transform, "DecorArrow2", blue, 15f,
                new Vector2(60f, -720f), new Vector2(260f, -520f), new Vector2(430f, -590f));
            AddUiPolylineArrow(root.transform, "DecorArrow3", navy, 14f,
                new Vector2(-470f, -260f), new Vector2(-290f, -90f), new Vector2(-330f, 70f));
            AddUiPolylineArrow(root.transform, "DecorArrow4", red, 14f,
                new Vector2(180f, -250f), new Vector2(360f, -70f), new Vector2(470f, -150f));
            AddUiPolylineArrow(root.transform, "DecorArrow5", blue, 13f,
                new Vector2(-140f, -840f), new Vector2(30f, -760f), new Vector2(190f, -820f));
        }

        private static void AddUiPolylineArrow(Transform parent, string name, Color color, float thickness, params Vector2[] pts)
        {
            if (pts == null || pts.Length < 2) return;

            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Sprite headSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeadSpritePath);

            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 delta = b - a;
                float len = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                GameObject seg = CreateSliced(root.transform, $"Seg{i}", rounded, color,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), mid, new Vector2(len + thickness * 0.15f, thickness));
                seg.GetComponent<Image>().raycastTarget = false;
                seg.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, angle);
            }

            Vector2 last = pts[pts.Length - 1];
            Vector2 dir = (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized;
            float headAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Vector2 headPos = last + dir * (thickness * 0.08f);

            var headGo = new GameObject("Head", typeof(RectTransform));
            headGo.transform.SetParent(root.transform, false);
            var headRt = headGo.GetComponent<RectTransform>();
            headRt.anchorMin = new Vector2(0.5f, 0.5f);
            headRt.anchorMax = new Vector2(0.5f, 0.5f);
            headRt.pivot = new Vector2(0.5f, ArrowSpriteFactory.TrianglePivotY);
            headRt.anchoredPosition = headPos;
            float headBox = thickness * GameConstants.ArrowHeadWidthInCells
                            / (GameConstants.PathWidthInCells * ArrowSpriteFactory.TriangleWidthNormalized);
            headRt.sizeDelta = new Vector2(headBox, headBox);
            headRt.localEulerAngles = new Vector3(0f, 0f, headAngle);
            var headImage = headGo.AddComponent<Image>();
            headImage.sprite = headSprite;
            headImage.color = color;
            headImage.raycastTarget = false;
            headImage.preserveAspect = true;
        }

        private static void WireSettingsIntoGameScene()
        {
            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning($"[Unpuzzle] Нет {GameScenePath} — шестерёнка HUD будет добавлена при Rebuild Unpuzzle Scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            UIManager ui = Object.FindObjectOfType<UIManager>();
            if (ui == null)
            {
                Debug.LogWarning("[Unpuzzle] В GameScene нет UIManager — HUD настроек не привязан.");
                EditorSceneManager.SaveScene(scene);
                return;
            }

            Transform canvas = ui.transform;

            SettingsPanel[] panels = ui.GetComponentsInChildren<SettingsPanel>(true);
            for (int i = panels.Length - 1; i >= 0; i--)
            {
                if (panels[i] != null) Object.DestroyImmediate(panels[i].gameObject);
            }

            Transform oldNamed = canvas.Find("SettingsPanel");
            if (oldNamed != null) Object.DestroyImmediate(oldNamed.gameObject);

            Transform oldOverlay = canvas.Find("SettingsOverlay");
            if (oldOverlay != null) Object.DestroyImmediate(oldOverlay.gameObject);

            ui.settings = InstantiateSettingsOverlay(canvas);
            ui.settingsPanel = ui.settings != null ? ui.settings.gameObject : null;
            ui.shop = ShopPanel.EnsureOnCanvas(canvas);
            ui.dailyHintsPanel = DailyHintsPanel.EnsureOnCanvas(canvas);
            ui.shopButton = null;
            ui.soundButton = null;
            ui.soundButtonLabel = null;
            ui.settingsCloseButton = null;

            if (ui.settingsButton == null)
            {
                Sprite gearIcon = LoadArt("icon_gear");
                ui.settingsButton = CreateIconButton(canvas, "SettingsButton",
                    new Vector2(1f, 1f), new Vector2(-96f, -96f), new Vector2(132f, 132f),
                    Color.white, gearIcon);
            }

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureMenuBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));

            EditorBuildSettingsScene[] previous = EditorBuildSettings.scenes;
            for (int i = 0; i < previous.Length; i++)
            {
                string path = previous[i].path;
                if (path == MenuScenePath || path == GameScenePath) continue;
                bool enabled = previous[i].enabled;
                if (path.EndsWith("SampleScene.unity")) enabled = false;
                scenes.Add(new EditorBuildSettingsScene(path, enabled));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateMainMenuScene()
        {
            var menu = Object.FindObjectOfType<MainMenuController>();
            if (menu == null)
            {
                Debug.LogError("[Unpuzzle] MainMenuController не найден на сцене MainMenu.");
                return;
            }

            if (menu.playButton == null || menu.settingsButton == null || menu.shopButton == null
                || menu.titleText == null || menu.levelLabel == null || menu.settingsPanel == null
                || menu.shopPanel == null)
                Debug.LogError("[Unpuzzle] На MainMenuController остались пустые ссылки.");

            if (menu.settingsPanel != null &&
                (menu.settingsPanel.soundToggle == null || menu.settingsPanel.musicToggle == null ||
                 menu.settingsPanel.closeButton == null || menu.settingsPanel.dimmerButton == null))
                Debug.LogError("[Unpuzzle] На SettingsPanel остались пустые ссылки.");

            if (menu.settingsPanel != null && PrefabUtility.IsPartOfPrefabInstance(menu.settingsPanel.gameObject))
                Debug.LogError("[Unpuzzle] SettingsOverlay всё ещё префаб — запустите Convert Settings Prefab To Scene Objects.");

            EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
            if (build.Length < 2 || build[0].path != MenuScenePath || !build[0].enabled)
                Debug.LogError("[Unpuzzle] MainMenu должен быть сценой 0 в Build Settings.");
            if (build.Length < 2 || build[1].path != GameScenePath || !build[1].enabled)
                Debug.LogError("[Unpuzzle] GameScene должен быть сценой 1 в Build Settings.");
        }

        private static GameObject CreateEndPanel(Transform parent, string name, string title, Color bg,
            out Button restartButton, out Button nextButton, bool withNext)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            var panelImage = panel.AddComponent<Image>();
            Sprite endBg = name == "WinPanel" ? LoadArt("bg_win") : LoadArt("bg_lose");
            panelImage.sprite = endBg;
            panelImage.color = Color.white;
            panelImage.type = Image.Type.Simple;

            CreateText(panel.transform, "TitleText", title,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(900f, 160f),
                TextAnchor.MiddleCenter, 64, Color.white);

            nextButton = null;
            if (withNext)
            {
                nextButton = CreateHudButton(panel.transform, "NextLevelButton",
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(480f, 140f),
                    Color.white, null, "Дальше", GameConstants.HudDark, 48);
            }

            restartButton = CreateHudButton(panel.transform, "RestartButton",
                new Vector2(0.5f, 0.5f), new Vector2(0f, withNext ? -120f : 0f), new Vector2(480f, 140f),
                Color.white, null, "Заново", GameConstants.HudDark, 48);

            return panel;
        }

        // -------------------------------------------------------------- UI helpers

        private static Sprite LoadArt(string fileName)
        {
            string png = "Assets/Art/Sprites/" + fileName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            if (sprite != null) return sprite;
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/" + fileName + " 1.png");
            if (sprite != null) return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/" + fileName + ".jpg");
        }

        private static Button CreateIconButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            Color bg, Sprite icon, Color? iconColor = null, Sprite faceSprite = null)
        {
            Button button = CreateHudButton(parent, name, anchor, pos, size, bg, icon, string.Empty,
                iconColor ?? Color.white, 1, FontStyle.Normal, faceSprite);
            return button;
        }

        private static Button CreateHudButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            Color bg, Sprite icon, string label, Color labelColor, int fontSize, FontStyle fontStyle = FontStyle.Normal,
            Sprite faceSprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(Mathf.Approximately(anchor.x, 0f) ? 0f : (Mathf.Approximately(anchor.x, 1f) ? 1f : 0.5f),
                Mathf.Approximately(anchor.y, 0f) ? 0.5f : (Mathf.Approximately(anchor.y, 1f) ? 1f : 0.5f));
            if (Mathf.Approximately(anchor.x, 0.5f) && Mathf.Approximately(anchor.y, 0f))
                rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Sprite face = faceSprite != null ? faceSprite : LoadArt("btn");
            if (face == null) face = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            var image = go.AddComponent<Image>();
            image.sprite = face;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = face != null ? Color.white : bg;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(size.x * 0.58f, size.y * 0.58f);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            if (!string.IsNullOrEmpty(label))
            {
                CreateText(go.transform, "Label", label,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size,
                    TextAnchor.MiddleCenter, fontSize, labelColor, fontStyle);
            }

            return button;
        }

        public static void EnsureExtraMoveHud(UIManager ui)
        {
            if (ui == null) return;

            Transform canvas = ui.transform;
            Transform bottomHud = canvas.Find("BottomHud");
            if (bottomHud == null)
            {
                var found = AuthoredUi.FindDeep(canvas, "BottomHud");
                bottomHud = found;
            }

            if (bottomHud == null) return;

            Button extra = null;
            Transform extraT = bottomHud.Find("ExtraMoveButton");
            if (extraT != null) extra = extraT.GetComponent<Button>();
            if (extra == null) extra = ui.extraMoveButton;
            if (extra != null && extra.transform.parent != bottomHud)
            {
                if (extra.transform.parent != null && extra.transform.parent.name == "LosePanel")
                    extra = null;
            }

            if (extra == null)
            {
                extra = CreateIconButton(bottomHud, "ExtraMoveButton",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(148f, 148f),
                    Color.white, LoadArt("icon_extra_move"), Color.white, LoadArt("btn"));
            }

            ui.extraMoveButton = extra;
            if (ui.extraMoveCountText == null && extra != null)
            {
                Transform value = extra.transform.Find("Count/Value");
                ui.extraMoveCountText = value != null ? value.GetComponent<Text>() : null;
            }

            if (ui.extraMoveCountText == null && extra != null)
                ui.extraMoveCountText = CreateBadge(extra.transform, "0");

            EditorUtility.SetDirty(ui);
        }

        private static Text CreateBadge(Transform parent, string value)
        {
            Sprite badgeSprite = LoadArt("badge_count");
            GameObject badge = CreateSliced(parent, "Count",
                badgeSprite != null ? badgeSprite : AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath), Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(56f, 56f));
            if (badgeSprite != null)
            {
                var badgeImage = badge.GetComponent<Image>();
                badgeImage.type = Image.Type.Simple;
                badgeImage.preserveAspect = true;
                badgeImage.color = Color.white;
            }
            return CreateText(badge.transform, "Value", value,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f),
                TextAnchor.MiddleCenter, 28, Color.white);
        }

        private static GameObject CreateSliced(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(
                Mathf.Approximately(anchorMin.x, 0f) ? 0f : (Mathf.Approximately(anchorMin.x, 1f) ? 1f : 0.5f),
                Mathf.Approximately(anchorMin.y, 0f) ? 0f : (Mathf.Approximately(anchorMin.y, 1f) ? 1f : 0.5f));
            if (Mathf.Approximately(anchorMin.x, 0.5f)) rt.pivot = new Vector2(0.5f, rt.pivot.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, TextAnchor alignment,
            int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (Mathf.Approximately(anchorMin.x, 0f) && Mathf.Approximately(anchorMax.x, 0f))
                rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateEmptyStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Font GetDefaultFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* ignore */ }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { /* ignore */ }
            }

            return font;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void AddSceneToBuildSettings(string path, int index)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(s => s.path == path);
            var entry = new EditorBuildSettingsScene(path, true);
            if (existing >= 0) scenes.RemoveAt(existing);
            index = Mathf.Clamp(index, 0, scenes.Count);
            scenes.Insert(index, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateBuiltScene()
        {
            if (GameObject.Find("Ground") != null)
                Debug.LogError("[Unpuzzle] В GameScene остался Ground — физика должна быть вырезана.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Unpuzzle] Нет префаба Arrow.");
                return;
            }

            if (prefab.GetComponent<Rigidbody2D>() != null)
                Debug.LogError("[Unpuzzle] На префабе Arrow остался Rigidbody2D.");
            if (prefab.transform.Find("Path") == null || prefab.transform.Find("Head") == null)
                Debug.LogError("[Unpuzzle] Префаб Arrow должен содержать Path (LineRenderer) и Head (SpriteRenderer).");

            var gm = Object.FindObjectOfType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[Unpuzzle] GameManager не найден на сцене.");
                return;
            }

            if (gm.levelRoot == null || gm.uiManager == null || gm.inputHandler == null || gm.levelGenerator == null || gm.arrowPrefab == null)
                Debug.LogError("[Unpuzzle] На GameManager остались пустые ссылки.");
            if (GameObject.Find("ScreenRoot") == null)
                Debug.LogError("[Unpuzzle] ScreenRoot не собран — Tools/Unpuzzle/Setup Visible Layout (HUD corridors).");
            if (gm.colorPriority != null && gm.colorPriority.rulesEnabled)
                Debug.LogWarning("[Unpuzzle] ColorPriority должен быть выключен по умолчанию.");
        }
    }

    /// <summary>Подхватывает отложенный Setup Main Menu после импорта скриптов (в т.ч. выход из Play Mode).</summary>
    internal sealed class UnpuzzleMainMenuAutoSetup : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            UnpuzzleSetup.TryRunPendingSetupMainMenu();
        }
    }
}
