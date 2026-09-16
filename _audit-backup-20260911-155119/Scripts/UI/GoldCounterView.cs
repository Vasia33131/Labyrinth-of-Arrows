using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>Счётчик золота. Подписывается на Wallet.OnChanged, не двигает чужие кнопки.</summary>
    public class GoldCounterView : MonoBehaviour
    {
        public const string ObjectName = "GoldText";

        public Text label;

        private void Awake()
        {
            if (label == null) label = GetComponent<Text>();
            Refresh();
        }

        private void OnEnable()
        {
            Wallet.OnChanged += HandleChanged;
            GameTexts.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Wallet.OnChanged -= HandleChanged;
            GameTexts.OnLanguageChanged -= Refresh;
        }

        private void HandleChanged(int _)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (label == null) label = GetComponent<Text>();
            if (label == null) return;
            label.text = GameTexts.Gold(Wallet.Gold);
        }

        public static GoldCounterView FindOn(Transform root)
        {
            if (root == null) return null;

            Transform named = FindNamed(root, ObjectName);
            if (named != null)
            {
                var view = named.GetComponent<GoldCounterView>();
                if (view != null)
                {
                    view.enabled = true;
                    return view;
                }

                if (AuthoredUi.CanBuild)
                {
                    view = named.gameObject.AddComponent<GoldCounterView>();
                    view.enabled = true;
                    return view;
                }
            }

            GoldCounterView existing = root.GetComponentInChildren<GoldCounterView>(true);
            if (existing != null) existing.enabled = true;
            return existing;
        }

        public static GoldCounterView Bind(Text text)
        {
            if (text == null) return null;

            var view = text.GetComponent<GoldCounterView>();
            if (view == null)
            {
                if (!AuthoredUi.CanBuild) return null;
                view = text.gameObject.AddComponent<GoldCounterView>();
            }

            view.label = text;
            view.enabled = true;
            view.Refresh();
            return view;
        }

        public static GoldCounterView EnsureOnCanvas(Transform canvas, bool menuStyle)
        {
            if (canvas == null) return null;

            GoldCounterView existing = FindOn(canvas);
            if (existing != null)
            {
                if (existing.label == null) existing.label = existing.GetComponent<Text>();
                existing.Refresh();
                return existing;
            }

            if (!AuthoredUi.CanBuild)
            {
                AuthoredUi.Missing(ObjectName);
                return null;
            }

            var go = new GameObject(ObjectName, typeof(RectTransform));
            go.transform.SetParent(BannerSafeArea.ResolveContentRoot(canvas), false);

            var rt = go.GetComponent<RectTransform>();
            if (menuStyle)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -500f);
                rt.sizeDelta = new Vector2(920f, 56f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f);
                rt.sizeDelta = new Vector2(420f, 44f);
            }

            var text = go.AddComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = menuStyle ? 36 : 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = menuStyle
                ? new Color(GameConstants.NavyText.r, GameConstants.NavyText.g, GameConstants.NavyText.b, 0.78f)
                : Color.white;

            return Bind(text);
        }

        private static Font cachedFont;

        /// <summary>
        /// Шрифт из Resources. Встроенный шрифт Unity в WebGL рисует только ASCII:
        /// системного фолбэка в браузере нет, поэтому кириллица в билде исчезает.
        /// </summary>
        public static Font ResolveFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Resources.Load<Font>(GameConstants.UiFontResourcePath);
            if (cachedFont != null) return cachedFont;

            try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* ignore */ }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { /* ignore */ }
            }

            return cachedFont;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            Transform direct = root.Find(name);
            if (direct != null) return direct;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }

            return null;
        }
    }
}
