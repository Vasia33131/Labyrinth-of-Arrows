using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Собирает слайдер прогресса над персонажем и проставляет ссылки.
    /// Меню: Tools/Unpuzzle/Setup Level Progress Slider
    /// </summary>
    public static class LevelProgressSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string CapsuleSpritePath = "Assets/Art/Sprites/ui_capsule.png";
        private const string RoundedSpritePath = "Assets/Art/Sprites/ui_rounded.png";
        private const string ProgressVersionKey = "Unpuzzle.LevelProgressVersion";
        private const int ProgressVersion = 3;
        private const int PixelsPerUnit = 128;

        private static readonly Color TrackColor = GameConstants.ProgressTrack;
        private static readonly Color FillColor = GameConstants.ProgressGreen;
        private static readonly Color LabelColor = Color.white;

        [InitializeOnLoadMethod]
        private static void AutoEmbedAfterCompile()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt(ProgressVersionKey, 0) >= ProgressVersion) return;
            if (!File.Exists(GameScenePath)) return;

            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (EditorPrefs.GetInt(ProgressVersionKey, 0) >= ProgressVersion) return;
                    SetupLevelProgressSlider();
                };
            };
        }

        [MenuItem("Tools/Unpuzzle/Setup Level Progress Slider", priority = 6)]
        public static void SetupLevelProgressSlider()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Unpuzzle] Выйди из Play Mode и повтори Setup Level Progress Slider.");
                return;
            }

            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning("[Unpuzzle] GameScene нет — сначала Tools/Unpuzzle/Setup Screen Layout.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (GameObject.Find("CharacterZone") == null)
                ScreenLayoutSetup.EmbedIntoOpenScene();

            EmbedIntoOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(ProgressVersionKey, ProgressVersion);
            Validate();
            Debug.Log("[Unpuzzle] Плашка LevelBadge теперь прогресс-бар кампании.");
        }

        public static void EmbedIntoOpenScene()
        {
            GameObject badge = GameObject.Find("LevelBadge");
            if (badge == null)
            {
                Debug.LogError("[Unpuzzle] LevelBadge не найдена — прогресс-бар не собран.");
                return;
            }

            GameObject legacy = GameObject.Find("ProgressRow");
            if (legacy != null)
            {
                legacy.SetActive(false);
                EditorUtility.SetDirty(legacy);
            }

            var view = badge.GetComponent<LevelProgressView>();
            if (view == null) view = badge.AddComponent<LevelProgressView>();
            view.trackImage = badge.GetComponent<Image>();
            view.fillColor = GameConstants.ProgressGreen;
            view.trackColor = GameConstants.HudDark;
            view.characterView = Object.FindObjectOfType<CharacterView>();

            var builder = Object.FindObjectOfType<ScreenLayoutBuilder>();
            if (builder != null)
            {
                builder.progressRow = null;
                EditorUtility.SetDirty(builder);
                builder.Apply();
            }

            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null)
            {
                ui.progressView = view;
                EditorUtility.SetDirty(ui);
            }

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(badge.scene);
        }

        private static RectTransform BuildProgressRow(RectTransform characterZone, RectTransform characterSlot)
        {
            Sprite capsule = AssetDatabase.LoadAssetAtPath<Sprite>(CapsuleSpritePath);
            if (capsule == null) capsule = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);

            RectTransform row = FindNamed(characterZone, "ProgressRow");
            if (row == null)
            {
                var go = new GameObject("ProgressRow", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(characterZone, false);
                row = go.GetComponent<RectTransform>();
            }

            var group = row.GetComponent<CanvasGroup>();
            if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = false;

            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(220f, 68f);
            row.anchoredPosition = new Vector2(0f, 80f);

            if (characterSlot != null)
            {
                int slotIndex = characterSlot.GetSiblingIndex();
                row.SetSiblingIndex(Mathf.Max(0, slotIndex));
            }

            Slider slider = EnsureSlider(row, capsule);
            Image background = slider != null ? slider.targetGraphic as Image : null;
            Text label = EnsurePercentLabel(row);
            Image fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            Image shine = EnsureShine(slider.fillRect, capsule);

            var view = row.GetComponent<LevelProgressView>();
            if (view == null) view = row.gameObject.AddComponent<LevelProgressView>();
            view.slider = slider;
            view.percentText = label;
            view.fillImage = fillImage;
            view.shineImage = shine;
            view.characterView = Object.FindObjectOfType<CharacterView>();
            view.fillColor = FillColor;
            view.fillPulseColor = GameConstants.ProgressGreenPulse;
            view.trackColor = TrackColor;
            view.trackImage = background;
            view.animateDuration = 0.3f;
            view.ResetForLevel();
            return row;
        }

        private static Text EnsurePercentLabel(RectTransform row)
        {
            Transform existing = row.Find("ProgressLabel");
            Text label = existing != null ? existing.GetComponent<Text>() : null;
            if (label == null)
            {
                var go = new GameObject("ProgressLabel", typeof(RectTransform));
                go.transform.SetParent(row, false);
                label = go.AddComponent<Text>();
            }

            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            label.text = "1 / 100";
            label.font = GetDefaultFont();
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.color = LabelColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.transform.SetAsLastSibling();
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static Slider EnsureSlider(RectTransform row, Sprite capsule)
        {
            Transform existing = row.Find("ProgressSlider");
            Slider slider = existing != null ? existing.GetComponent<Slider>() : null;
            if (slider == null)
            {
                var go = new GameObject("ProgressSlider", typeof(RectTransform));
                go.transform.SetParent(row, false);
                slider = go.AddComponent<Slider>();
            }

            var rt = slider.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(10f, 6f);
            rt.offsetMax = new Vector2(-10f, -6f);

            Image background = EnsureImage(rt, "Background", capsule, TrackColor, true);
            Stretch(background.rectTransform);
            background.rectTransform.SetAsFirstSibling();

            RectTransform fillArea = EnsureRect(rt, "Fill Area");
            Stretch(fillArea);
            fillArea.SetSiblingIndex(1);

            Image fill = EnsureImage(fillArea, "Fill", capsule, FillColor, true);
            Stretch(fill.rectTransform);

            Transform handleArea = rt.Find("Handle Slide Area");
            if (handleArea != null) handleArea.gameObject.SetActive(false);
            Transform handle = rt.Find("Handle");
            if (handle != null) handle.gameObject.SetActive(false);

            slider.minValue = 1f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 1f;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.targetGraphic = background;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = null;
            return slider;
        }

        private static Image EnsureShine(RectTransform fillRect, Sprite capsule)
        {
            if (fillRect == null) return null;

            Transform existing = fillRect.Find("Shine");
            Image shine = existing != null ? existing.GetComponent<Image>() : null;
            if (shine == null)
            {
                var go = new GameObject("Shine", typeof(RectTransform));
                go.transform.SetParent(fillRect, false);
                shine = go.AddComponent<Image>();
            }

            Stretch(shine.rectTransform);
            shine.sprite = capsule;
            shine.type = capsule != null ? Image.Type.Sliced : Image.Type.Simple;
            shine.color = new Color(1f, 1f, 1f, 0f);
            shine.raycastTarget = false;
            return shine;
        }

        private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color, bool sliced)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                image = go.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform found) return found;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform FindCharacterZone()
        {
            var builder = Object.FindObjectOfType<ScreenLayoutBuilder>();
            if (builder != null && builder.characterZone != null)
                return builder.characterZone;

            GameObject found = GameObject.Find("CharacterZone");
            return found != null ? found.transform as RectTransform : null;
        }

        private static RectTransform FindNamed(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform child = parent.Find(name);
            return child as RectTransform;
        }

        private static Font GetDefaultFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* ignore */ }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { /* ignore */ }
            }

            return font;
        }

        private static void EnsureCapsuleSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(CapsuleSpritePath) != null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Sprites"))
                AssetDatabase.CreateFolder("Assets/Art", "Sprites");

            const int width = 128;
            const int height = 32;
            const int radius = 16;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = x >= radius && x < width - radius;
                    if (!inside)
                    {
                        int cx = x < radius ? radius : width - radius - 1;
                        int cy = radius;
                        float dx = x - cx;
                        float dy = y - cy;
                        inside = dx * dx + dy * dy <= radius * radius;
                    }

                    pixels[y * width + x] = inside ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(CapsuleSpritePath));
            File.WriteAllBytes(CapsuleSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CapsuleSpritePath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(CapsuleSpritePath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = new Vector4(radius, radius, radius, radius);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = new Vector4(radius, radius, radius, radius);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void Validate()
        {
            GameObject badge = GameObject.Find("LevelBadge");
            if (badge == null || badge.GetComponent<LevelProgressView>() == null)
                Debug.LogError("[Unpuzzle] LevelBadge без LevelProgressView.");

            var ui = Object.FindObjectOfType<UIManager>();
            if (ui == null || ui.progressView == null || ui.progressView.gameObject.name != "LevelBadge")
                Debug.LogError("[Unpuzzle] UIManager.progressView должен быть на LevelBadge.");
        }
    }
}
