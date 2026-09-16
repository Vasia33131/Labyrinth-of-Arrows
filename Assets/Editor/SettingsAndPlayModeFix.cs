using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Кладёт панель настроек и панель режима в открытую сцену, чтобы их можно было двигать руками.
    /// </summary>
    public static class SettingsAndPlayModeFix
    {
        private const string FlagPath = "Library/unpuzzle_fix_settings_playmode.flag";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            EditorApplication.delayCall += RunFlagged;
            EditorApplication.delayCall += DailyHintsSceneSetup.TryRunFlagged;
        }

        [MenuItem("Tools/Unpuzzle/Put Settings And Play Mode On Scene", priority = 11)]
        public static void RunMenu()
        {
            EmbedIntoOpenScenes(true);
        }

        private static void RunFlagged()
        {
            if (!System.IO.File.Exists(FlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — вынесу панели в сцену.");
                return;
            }

            try { System.IO.File.Delete(FlagPath); }
            catch { /* ignore */ }

            EmbedIntoOpenScenes(true);
        }

        private static void EmbedIntoOpenScenes(bool save)
        {
            int touched = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (EmbedInLoadedScene(scene))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (save) EditorSceneManager.SaveScene(scene);
                    touched++;
                }
            }

            if (touched == 0)
                Debug.LogWarning("[Unpuzzle] Нет открытой сцены с настройками или панелью режима.");
            else
                Debug.Log("[Unpuzzle] Панель режима и настройки лежат в сцене. Школа включена, галочки видны. ModePage тоже включена — выключи SchoolPage, чтобы править кнопки «Обычная / Школа».");
        }

        private static bool EmbedInLoadedScene(Scene scene)
        {
            bool changed = false;

            SettingsPanel[] settings = Object.FindObjectsOfType<SettingsPanel>(true);
            for (int i = 0; i < settings.Length; i++)
            {
                EmbedSettings(settings[i]);
                changed = true;
            }

            PlayModePanel[] playModes = Object.FindObjectsOfType<PlayModePanel>(true);
            for (int i = 0; i < playModes.Length; i++)
            {
                EmbedPlayMode(playModes[i]);
                changed = true;
            }

            return changed;
        }

        private static void EmbedSettings(SettingsPanel panel)
        {
            if (panel == null) return;

            panel.gameObject.SetActive(true);
            panel.AuthorForEditor();
            EditorUtility.SetDirty(panel);
            if (panel.soundIcon != null) EditorUtility.SetDirty(panel.soundIcon.gameObject);
            if (panel.musicIcon != null) EditorUtility.SetDirty(panel.musicIcon.gameObject);
            if (panel.closeXButton != null)
                EditorUtility.SetDirty(panel.closeXButton.gameObject);
        }

        private static void EmbedPlayMode(PlayModePanel panel)
        {
            if (panel == null) return;

            panel.gameObject.SetActive(true);
            panel.AuthorForEditor();
            panel.transform.SetAsLastSibling();
            EditorUtility.SetDirty(panel);
            Selection.activeGameObject = panel.gameObject;
        }
    }
}
