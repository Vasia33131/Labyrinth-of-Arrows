using UnityEditor;
using UnityEngine;

namespace Unpuzzle.EditorTools
{
    /// <summary>SFX — 2D, preload. Музыка — compressed in memory, тоже 2D.</summary>
    public static class UnpuzzleAudioImport
    {
        private const string Root = "Assets/Resources/Audio";
        private const string PrefsKey = "Unpuzzle.AudioImport.v2";

        [InitializeOnLoadMethod]
        private static void AutoConfigure()
        {
            if (EditorPrefs.GetInt(PrefsKey, 0) == 1) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetInt(PrefsKey, 0) == 1) return;
                Configure();
                EditorPrefs.SetInt(PrefsKey, 1);
            };
        }

        [MenuItem("Tools/Unpuzzle/Configure Audio Import", priority = 40)]
        public static void Configure()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root });
            int changed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                if (!Apply(importer, path.Contains("/Music/"))) continue;
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log("[Unpuzzle] Audio import: обновлено клипов " + changed);
        }

        private static bool Apply(AudioImporter importer, bool music)
        {
            bool changed = false;
            if (importer.forceToMono != !music)
            {
                importer.forceToMono = !music;
                changed = true;
            }

            if (importer.loadInBackground != music)
            {
                importer.loadInBackground = music;
                changed = true;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType loadType = music
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType != loadType)
            {
                settings.loadType = loadType;
                changed = true;
            }

            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                changed = true;
            }

            if (settings.quality < 0.7f)
            {
                settings.quality = 0.8f;
                changed = true;
            }

            if (changed) importer.defaultSampleSettings = settings;
            return changed;
        }
    }
}
