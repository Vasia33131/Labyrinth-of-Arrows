using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>Одна карточка скина на вкладке «Персонаж».</summary>
    public class CharacterSkinSlotView : MonoBehaviour
    {
        /// <summary>
        /// Кнопка покупки/надевания — платящий CTA, поэтому держим её не ниже 130 логических
        /// единиц: на телефоне 412 CSS px это ~45 px. Прежние 236×56 давали 19 px по высоте.
        /// Клетка сетки выросла с 400×460 до 400×540, чтобы превью не ужималось (см. ShopPanel).
        /// </summary>
        public static readonly Vector2 ActionButtonSize = new Vector2(300f, 130f);
        public static readonly Vector2 ActionButtonOffset = new Vector2(0f, 12f);
        public static readonly Vector2 PreviewSize = new Vector2(380f, 348f);
        public static readonly Vector2 PreviewOffset = new Vector2(0f, 50f);
        public static readonly Vector2 NameOffset = new Vector2(0f, -6f);

        public string skinId;
        public Image background;
        public Image preview;
        public Text nameLabel;
        public Button actionButton;
        public Text actionLabel;
        public Text wornBadge;

        public void Bind(CharacterSkinData skin, bool unlocked, bool selected, UnityAction onClick)
        {
            if (skin == null) return;

            skinId = skin.id;
            if (nameLabel != null)
            {
                string fallback = string.IsNullOrEmpty(skin.displayName) ? skin.id : skin.displayName;
                nameLabel.text = GameTexts.SkinName(skin.id, fallback);
            }

            PaintPreview(skin);
            if (!Application.isPlaying || !SceneCanvas.LayoutLocked)
                LayoutParts();

            if (wornBadge != null)
                wornBadge.gameObject.SetActive(false);

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(true);
                actionButton.onClick.RemoveAllListeners();
                bool battlePassLocked = !unlocked && skin.unlockByBattlePass;
                actionButton.interactable = !selected && !battlePassLocked;
                if (!selected && onClick != null && !battlePassLocked)
                    actionButton.onClick.AddListener(onClick);
            }

            if (actionLabel != null)
            {
                if (selected) actionLabel.text = GameTexts.Equipped;
                else if (unlocked) actionLabel.text = GameTexts.Equip;
                else if (skin.unlockByBattlePass) actionLabel.text = GameTexts.BattlePass;
                else actionLabel.text = GameTexts.Price(skin.priceGold);

                actionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                actionLabel.resizeTextForBestFit = true;
                actionLabel.resizeTextMinSize = 12;
            }

            HideAllBackgrounds();
        }

        public void ApplyAuthoredLayout()
        {
            LayoutParts();
        }

        private void PaintPreview(CharacterSkinData skin)
        {
            if (preview == null || skin == null) return;

            ArtLibrary art = ArtLibrary.Current;
            Sprite sprite = skin.ResolveIdle(art);
            preview.sprite = sprite;
            preview.type = Image.Type.Simple;
            preview.preserveAspect = sprite != null;
            preview.raycastTarget = false;
            preview.color = sprite != null ? skin.ColorForIdle() : skin.PreviewTint;
        }

        public static CharacterSkinSlotView Create(Transform parent, CharacterSkinData skin)
        {
            var go = new GameObject("SkinSlot_" + (skin != null ? skin.id : "unknown"), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var view = go.AddComponent<CharacterSkinSlotView>();
            view.preview = CreateImage(go.transform, "Preview",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                PreviewOffset, PreviewSize);
            view.nameLabel = CreateLabel(go.transform, "Name", string.Empty,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                NameOffset, new Vector2(340f, 38f), TextAnchor.MiddleCenter, 30);
            view.actionButton = CreateActionButton(go.transform);
            view.actionLabel = view.actionButton.GetComponentInChildren<Text>(true);
            view.wornBadge = CreateLabel(go.transform, "WornBadge", GameTexts.Equipped,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, -10f), new Vector2(132f, 40f), TextAnchor.MiddleCenter, 22);
            view.wornBadge.gameObject.SetActive(false);

            if (skin != null) view.skinId = skin.id;
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
                ActionButtonOffset, ActionButtonSize);

            var image = go.AddComponent<Image>();
            Sprite face = ResolveBtn();
            if (face != null) image.sprite = face;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            UiTapGuard.ApplyTo(image);

            CreateLabel(go.transform, "Label", GameTexts.Equip,
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

        private void LayoutParts()
        {
            HideAllBackgrounds();
            Place(preview != null ? preview.rectTransform : null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                PreviewOffset, PreviewSize);
            Place(nameLabel != null ? nameLabel.rectTransform : null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                NameOffset, new Vector2(340f, 38f));
            Place(actionButton != null ? actionButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                ActionButtonOffset, ActionButtonSize);
            Place(wornBadge != null ? wornBadge.rectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                ActionButtonOffset, new Vector2(ActionButtonSize.x, ActionButtonSize.y - 4f));
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
