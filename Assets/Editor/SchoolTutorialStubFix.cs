using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Туториал школы авторский: спрайты и вёрстку не переписываем.
    /// </summary>
    public static class SchoolTutorialStubFix
    {
        private const string FlagPath = "Library/unpuzzle_fix_tutorial_squares.flag";

        [InitializeOnLoadMethod]
        private static void AutoRunIfFlagged()
        {
            EditorApplication.delayCall += RunFlagged;
        }

        [MenuItem("Tools/Unpuzzle/Replace School Tutorial Squares", priority = 13)]
        public static void RunMenu()
        {
            FixOpenScene();
        }

        private static void RunFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlagged;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — заменю квадраты в туториале школы.");
                return;
            }

            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            FixOpenScene();
        }

        private static void FixOpenScene()
        {
            SchoolTutorialPanel[] panels = Object.FindObjectsOfType<SchoolTutorialPanel>(true);
            if (panels == null || panels.Length == 0)
            {
                Debug.LogWarning("[Unpuzzle] SchoolTutorialOverlay не найден в открытой сцене.");
                return;
            }

            Debug.Log("[Unpuzzle] SchoolTutorialOverlay не переписываю — вёрстка и спрайты из сцены.");
        }
    }
}
