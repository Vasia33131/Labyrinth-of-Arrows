using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Ставит <see cref="CapsuleTapInset"/> на кнопки, нарисованные спрайтом капсулы с прозрачными полями.
    /// Вёрстку не двигает: меняется только зона raycast, позиции и картинка остаются авторскими.
    /// </summary>
    public static class UiTapGuard
    {
        public static void Apply(Component root)
        {
            if (root == null) return;

            Sprite capsule = ResolveCapsule();
            if (capsule == null) return;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
                ApplyTo(images[i], capsule);
        }

        /// <summary>Для кнопок, собранных в рантайме — вызывать сразу после создания Image.</summary>
        public static void ApplyTo(Image image)
        {
            ApplyTo(image, ResolveCapsule());
        }

        private static void ApplyTo(Image image, Sprite capsule)
        {
            if (image == null || capsule == null) return;
            if (image.sprite != capsule) return;
            if (!image.raycastTarget) return;
            if (image.GetComponent<Selectable>() == null) return;
            if (image.GetComponent<CapsuleTapInset>() != null) return;

            image.gameObject.AddComponent<CapsuleTapInset>();
        }

        private static Sprite ResolveCapsule()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.btn != null) return art.btn;

            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.btn : null;
        }
    }
}
