using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>Одна карточка фона на вкладке «Фон».</summary>
    public class BackgroundSlotView : MonoBehaviour
    {
        public string backgroundId;
        public Image background;
        public Image preview;
        public Text nameLabel;
        public Button actionButton;
        public Text actionLabel;
        public Text wornBadge;

        public void Bind(BackgroundData data, bool unlocked, bool selected, UnityAction onClick)
        {
            if (data == null) return;

            backgroundId = data.id;
            if (nameLabel != null)
            {
                string fallback = string.IsNullOrEmpty(data.displayName) ? data.id : data.displayName;
                string title = GameTexts.BackgroundName(data.id, fallback);
                if (!unlocked && !data.IsFree && !data.unlockByBattlePass)
                    nameLabel.text = $"{title}  {data.priceGold}";
                else
                    nameLabel.text = title;
            }

            PaintPreview(data);
            PlaceName();
            if (!Application.isPlaying || !SceneCanvas.LayoutLocked)
                LayoutParts();

            if (wornBadge != null)
            {
                wornBadge.text = GameTexts.Equipped;
                wornBadge.gameObject.SetActive(selected);
            }

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(!selected);
                actionButton.onClick.RemoveAllListeners();
                bool battlePassLocked = !unlocked && data.unlockByBattlePass;
                actionButton.interactable = !battlePassLocked;
                if (!selected && onClick != null && !battlePassLocked)
                    actionButton.onClick.AddListener(onClick);
            }

            if (actionLabel != null && !selected)
            {
                if (unlocked) actionLabel.text = GameTexts.Equip;
                else if (data.unlockByBattlePass) actionLabel.text = GameTexts.BattlePass;
                else actionLabel.text = GameTexts.Buy;
            }

            HideAllBackgrounds();
        }

        public void ApplyAuthoredLayout()
        {
            LayoutParts();
        }

        private void PaintPreview(BackgroundData data)
        {
            if (preview == null || data == null) return;

            if (data.sprite != null)
            {
                preview.sprite = data.sprite;
                preview.type = Image.Type.Simple;
                preview.preserveAspect = true;
                preview.raycastTarget = false;
                preview.color = Color.white;
                return;
            }

            SceneCanvas canvas = SceneCanvas.InScene;
            bool menu = BackgroundVisual.IsMenu(canvas);
            BackgroundVisual.Resolve(data, menu, canvas, out Sprite sprite, out Color color);

            preview.sprite = sprite;
            preview.type = Image.Type.Simple;
            preview.preserveAspect = sprite != null;
            preview.raycastTarget = false;
            preview.color = sprite != null ? color : data.PreviewTint;
        }

        public static BackgroundSlotView Create(Transform parent, BackgroundData data)
        {
            var go = new GameObject("BackgroundSlot_" + (data != null ? data.id : "unknown"),
                typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var view = go.AddComponent<BackgroundSlotView>();
            view.preview = CreateImage(go.transform, "Preview",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f), new Vector2(320f, 260f));
            view.nameLabel = CreateLabel(go.transform, "Name", string.Empty,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(340f, 38f), TextAnchor.MiddleCenter, 30);
            view.actionButton = CreateActionButton(go.transform);
            view.actionLabel = view.actionButton.GetComponentInChildren<Text>(true);
            view.wornBadge = CreateLabel(go.transform, "WornBadge", GameTexts.Equipped,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, -10f), new Vector2(132f, 40f), TextAnchor.MiddleCenter, 22);
            view.wornBadge.gameObject.SetActive(false);

            if (data != null) view.backgroundId = data.id;
            return view;
        }

        private static Image CreateImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, pos, size);

            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static Button CreateActionButton(Transform parent)
        {
            var go = new GameObject("ActionButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(236f, 56f));

            var image = go.AddComponent<Image>();
            Sprite face = ResolveBtn();
            if (face != null) image.sprite = face;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            CreateLabel(go.transform, "Label", GameTexts.Buy,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 24);
            return button;
        }

        private static Text CreateLabel(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
            TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (Mathf.Approximately(anchorMin.x, 0f) && Mathf.Approximately(anchorMax.x, 1f)
                && Mathf.Approximately(anchorMin.y, 0f) && Mathf.Approximately(anchorMax.y, 1f))
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = pivot;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                Place(rt, anchorMin, anchorMax, pivot, pos, size);
            }

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = GoldCounterView.ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = GameConstants.NavyText;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, fontSize - 10);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private void LayoutParts()
        {
            HideAllBackgrounds();
            Place(preview != null ? preview.rectTransform : null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f), new Vector2(320f, 260f));
            PlaceName();
            Place(actionButton != null ? actionButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(236f, 56f));
            Place(wornBadge != null ? wornBadge.rectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(236f, 52f));
            PaintActionFace();
            if (nameLabel != null)
            {
                nameLabel.alignment = TextAnchor.MiddleCenter;
                nameLabel.fontSize = 30;
                nameLabel.resizeTextForBestFit = true;
                nameLabel.resizeTextMinSize = 16;
                nameLabel.resizeTextMaxSize = 30;
            }

            if (preview != null) preview.transform.SetAsFirstSibling();
            if (nameLabel != null) nameLabel.transform.SetSiblingIndex(1);
            if (actionButton != null) actionButton.transform.SetAsLastSibling();
            if (wornBadge != null) wornBadge.transform.SetAsLastSibling();
        }

        private void HideAllBackgrounds()
        {
            HideImage(GetComponent<Image>(), true);
            HideImage(background, background != null && background.gameObject == gameObject);
            HideChild("Frame");
            HideChild("Window");
        }

        private void HideImage(Image image, bool keepRaycast)
        {
            if (image == null) return;
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = keepRaycast;
        }

        private void HideChild(string name)
        {
            Transform child = transform.Find(name);
            if (child == null) return;
            child.gameObject.SetActive(false);
            if (background != null && background.transform == child)
                background.enabled = false;
        }

        private void PlaceName()
        {
            var rt = nameLabel != null ? nameLabel.rectTransform : null;
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.075f, 0.878f);
            rt.anchorMax = new Vector2(0.925f, 0.961f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 pos, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private void PaintActionFace()
        {
            if (actionButton == null) return;
            var image = actionButton.targetGraphic as Image ?? actionButton.GetComponent<Image>();
            if (image == null) return;
            Sprite face = ResolveBtn();
            if (face != null) image.sprite = face;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
        }

        private static Sprite ResolveBtn()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art != null && art.btn != null) return art.btn;
            SceneCanvas canvas = SceneCanvas.InScene;
            return canvas != null ? canvas.btn : null;
        }
    }
}
