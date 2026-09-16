using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Создаёт Resources/BackgroundCatalog.asset, чтобы в инспекторе можно было кинуть PNG.
    /// Меню: Tools/Unpuzzle/Setup Backgrounds
    /// </summary>
    public static class BackgroundCatalogSetup
    {
        public const string AssetPath = "Assets/Resources/BackgroundCatalog.asset";
        public const string NewSpritesFolder = "Assets/Art/Sprites/New";

        [MenuItem("Tools/Unpuzzle/Setup Backgrounds", priority = 10)]
        public static void SetupBackgrounds()
        {
            BackgroundCatalog catalog = EnsureAsset();
            if (catalog == null)
            {
                Debug.LogError("[Unpuzzle] BackgroundCatalog не создался.");
                return;
            }

            Debug.Log("[Unpuzzle] Каталог фонов готов: " + AssetPath + ". Спрайты можно подставить в инспекторе.");
        }

        public static BackgroundCatalog EnsureAsset()
        {
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            var catalog = AssetDatabase.LoadAssetAtPath<BackgroundCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
                catalog.ResetToBuiltin();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.ResetToBuiltin();
            catalog.KeepOnlyBuiltinBackgrounds();
            ApplySpritesFromNewFolder(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void ApplySpritesFromNewFolder(BackgroundCatalog catalog)
        {
            if (catalog == null) return;

            AssignSprite(catalog, BackgroundCatalog.WitchGroveId, "bg_witch_grove");
            AssignSprite(catalog, BackgroundCatalog.NightElfId, "bg_night_elf");
            AssignSprite(catalog, BackgroundCatalog.NeonCityId, "bg_neon_city");
            AssignSprite(catalog, BackgroundCatalog.ClassroomId, "bg_classroom");

            BackgroundData classic = catalog.Get(GameConstants.DefaultBackgroundId);
            if (classic != null)
                classic.sprite = null;
        }

        private static void AssignSprite(BackgroundCatalog catalog, string id, string fileNameWithoutExtension)
        {
            BackgroundData background = catalog.Get(id);
            if (background == null) return;

            background.sprite = LoadSprite(fileNameWithoutExtension);
        }

        private static Sprite LoadSprite(string fileNameWithoutExtension)
        {
            string path = ResolveSpritePath(fileNameWithoutExtension);
            if (path == null)
            {
                Debug.LogWarning("[Unpuzzle] Нет спрайта фона: " + NewSpritesFolder + "/" + fileNameWithoutExtension);
                return null;
            }

            ConfigureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string ResolveSpritePath(string fileNameWithoutExtension)
        {
            string png = NewSpritesFolder + "/" + fileNameWithoutExtension + ".png";
            if (File.Exists(png)) return png;

            string jpg = NewSpritesFolder + "/" + fileNameWithoutExtension + ".jpg";
            if (File.Exists(jpg)) return jpg;

            return null;
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
    }
}
