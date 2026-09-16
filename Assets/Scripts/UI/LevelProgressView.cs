using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Плашка LevelBadge — прогресс кампании слева направо.
    /// Тексты «Уровень» и «Ходы» рисуются сверху и не перезаписываются.
    /// </summary>
    public class LevelProgressView : MonoBehaviour
    {
        public const int MinLevel = 1;

        [Header("Ссылки")]
        public Slider slider;
        public Text percentText;
        public Image fillImage;
        public Image shineImage;
        public Image trackImage;
        public CharacterView characterView;

        [Header("Анимация")]
        [Range(0.2f, 0.45f)] public float animateDuration = 0.3f;
        public Color fillColor = new Color(56f / 255f, 204f / 255f, 92f / 255f, 1f);
        public Color fillPulseColor = new Color(0.55f, 0.95f, 0.62f, 1f);
        public Color trackColor = new Color(0.22f, 0.25f, 0.29f, 1f);

        private Coroutine animRoutine;
        private int displayedLevel = MinLevel;
        private int targetLevel = MinLevel;
        private int totalLevels = LevelCatalog.TargetCount;
        private bool completePlayed;

        private void Awake()
        {
            HideLegacySlider();
            EnsureBadgeFill();
            ApplyVisual(MinLevel, totalLevels);
        }

        private void Start()
        {
            SyncFromGame(false);
        }

        private void OnEnable()
        {
            HideLegacySlider();
            EnsureBadgeFill();
            SyncFromGame(false);
        }

        public void ResetForLevel()
        {
            completePlayed = false;
            SyncFromGame(false);
        }

        public void SetProgress(int percent, bool animate)
        {
            int total = Mathf.Max(1, totalLevels > 0 ? totalLevels : LevelCatalog.TargetCount);
            SetCampaignProgress(Mathf.Clamp(percent, MinLevel, total), total, animate);
        }

        public void SetCampaignProgress(int currentLevel, int levelCount, bool animate)
        {
            totalLevels = Mathf.Max(1, levelCount);
            int level = Mathf.Clamp(currentLevel, MinLevel, totalLevels);
            if (level < totalLevels) completePlayed = false;

            EnsureBadgeFill();

            if (!animate)
            {
                StopAnim();
                targetLevel = level;
                displayedLevel = level;
                ApplyVisual(level, totalLevels);
                if (level >= totalLevels) PlayCompleteFeedback();
                return;
            }

            if (animRoutine != null && targetLevel == level)
                return;
            if (animRoutine == null && displayedLevel == level)
                return;

            targetLevel = level;
            StopAnim();
            if (!isActiveAndEnabled)
            {
                displayedLevel = level;
                ApplyVisual(level, totalLevels);
                if (level >= totalLevels) PlayCompleteFeedback();
                return;
            }

            animRoutine = StartCoroutine(AnimateRoutine(level));
        }

        private void SyncFromGame(bool animate)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            SetCampaignProgress(gm.CurrentLevelIndex, gm.ActiveLevelCount, animate);
        }

        private IEnumerator AnimateRoutine(int toLevel)
        {
            float from = displayedLevel;
            float duration = Mathf.Clamp(animateDuration, 0.2f, 0.45f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                u = 1f - (1f - u) * (1f - u);
                ApplyVisual(Mathf.Lerp(from, toLevel, u), totalLevels);
                yield return null;
            }

            displayedLevel = toLevel;
            ApplyVisual(toLevel, totalLevels);
            animRoutine = null;

            if (toLevel >= totalLevels)
                yield return PulseRoutine();
        }

        private IEnumerator PulseRoutine()
        {
            PlayCompleteFeedback();

            if (UsesAuthoredSliderArt())
                yield break;

            const float dur = 0.28f;
            Color baseColor = fillColor;

            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float wave = Mathf.Sin((t / dur) * Mathf.PI);
                if (fillImage != null)
                    fillImage.color = Color.Lerp(baseColor, fillPulseColor, wave);
                yield return null;
            }

            if (fillImage != null)
                fillImage.color = baseColor;
        }

        private void PlayCompleteFeedback()
        {
            if (completePlayed) return;
            completePlayed = true;

            if (characterView == null) characterView = FindObjectOfType<CharacterView>();
            if (characterView == null) return;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Win) return;
            characterView.PlayHappy();
        }

        private void ApplyVisual(float levelValue, int levelCount)
        {
            int total = Mathf.Max(1, levelCount);
            float clamped = Mathf.Clamp(levelValue, MinLevel, total);
            float amount = Mathf.Clamp01(clamped / total);

            bool authored = UsesAuthoredSliderArt();

            if (fillImage != null)
            {
                fillImage.enabled = true;
                if (!authored)
                    fillImage.color = fillColor;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.fillAmount = amount;
            }

            if (trackImage != null)
            {
                trackImage.enabled = true;
                if (!authored)
                    trackImage.color = trackColor;
            }
        }

        private void EnsureBadgeFill()
        {
            fillColor = GameConstants.ProgressGreen;
            fillPulseColor = GameConstants.ProgressGreenPulse;
            trackColor = GameConstants.HudDark;

            Transform badge = ResolveBadge();
            if (badge == null) return;

            Transform leftoverHost = badge.Find("FillHost");
            if (leftoverHost != null)
                leftoverHost.gameObject.SetActive(false);

            if (fillImage == null)
            {
                Transform existing = badge.Find("ProgressFill");
                if (existing == null && leftoverHost != null)
                    existing = leftoverHost.Find("ProgressFill");
                fillImage = existing != null ? existing.GetComponent<Image>() : null;
            }

            if (fillImage == null)
            {
                if (!AuthoredUi.CanBuild)
                {
                    AuthoredUi.Missing("ProgressFill");
                    return;
                }

                var go = new GameObject("ProgressFill", typeof(RectTransform));
                go.transform.SetParent(badge, false);
                fillImage = go.AddComponent<Image>();
            }

            if (trackImage == null)
            {
                Transform face = badge.Find("Track");
                trackImage = face != null ? face.GetComponent<Image>() : null;
            }

            if (trackImage == null)
                trackImage = badge.GetComponent<Image>();

            ArtLibrary art = ArtLibrary.Current;
            if (!Application.isPlaying && !SceneCanvas.ArtLocked && art != null)
            {
                if (trackImage != null && trackImage.sprite == null && art.sliderTrack != null)
                    trackImage.sprite = art.sliderTrack;
                if (fillImage.sprite == null && art.sliderFill != null)
                    fillImage.sprite = art.sliderFill;
            }

            bool authored = UsesAuthoredSliderArt();

            if (!Application.isPlaying)
            {
                var fillRt = fillImage.rectTransform;
                if (fillRt.parent != badge)
                    fillRt.SetParent(badge, false);
                if (fillRt.GetSiblingIndex() != 0)
                    fillRt.SetSiblingIndex(0);
            }

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.raycastTarget = false;
            fillImage.enabled = true;

            if (authored)
            {
                fillImage.color = Color.white;
                Image badgeImage = badge.GetComponent<Image>();
                if (badgeImage != null && badgeImage != trackImage)
                    badgeImage.enabled = false;

                if (trackImage != null)
                {
                    trackImage.color = Color.white;
                    trackImage.type = Image.Type.Sliced;
                    trackImage.preserveAspect = false;
                    trackImage.raycastTarget = false;
                    trackImage.enabled = true;
                    if (trackImage.transform.parent != badge)
                        trackImage.transform.SetParent(badge, false);
                    if (trackImage.transform.GetSiblingIndex() != 1)
                        trackImage.transform.SetSiblingIndex(1);
                }

                var badgeMask = badge.GetComponent<Mask>();
                if (badgeMask != null)
                    badgeMask.enabled = false;
            }
            else
            {
                if (trackImage != null)
                {
                    trackImage.color = trackColor;
                    trackImage.enabled = true;
                }

                var mask = badge.GetComponent<Mask>();
                if (mask == null) mask = badge.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;

                Image badgeImage = trackImage != null ? trackImage : badge.GetComponent<Image>();
                if (fillImage.sprite == null)
                    fillImage.sprite = badgeImage != null ? badgeImage.sprite : fillImage.sprite;
                fillImage.color = fillColor;
            }

            if (characterView == null) characterView = FindObjectOfType<CharacterView>();
        }

        private bool UsesAuthoredSliderArt()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art == null) return false;

            Sprite track = trackImage != null ? trackImage.sprite : null;
            Sprite fill = fillImage != null ? fillImage.sprite : null;
            return (art.sliderTrack != null && (track == art.sliderTrack || fill == art.sliderTrack))
                || (art.sliderFill != null && (track == art.sliderFill || fill == art.sliderFill));
        }

        private Transform ResolveBadge()
        {
            if (gameObject.name == "LevelBadge")
                return transform;

            GameObject badge = GameObject.Find("LevelBadge");
            return badge != null ? badge.transform : transform;
        }

        private static void HideLegacySlider()
        {
            GameObject row = GameObject.Find("ProgressRow");
            if (row != null) row.SetActive(false);
        }

        private void StopAnim()
        {
            if (animRoutine == null) return;
            StopCoroutine(animRoutine);
            animRoutine = null;
        }

        private void OnDisable()
        {
            StopAnim();
        }
    }
}
