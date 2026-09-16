using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Спрайт капсулы нарисован с прозрачными полями, а Image ловит raycast всем rect'ом.
    /// Из-за этого прозрачный край одной кнопки перехватывает тап по видимой капсуле соседней.
    /// raycastPadding поджимает зону попадания до видимой части, не меняя картинку.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class CapsuleTapInset : MonoBehaviour
    {
        /// <summary>Доли прозрачных полей btn.png: альфа-бокс 77..1456 x 164..774 при размере 1536x1024.</summary>
        public static readonly Vector4 CapsuleInset = new Vector4(0.0501f, 0.2441f, 0.0521f, 0.1602f);

        [Tooltip("Доли прозрачных полей спрайта: left, bottom, right, top.")]
        public Vector4 inset = CapsuleInset;

        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        public void Apply()
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null) return;

            Rect rect = image.rectTransform.rect;
            image.raycastPadding = new Vector4(
                rect.width * inset.x,
                rect.height * inset.y,
                rect.width * inset.z,
                rect.height * inset.w);
        }
    }
}
