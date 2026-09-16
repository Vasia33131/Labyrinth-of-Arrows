using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>Одноразовый откат кнопки «Настройки» к размеру до растягивания.</summary>
    public static class RestoreSettingsButton
    {
        private const string FlagPath = "Library/unpuzzle_restore_settings_button.flag";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            EditorApplication.delayCall += RunFlagged;
        }

        private static void RunFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            Restore();
        }

        private static void Restore()
        {
            var buttons = Object.FindObjectsOfType<Button>(true);
            int restored = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.name != "SettingsButton") continue;
                var label = button.transform.Find("Label") as RectTransform;
                if (label == null) continue;

                var rt = button.transform as RectTransform;
                if (rt != null
                    && Mathf.Abs(rt.anchorMin.x - rt.anchorMax.x) < 0.001f
                    && Mathf.Abs(rt.anchorMin.y - rt.anchorMax.y) < 0.001f)
                {
                    rt.sizeDelta = new Vector2(800f, 300f);
                    EditorUtility.SetDirty(rt);
                }

                label.anchorMin = new Vector2(0.5f, 0.5f);
                label.anchorMax = new Vector2(0.5f, 0.5f);
                label.pivot = new Vector2(0.5f, 0.5f);
                label.anchoredPosition = new Vector2(0f, 12f);
                label.sizeDelta = new Vector2(756f, 140f);
                EditorUtility.SetDirty(label);
                EditorUtility.SetDirty(button);
                restored++;
            }

            if (restored == 0) return;

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Unpuzzle] Кнопка «Настройки» возвращена к прежнему размеру.");
        }
    }
}
