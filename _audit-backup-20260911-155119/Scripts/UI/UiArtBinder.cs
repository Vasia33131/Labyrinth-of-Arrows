using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вешает готовый арт на уже существующие Image / Button. Не создаёт сцены заново.
    /// </summary>
    public static class UiArtBinder
    {
        public static void BindMainMenu(Transform canvas)
        {
            if (Application.isPlaying || SceneCanvas.ArtLocked) return;
            ArtLibrary art = ArtLibrary.Current;
            if (art == null || canvas == null) return;

            Paint(FindImage(canvas, "Background"), art.bgMenu, Image.Type.Simple, false, false);
            PaintButton(FindButton(canvas, "PlayButton"), art.btn, null, true);
            PaintButton(FindButton(canvas, "SettingsButton"), art.btn, art.iconGear, true);
            PaintButton(FindButton(canvas, "ShopButton"), art.btn, null, true);
            Paint(FindImage(canvas, "GearIcon"), art.iconGear, Image.Type.Simple, true, false);
        }

        public static void BindGameHud(UIManager ui)
        {
            if (Application.isPlaying || SceneCanvas.ArtLocked) return;
            ArtLibrary art = ArtLibrary.Current;
            if (art == null || ui == null) return;

            EnsureBoardBackground(ui.transform, art.bgBoard);
            PaintButton(FindButton(ui.transform, "ShopButton"), art.btn, null, true);
        }

        public static void BindSettings(SettingsPanel panel)
        {
            if (Application.isPlaying || SceneCanvas.ArtLocked) return;
            ArtLibrary art = ArtLibrary.Current;
            if (art == null || panel == null) return;

            Transform root = panel.transform;
            Paint(FindImage(root, "Dimmer"), art.bgDimmer, Image.Type.Simple, false, true);
            Paint(FindImage(root, "Card"), art.uiCard, Image.Type.Simple, true, true);
            PaintButton(panel.closeButton, art.btn, null, false);
            PaintButton(panel.closeXButton, art.iconClose, null, false);
            HideToggleRowFace(panel.soundToggle);
            HideToggleRowFace(panel.musicToggle);
        }

        public static void BindShop(ShopPanel panel)
        {
            if (Application.isPlaying || SceneCanvas.ArtLocked) return;
            ArtLibrary art = ArtLibrary.Current;
            if (art == null || panel == null) return;

            Transform root = panel.transform;
            Paint(FindImage(root, "Dimmer"), art.bgDimmer, Image.Type.Simple, false, true);
            Paint(FindImage(root, "Card"), art.uiCard, Image.Type.Simple, true, true);
            PaintButton(panel.closeXButton, art.iconClose, null, false);
            PaintButton(panel.characterTab, art.btn, null, false);
            PaintButton(panel.backgroundTab, art.btn, null, false);
            PaintButton(panel.goldTab, art.btn, null, false);
            PaintButton(panel.goldAdButton, art.btn, null, false);
        }

        public static void PaintButton(Button button, Sprite face, Sprite icon, bool slicedFace)
        {
            if (button == null) return;
            Paint(button.targetGraphic as Image ?? button.GetComponent<Image>(), face,
                slicedFace ? Image.Type.Sliced : Image.Type.Simple, !slicedFace, true);

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            button.colors = colors;

            if (icon == null)
            {
                Transform leftover = button.transform.Find("Icon");
                if (leftover != null) leftover.gameObject.SetActive(false);
                return;
            }
            Transform iconTf = button.transform.Find("Icon");
            if (iconTf == null && button.transform.Find("GearIcon") != null)
                iconTf = button.transform.Find("GearIcon");

            Image iconImage = iconTf != null ? iconTf.GetComponent<Image>() : null;
            if (iconImage == null)
            {
                if (Application.isPlaying) return;
                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(button.transform, false);
                var rt = go.GetComponent<RectTransform>();
                bool hasLabel = button.transform.Find("Label") != null;
                if (hasLabel)
                {
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(56f, 0f);
                }
                else
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                }

                rt.sizeDelta = new Vector2(72f, 72f);
                iconImage = go.AddComponent<Image>();
            }

            var parentRt = button.transform as RectTransform;
            if (!Application.isPlaying && parentRt != null && iconImage.rectTransform != null)
            {
                float side = Mathf.Min(parentRt.sizeDelta.x, parentRt.sizeDelta.y) * 0.58f;
                if (side < 8f) side = 72f;
                iconImage.rectTransform.sizeDelta = new Vector2(side, side);
                iconImage.rectTransform.anchoredPosition = Vector2.zero;
            }

            Paint(iconImage, icon, Image.Type.Simple, true, false);
        }

        public static void Paint(Image image, Sprite sprite, Image.Type type, bool preserveAspect, bool raycast)
        {
            if (image == null) return;
            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.type = type;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycast;
        }

        private static void HideToggleRowFace(Toggle toggle)
        {
            if (toggle == null) return;
            var row = toggle.GetComponent<Image>();
            if (row == null) return;
            row.color = new Color(1f, 1f, 1f, 0f);
            row.raycastTarget = false;
        }

        private static void PaintCount(Button button, Sprite badge)
        {
            if (button == null || badge == null) return;
            Transform count = button.transform.Find("Count");
            if (count == null) return;
            Paint(count.GetComponent<Image>(), badge, Image.Type.Simple, true, false);
        }

        private static void EnsureBoardBackground(Transform canvas, Sprite sprite)
        {
            if (canvas == null || sprite == null) return;

            Transform existing = canvas.Find("BoardBackground");
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                if (Application.isPlaying) return;
                var go = new GameObject("BoardBackground", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                go.transform.SetAsFirstSibling();
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                image = go.AddComponent<Image>();
            }
            else
            {
                existing.SetAsFirstSibling();
            }

            Paint(image, sprite, Image.Type.Simple, false, false);
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform tf = FindDeep(root, name);
            return tf != null ? tf.GetComponent<Button>() : null;
        }

        private static Image FindImage(Transform root, string name)
        {
            Transform tf = FindDeep(root, name);
            return tf != null ? tf.GetComponent<Image>() : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            Transform direct = root.Find(name);
            if (direct != null) return direct;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
