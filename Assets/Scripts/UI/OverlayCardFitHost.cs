using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вешается на оверлей в рантайме. Держит Card и CloseXButton в одном размере
    /// на всех панелях и сценах, в том числе после ресайза UiFrame.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class OverlayCardFitHost : MonoBehaviour
    {
        public RectTransform card;
        public RectTransform closeX;

        private int lastWidth;
        private int lastHeight;
        private float lastParentWidth;
        private float lastParentHeight;

        public static OverlayCardFitHost Ensure(Component overlay, Button closeXButton = null)
        {
            if (overlay == null) return null;

            var host = overlay.GetComponent<OverlayCardFitHost>();
            if (host == null) host = overlay.gameObject.AddComponent<OverlayCardFitHost>();

            if (closeXButton != null)
                host.closeX = closeXButton.transform as RectTransform;

            host.Resolve();
            host.Fit(true);
            return host;
        }

        private void OnEnable()
        {
            Resolve();
            Fit(true);
        }

        private void Start()
        {
            Fit(true);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Fit(false);
        }

        private void Resolve()
        {
            if (card == null) card = OverlayCardFit.FindCard(transform);
            if (closeX == null) closeX = OverlayCardFit.FindCloseX(transform, card);
        }

        private void Fit(bool force)
        {
            if (!Application.isPlaying) return;

            Resolve();
            if (card == null) return;

            var parent = card.parent as RectTransform;
            if (parent == null) return;

            if (!force
                && Screen.width == lastWidth
                && Screen.height == lastHeight
                && Mathf.Approximately(parent.rect.width, lastParentWidth)
                && Mathf.Approximately(parent.rect.height, lastParentHeight))
                return;

            bool fitted = GetComponent<SchoolTutorialPanel>() != null
                ? OverlayCardFit.ApplyTutorial(card)
                : OverlayCardFit.Apply(card, closeX);
            if (!fitted) return;

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastParentWidth = parent.rect.width;
            lastParentHeight = parent.rect.height;
        }
    }
}
