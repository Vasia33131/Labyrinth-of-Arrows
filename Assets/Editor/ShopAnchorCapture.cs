using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Берёт текущие RectTransform магазина в открытой сцене и записывает
    /// тот же прямоугольник якорями (offset = 0), без ApplyShopLayout.
    /// </summary>
    public static class ShopAnchorCapture
    {
        private const string FlagPath = "Library/unpuzzle_capture_shop_anchors.flag";

        [InitializeOnLoadMethod]
        private static void AutoCaptureIfFlagged()
        {
            if (!File.Exists(FlagPath)) return;
            EditorApplication.delayCall += RunFlaggedCapture;
        }

        [MenuItem("Tools/Unpuzzle/Save Shop Anchors", priority = 10)]
        public static void SaveShopAnchorsMenu()
        {
            CaptureOpenScene();
        }

        private static void RunFlaggedCapture()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunFlaggedCapture;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= CaptureAfterPlay;
                EditorApplication.playModeStateChanged += CaptureAfterPlay;
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode — запишу якоря магазина.");
                return;
            }

            if (!File.Exists(FlagPath)) return;
            try { File.Delete(FlagPath); }
            catch { /* ignore */ }

            CaptureOpenScene();
        }

        private static void CaptureAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= CaptureAfterPlay;
            EditorApplication.delayCall += RunFlaggedCapture;
        }

        private static void CaptureOpenScene()
        {
            ShopPanel panel = Object.FindObjectOfType<ShopPanel>(true);
            if (panel == null)
            {
                Debug.LogError("[Unpuzzle] ShopOverlay не найден в открытой сцене.");
                return;
            }

            bool wasActive = panel.gameObject.activeSelf;
            if (!wasActive) panel.gameObject.SetActive(true);

            Canvas.ForceUpdateCanvases();
            var root = panel.transform as RectTransform;
            if (root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);

            int converted = RectAnchorCapture.ConvertTree(panel.transform);

            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(panel.gameObject);
            Scene scene = panel.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            LogShop(panel, converted);
        }

        private static void LogShop(ShopPanel panel, int converted)
        {
            Transform card = panel.transform.Find("Card");
            var cardRt = card as RectTransform;
            Transform tabs = card != null ? card.Find("Tabs") : null;
            var tabsRt = tabs as RectTransform;
            Debug.Log("[Unpuzzle] Магазин записан якорями (" + converted + " RectTransform). Card="
                + (cardRt != null ? cardRt.rect.size.ToString("F0") : "?")
                + " Tabs="
                + (tabsRt != null ? tabsRt.rect.size.ToString("F0") : "?")
                + " в сцене " + panel.gameObject.scene.name + ".");
        }
    }
}
