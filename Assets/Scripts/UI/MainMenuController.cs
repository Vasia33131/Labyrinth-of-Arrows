using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Стартовый экран: «Играть» открывает выбор режима, не грузит GameScene сразу.
    /// Панель ежедневных подсказок — после Game Ready API (п. 1.19.2), один раз за сессию.
    /// PlayModePanel — оверлей выбора режима, его Open/Close панель не повторяет.
    /// Возврат с уровня снова грузит MainMenu; флаг сессии не даёт показать панель повторно.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public Button playButton;
        public Button settingsButton;
        public Button shopButton;
        public Text titleText;
        public Text levelLabel;
        public SettingsPanel settingsPanel;
        public ShopPanel shopPanel;
        public PlayModePanel playModePanel;
        public DailyHintsPanel dailyHintsPanel;
        public Text goldText;
        public GoldCounterView goldCounter;

        private void Awake()
        {
            ResolveSettingsPanel();
            ResolveShopPanel();
            ResolvePlayModePanel();
            ResolveDailyHintsPanel();
            ApplyTexts();
            if (playButton != null) playButton.onClick.AddListener(Play);
            else AuthoredUi.Missing("PlayButton");
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            else AuthoredUi.Missing("SettingsButton");
            if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
            else AuthoredUi.Missing("ShopButton");
            RefreshLevelLabel();
            BindGoldCounter();
            SettingsPanel.ApplyAudio(settingsPanel != null ? settingsPanel.musicSource : null);
            if (settingsPanel != null) settingsPanel.Close();
            if (shopPanel != null) shopPanel.Close();
            if (playModePanel != null) playModePanel.Close();
            if (dailyHintsPanel != null) dailyHintsPanel.Close();
        }

        private void Start()
        {
            // Меню собрано и кнопки уже кликабельны — только теперь Game Ready API.
            YandexLoading.NotifyReady();
            GameAudio.BindUiClicks(transform);
            StartCoroutine(ShowDailyHintsAfterGameReady());
        }

        private void OnEnable()
        {
            GameTexts.OnLanguageChanged += ApplyTexts;
        }

        private void OnDisable()
        {
            GameTexts.OnLanguageChanged -= ApplyTexts;
        }

        /// <summary>Подписи меню на текущем языке YG2. Вызывается и при смене языка SDK.</summary>
        private void ApplyTexts()
        {
            UiLabel.Set(titleText, GameTexts.GameTitle);
            UiLabel.Set(playButton, GameTexts.Play);
            UiLabel.Set(settingsButton, GameTexts.Settings);
            UiLabel.Set(shopButton, GameTexts.Shop);
            RefreshLevelLabel();
        }

        /// <summary>
        /// П. 1.19.2: ежедневка только после GRA, когда loading-cover уже скрыт.
        /// GRA не откладываем — NotifyReady уходит сразу, панель ждёт флаг.
        /// </summary>
        private IEnumerator ShowDailyHintsAfterGameReady()
        {
            while (!YandexLoading.GameReadyCalled)
                yield return null;

            yield return null;
            TryShowDailyHintsOnEnter();
            while (dailyHintsPanel != null && dailyHintsPanel.HasPendingShow)
            {
                yield return null;
                dailyHintsPanel.FlushPendingAfterAds();
            }
        }

        public void Play()
        {
            CloseDailyHints();
            CloseSettings();
            CloseShop();
            ResolvePlayModePanel();
            if (playModePanel != null) playModePanel.Open();
        }

        public void OpenSettings()
        {
            CloseDailyHints();
            ClosePlayMode();
            CloseShop();
            ResolveSettingsPanel();
            if (settingsPanel != null) settingsPanel.Open();
        }

        public void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.Close();
        }

        public void OpenShop()
        {
            InterstitialAds.TryShowThen(InterstitialGate.Shop, OpenShopNow);
        }

        private void OpenShopNow()
        {
            if (this == null) return;
            CloseDailyHints();
            ClosePlayMode();
            CloseSettings();
            ResolveShopPanel();
            if (shopPanel != null) shopPanel.Open();
        }

        public void ClosePlayMode()
        {
            if (playModePanel != null) playModePanel.Close();
        }

        public void CloseShop()
        {
            if (shopPanel != null) shopPanel.Close();
        }

        public void CloseDailyHints()
        {
            if (dailyHintsPanel != null) dailyHintsPanel.Close();
        }

        private void TryShowDailyHintsOnEnter()
        {
            if (DailyHintsPanel.WasShownThisSession) return;

            ResolveDailyHintsPanel();
            if (dailyHintsPanel == null)
                dailyHintsPanel = DailyHintsPanel.EnsureOnCanvas(transform);
            if (dailyHintsPanel == null) return;

            dailyHintsPanel.ShowOnMenuEnter();
        }

        private void ResolveSettingsPanel()
        {
            if (settingsPanel != null) return;

            var sceneCanvas = GetComponent<SceneCanvas>();
            if (sceneCanvas != null && sceneCanvas.settings != null)
                settingsPanel = sceneCanvas.settings;
            if (settingsPanel == null)
                settingsPanel = GetComponentInChildren<SettingsPanel>(true);
            if (settingsPanel == null)
                settingsPanel = FindObjectOfType<SettingsPanel>(true);

            if (settingsPanel == null)
                AuthoredUi.Missing("SettingsOverlay");
            else if (sceneCanvas != null && sceneCanvas.settings == null)
                sceneCanvas.settings = settingsPanel;
        }

        private void ResolveShopPanel()
        {
            if (shopPanel == null)
            {
                var sceneCanvas = GetComponent<SceneCanvas>();
                if (sceneCanvas != null && sceneCanvas.shop != null)
                    shopPanel = sceneCanvas.shop;
                if (shopPanel == null)
                    shopPanel = GetComponentInChildren<ShopPanel>(true);
                if (shopPanel == null)
                    shopPanel = ShopPanel.FindOn(transform);

                if (shopPanel == null)
                    AuthoredUi.Missing(ShopPanel.OverlayName);
                else if (sceneCanvas != null && sceneCanvas.shop == null)
                    sceneCanvas.shop = shopPanel;
            }

            if (shopButton == null)
            {
                shopButton = ShopPanel.FindShopButton(transform);
                if (shopButton == null) AuthoredUi.Missing(ShopPanel.ButtonName);
            }
        }

        private void ResolvePlayModePanel()
        {
            if (playModePanel == null)
                playModePanel = PlayModePanel.FindOn(transform);
            if (playModePanel == null)
                AuthoredUi.Missing(PlayModePanel.OverlayName);
        }

        private void ResolveDailyHintsPanel()
        {
            if (dailyHintsPanel == null)
                dailyHintsPanel = DailyHintsPanel.FindOn(transform);
        }

        public void RefreshLevelLabel()
        {
            if (levelLabel == null) return;

            if (PlayProgress.IsSchool)
            {
                levelLabel.text = GameTexts.School(PlayProgress.SchoolLevel);
                return;
            }

            levelLabel.text = GameTexts.Level(PlayProgress.CampaignLevel);
        }

        private void BindGoldCounter()
        {
            if (goldCounter == null) goldCounter = GoldCounterView.FindOn(transform);
            if (goldCounter == null && goldText != null)
                goldCounter = goldText.GetComponent<GoldCounterView>();
            if (goldText == null && goldCounter != null) goldText = goldCounter.label;
            if (goldCounter == null)
                AuthoredUi.Missing(GoldCounterView.ObjectName);
            else
                goldCounter.Refresh();
        }
    }
}
