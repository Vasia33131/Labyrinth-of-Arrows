using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Вёрстка нарисована в портрете 1080×1920 и масштабируется по высоте. На телефоне
    /// в ландшафте высота окна вдвое меньше, поэтому бустеры падают до ~29 CSS px,
    /// а кнопки HUD до ~21 px. Вместо второй раскладки показываем просьбу повернуть
    /// устройство — стандартное поведение портретных игр.
    /// Десктоп не затрагивается: там окно широкое, но высокое, и UI остаётся крупным.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public class OrientationGate : MonoBehaviour
    {
        public const string ObjectName = "OrientationGate";

        private RectTransform root;
        private Text label;
        private bool shown;

        public static bool IsBlocking { get; private set; }

        public static OrientationGate Ensure(Canvas canvas)
        {
            if (canvas == null) return null;

            var gate = canvas.GetComponent<OrientationGate>();
            if (gate == null) gate = canvas.gameObject.AddComponent<OrientationGate>();
            return gate;
        }

        private static bool ShouldBlock()
        {
            if (!YandexDevice.IsMobile) return false;

            float height = Mathf.Max(1, Screen.height);
            return Screen.width / height >= GameConstants.MobileLandscapeAspect;
        }

        private void OnEnable()
        {
            GameTexts.OnLanguageChanged += ApplyText;
            Refresh();
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyText;
            if (shown) IsBlocking = false;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool block = ShouldBlock();
            if (block == shown && (!block || root != null)) return;

            shown = block;
            IsBlocking = block;

            if (!block)
            {
                if (root != null) root.gameObject.SetActive(false);
                return;
            }

            EnsureHierarchy();
            if (root == null) return;

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            ApplyText();
        }

        private void ApplyText()
        {
            if (label != null) label.text = GameTexts.RotateDevice;
        }

        /// <summary>
        /// Оверлей висит на самом Canvas, а не внутри UiFrame: рамка в ландшафте ужата
        /// до узкой колонки, а перекрыть нужно весь экран.
        /// </summary>
        private void EnsureHierarchy()
        {
            if (root != null) return;

            Transform existing = transform.Find(ObjectName);
            if (existing != null)
            {
                root = existing as RectTransform;
                label = root != null ? root.GetComponentInChildren<Text>(true) : null;
                if (label != null) return;
            }

            if (root == null)
            {
                var go = new GameObject(ObjectName, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                root = go.GetComponent<RectTransform>();

                var blocker = go.AddComponent<Image>();
                blocker.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
                blocker.raycastTarget = true;
            }

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            if (label != null) return;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(root, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.3f);
            textRt.anchorMax = new Vector2(0.92f, 0.7f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            label = textGo.AddComponent<Text>();
            label.font = GoldCounterView.ResolveFont();
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 48;
        }
    }
}
