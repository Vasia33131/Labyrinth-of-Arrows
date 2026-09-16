using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>Вешает выбранный фон на SceneCanvas.background и на world-backdrop поля.</summary>
    public static class BackgroundVisual
    {
        private static Sprite fillSprite;

        public static Sprite FillSprite
        {
            get
            {
                if (fillSprite != null) return fillSprite;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply(false, false);

                fillSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                fillSprite.name = "BackgroundFill";
                fillSprite.hideFlags = HideFlags.HideAndDontSave;
                return fillSprite;
            }
        }

        public static bool IsMenu(SceneCanvas canvas)
        {
            return canvas != null && canvas.playButton != null;
        }

        public static void Apply(SceneCanvas canvas)
        {
            if (canvas == null) return;

            bool menu = IsMenu(canvas);
            ApplyToImage(canvas.background, menu, canvas);

            if (!menu && BoardCameraFitter.Instance != null)
                BoardCameraFitter.Instance.SyncBackdrop();
        }

        public static void ApplyToImage(Image image, bool menu, SceneCanvas canvas)
        {
            if (image == null) return;

            Resolve(Backgrounds.Selected, menu, canvas, out Sprite sprite, out Color color);
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        public static void Resolve(BackgroundData background, bool menu, SceneCanvas canvas,
            out Sprite sprite, out Color color)
        {
            ArtLibrary art = ArtLibrary.Current;
            if (background == null)
            {
                if (menu)
                {
                    sprite = FallbackArt(true, art, canvas);
                    color = Color.white;
                }
                else
                {
                    sprite = FillSprite;
                    color = GameConstants.BoardBackground;
                }
                return;
            }

            sprite = background.ResolveSprite(menu, art);
            if (sprite == null) sprite = FallbackArt(menu, art, canvas);
            color = background.ResolveColor();
            // У «Классики» своего спрайта нет, она берёт общий арт. Тонировать его
            // цветом-превью нельзя, иначе фон уйдёт в тёмную заливку.
            if (background.sprite == null && background.id == GameConstants.DefaultBackgroundId)
                color = Color.white;
        }

        private static Sprite FallbackArt(bool menu, ArtLibrary art, SceneCanvas canvas)
        {
            Sprite fromArt = BackgroundData.DefaultArt(menu, art);
            if (fromArt != null) return fromArt;
            if (canvas == null) return null;
            return menu ? canvas.bgMenu : canvas.bgBoard;
        }
    }
}
