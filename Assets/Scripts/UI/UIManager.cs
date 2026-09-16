using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// HUD портрета: уровень, ходы, меню, настройки, подсказка / undo / рестарт,
    /// панели победы и поражения.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Ссылки")]
        public GameManager gameManager;
        public BonusSystem bonuses;

        [Header("HUD")]
        public Text levelText;
        public Text movesText;
        public Text arrowsText;
        public LevelProgressView progressView;
        public Text messageText;
        public Text hintText;

        [Header("Панели")]
        public GameObject winPanel;
        public GameObject losePanel;
        public GameObject settingsPanel;
        public SettingsPanel settings;
        public ShopPanel shop;
        public DailyHintsPanel dailyHintsPanel;

        [Header("Кнопки")]
        public Button menuButton;
        public Button settingsButton;
        public Button shopButton;
        public Button settingsCloseButton;
        public Button soundButton;
        public Text soundButtonLabel;
        public Button restartButton;
        public Button winRestartButton;
        public Button loseRestartButton;
        public Button nextLevelButton;

        [Header("Бонусы")]
        public Button hintButton;
        public Text hintCountText;
        public Button undoButton;
        public Text undoCountText;
        public Button extraMoveButton;
        public Text extraMoveCountText;
        public Button loseExtraMoveButton;

        public Color bonusDisabledColor = new Color(0.75f, 0.76f, 0.78f);
        public float messageDuration = 1.2f;
        [Min(0)] public int lowMovesThreshold = 2;

        [Header("Золото")]
        public Text goldText;
        public GoldCounterView goldCounter;

        private Coroutine messageRoutine;
        private Color bonusEnabledColor = Color.white;
        private bool endButtonAnchorsReady;
        private Vector2 nextLevelAnchorsMin;
        private Vector2 nextLevelAnchorsMax;
        private Vector2 winRestartAnchorsMin;
        private Vector2 winRestartAnchorsMax;
        private Vector2 loseRestartAnchorsMin;
        private Vector2 loseRestartAnchorsMax;
        private Vector2 loseExtraAnchorsMin;
        private Vector2 loseExtraAnchorsMax;

        private void Awake()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (progressView == null || progressView.gameObject.name == "ProgressRow")
                progressView = BindBadgeProgress();

            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (winRestartButton != null) winRestartButton.onClick.AddListener(OnRestartClicked);
            if (loseRestartButton != null) loseRestartButton.onClick.AddListener(OnRestartClicked);
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            ResolveShop();
            if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (shopButton != null) shopButton.onClick.AddListener(OnShopClicked);
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(HideSettings);
            if (soundButton != null && settings == null) soundButton.onClick.AddListener(OnSoundClicked);

            if (hintButton != null)
            {
                bonusEnabledColor = GetButtonColor(hintButton, bonusEnabledColor);
                hintButton.onClick.AddListener(OnHintClicked);
            }

            if (undoButton != null) undoButton.onClick.AddListener(OnUndoClicked);
            if (extraMoveButton != null) extraMoveButton.onClick.AddListener(OnExtraMoveClicked);
            if (loseExtraMoveButton != null) loseExtraMoveButton.onClick.AddListener(OnExtraMoveClicked);

            BindGoldCounter();
            SettingsPanel.ApplyAudio(settings != null ? settings.musicSource : null);
            HidePanels();
            HideSettings();
            HideShop();
            ClearMessage();
            ApplyTexts();
            CaptureEndButtonAnchors();
        }

        private void Start()
        {
            // Страховка, если билд открыли сразу на GameScene, минуя меню.
            YandexLoading.NotifyReady();
            GameAudio.BindUiClicks(transform);
        }

        private void OnEnable()
        {
            Hints.OnChanged += HandleStockChanged;
            UndoCharges.OnChanged += HandleStockChanged;
            ExtraMoves.OnChanged += HandleStockChanged;
            GameTexts.OnLanguageChanged += ApplyTexts;
            if (Bonuses != null) Bonuses.OnBonusesChanged += Refresh;
        }

        private void OnDisable()
        {
            Hints.OnChanged -= HandleStockChanged;
            UndoCharges.OnChanged -= HandleStockChanged;
            ExtraMoves.OnChanged -= HandleStockChanged;
            GameTexts.OnLanguageChanged -= ApplyTexts;
            if (bonuses != null) bonuses.OnBonusesChanged -= Refresh;
        }

        /// <summary>
        /// Подписи HUD и панелей итога на текущем языке YG2. Панели победы и поражения
        /// собраны в сцене, поэтому их текст переписывается здесь, а не при спавне.
        /// </summary>
        private void ApplyTexts()
        {
            RefreshCore();
            RefreshSoundLabel();

            if (winPanel != null)
                UiLabel.SetNamed(winPanel.transform, "TitleText", GameTexts.Win);
            if (losePanel != null)
                UiLabel.SetNamed(losePanel.transform, "TitleText", GameTexts.Lose);

            // HUD-рестарт — иконка без подписи, его Label не трогаем.
            ApplyWinActions();
            UiLabel.Set(winRestartButton, GameTexts.Restart);
            UiLabel.Set(loseRestartButton, GameTexts.Restart);
            UiLabel.Set(loseExtraMoveButton, GameTexts.ExtraMoveTitle);
        }

        private void HandleStockChanged(int _)
        {
            RefreshBonuses();
        }

        private BonusSystem Bonuses
        {
            get
            {
                if (bonuses == null && gameManager == null) gameManager = GameManager.Instance;
                if (bonuses == null && gameManager != null) bonuses = gameManager.bonuses;
                return bonuses;
            }
        }

        public void Refresh()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null) return;

            RefreshCore();
            UpdateProgress(true);
            RefreshBonuses();
            RefreshSoundLabel();
        }

        public void RefreshAfterLevelStart()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            RefreshCore();
            UpdateProgress(false);
            RefreshBonuses();
            RefreshSoundLabel();
        }

        private void RefreshCore()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null) return;

            if (levelText != null)
            {
                levelText.text = gameManager.IsSchoolMode
                    ? GameTexts.School(gameManager.CurrentLevelIndex)
                    : GameTexts.Level(gameManager.CurrentLevelIndex);
            }

            if (movesText != null)
            {
                movesText.text = GameTexts.Moves(gameManager.HasMoveLimit
                    ? gameManager.MovesLeft
                    : gameManager.MovesUsed);

                bool low = gameManager.HasMoveLimit && gameManager.MovesLeft <= lowMovesThreshold;
                movesText.color = low ? new Color(1f, 0.42f, 0.38f) : Color.white;
            }

            if (arrowsText != null)
                arrowsText.text = GameTexts.ArrowsLeft(gameManager.ArrowsLeft);

            if (goldCounter != null) goldCounter.Refresh();
            else if (goldText != null) goldText.text = GameTexts.Gold(Wallet.Gold);
        }

        private void BindGoldCounter()
        {
            if (goldCounter == null && goldText != null)
                goldCounter = goldText.GetComponent<GoldCounterView>();
            if (goldText == null && goldCounter != null) goldText = goldCounter.label;
            if (goldCounter != null) goldCounter.Refresh();
        }

        private void UpdateProgress(bool animate)
        {
            if (progressView == null || progressView.gameObject.name == "ProgressRow")
                progressView = BindBadgeProgress();
            if (progressView == null || gameManager == null) return;
            progressView.SetCampaignProgress(
                gameManager.CurrentLevelIndex,
                gameManager.ActiveLevelCount,
                animate);
        }

        private static LevelProgressView BindBadgeProgress()
        {
            GameObject badge = GameObject.Find("LevelBadge");
            if (badge == null) return FindObjectOfType<LevelProgressView>();

            var view = badge.GetComponent<LevelProgressView>();
            if (view != null) return view;
            if (AuthoredUi.CanBuild) return badge.AddComponent<LevelProgressView>();
            AuthoredUi.Missing("LevelBadge (LevelProgressView)");
            return FindObjectOfType<LevelProgressView>();
        }

        private void RefreshBonuses()
        {
            int hints = Hints.GetHintsCount();
            int undos = UndoCharges.GetCount();
            int extras = ExtraMoves.GetCount();

            if (hintCountText != null) hintCountText.text = hints.ToString();
            if (undoCountText != null) undoCountText.text = undos.ToString();
            if (extraMoveCountText != null) extraMoveCountText.text = extras.ToString();

            BonusSystem bonusSystem = Bonuses;
            if (bonusSystem == null)
            {
                if (hintButton != null) hintButton.interactable = false;
                if (undoButton != null) undoButton.interactable = false;
                if (extraMoveButton != null) extraMoveButton.interactable = false;
                return;
            }

            if (hintButton != null)
                hintButton.interactable = bonusSystem.CanUseHint || bonusSystem.CanOfferHintsAd;

            if (undoButton != null)
                undoButton.interactable = bonusSystem.CanUseUndo || bonusSystem.CanOfferUndoAd;

            if (extraMoveButton != null)
                extraMoveButton.interactable = bonusSystem.CanUseExtraMove || bonusSystem.CanOfferExtraMoveAd;

            if (loseExtraMoveButton != null)
            {
                bool offerOrUse = bonusSystem.CanUseExtraMove || bonusSystem.CanOfferExtraMoveAd;
                loseExtraMoveButton.gameObject.SetActive(offerOrUse);
                loseExtraMoveButton.interactable = offerOrUse;
            }
        }

        private static Color GetButtonColor(Button button, Color fallback)
        {
            var image = button.targetGraphic as Image;
            return image != null ? image.color : fallback;
        }

        public void SetHint(string hint)
        {
            if (hintText == null) return;
            hintText.text = hint;
            hintText.enabled = !string.IsNullOrEmpty(hint);
        }

        public void ShowMessage(string message)
        {
            ShowMessage(message, messageDuration);
        }

        /// <summary>Длинные тексты (например, описание управления) живут дольше обычной реплики.</summary>
        public void ShowMessage(string message, float seconds)
        {
            if (messageText == null)
            {
                GameLog.Info($"[UI] {message}");
                return;
            }

            messageText.text = message;
            messageText.enabled = true;

            if (messageRoutine != null) StopCoroutine(messageRoutine);
            if (gameObject.activeInHierarchy) messageRoutine = StartCoroutine(HideMessageRoutine(seconds));
        }

        public void ClearMessage()
        {
            if (messageRoutine != null)
            {
                StopCoroutine(messageRoutine);
                messageRoutine = null;
            }

            if (messageText == null) return;
            messageText.text = string.Empty;
            messageText.enabled = false;
        }

        private IEnumerator HideMessageRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            messageRoutine = null;
            ClearMessage();
        }

        public void HidePanels()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            HideDailyHints();
        }

        public void ShowWin()
        {
            ClearMessage();
            HideSettings();
            HideShop();
            HideDailyHints();
            ApplyWinActions();
            if (winPanel != null) winPanel.SetActive(true);
            FitDesktopEndButtons();
        }

        /// <summary>
        /// Школа 20: без «Дальше», слот той же кнопки — «Выйти» в меню.
        /// Уровни 1–19 и обычная кампания оставляют «Дальше».
        /// </summary>
        private void ApplyWinActions()
        {
            if (nextLevelButton == null) return;

            nextLevelButton.gameObject.SetActive(true);
            UiLabel.Set(nextLevelButton, IsSchoolFinalWin() ? GameTexts.Exit : GameTexts.NextLevel);
        }

        private bool IsSchoolFinalWin()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            return gameManager != null && gameManager.IsSchoolFinalLevel;
        }

        public void ShowLose()
        {
            ClearMessage();
            HideSettings();
            HideShop();
            HideDailyHints();
            if (losePanel != null) losePanel.SetActive(true);
            FitDesktopEndButtons();
        }

        /// <summary>
        /// ПК: Win/Lose сидят на всём BannerContent, якоря 44%×13% превращают
        /// «Дальше» / «Заново» в широкие полосы. Телефонные якоря не меняем.
        /// </summary>
        private void FitDesktopEndButtons()
        {
            if (!YandexDevice.IsDesktop) return;
            CaptureEndButtonAnchors();
            Canvas.ForceUpdateCanvases();
            FitDesktopEndButton(nextLevelButton, nextLevelAnchorsMin, nextLevelAnchorsMax);
            FitDesktopEndButton(winRestartButton, winRestartAnchorsMin, winRestartAnchorsMax);
            FitDesktopEndButton(loseRestartButton, loseRestartAnchorsMin, loseRestartAnchorsMax);
            FitDesktopEndButton(loseExtraMoveButton, loseExtraAnchorsMin, loseExtraAnchorsMax);
        }

        private void CaptureEndButtonAnchors()
        {
            if (endButtonAnchorsReady) return;
            RememberEndButtonAnchors(nextLevelButton, ref nextLevelAnchorsMin, ref nextLevelAnchorsMax);
            RememberEndButtonAnchors(winRestartButton, ref winRestartAnchorsMin, ref winRestartAnchorsMax);
            RememberEndButtonAnchors(loseRestartButton, ref loseRestartAnchorsMin, ref loseRestartAnchorsMax);
            RememberEndButtonAnchors(loseExtraMoveButton, ref loseExtraAnchorsMin, ref loseExtraAnchorsMax);
            endButtonAnchorsReady = true;
        }

        private static void RememberEndButtonAnchors(Button button, ref Vector2 min, ref Vector2 max)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            min = rt.anchorMin;
            max = rt.anchorMax;
        }

        private static void FitDesktopEndButton(Button button, Vector2 designedMin, Vector2 designedMax)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            var panel = rt != null ? rt.parent as RectTransform : null;
            if (panel == null) return;
            if ((designedMax - designedMin).sqrMagnitude < 0.0001f) return;

            Rect visual = OverlayCardFit.VisualSpriteRect(panel);
            if (visual.width < 32f || visual.height < 32f) return;

            float midX = (designedMin.x + designedMax.x) * 0.5f;
            float midY = (designedMin.y + designedMax.y) * 0.5f;
            float cx = Mathf.Lerp(visual.xMin, visual.xMax, midX);
            float cy = Mathf.Lerp(visual.yMin, visual.yMax, midY);

            float scale = Mathf.Min(1f, visual.width / GameConstants.UiReferenceWidth);
            Vector2 size = new Vector2(560f, 168f) * Mathf.Max(0.4f, scale);

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(cx - panel.rect.center.x, cy - panel.rect.center.y);
        }

        public void HideSettings()
        {
            if (settings != null)
            {
                settings.Close();
                return;
            }

            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void HideShop()
        {
            if (shop != null) shop.Close();
        }

        public void HideDailyHints()
        {
            if (dailyHintsPanel != null) dailyHintsPanel.Close();
        }

        private void OnRestartClicked()
        {
            // HUD-рестарт посреди уровня — ещё игровой экран, ролик был бы «в перерез».
            // После победы/поражения это уже кнопка на паузе, как в примерах п. 4.4.
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                RestartNow();
                return;
            }

            InterstitialAds.TryShowThen(InterstitialGate.Restart, RestartNow);
        }

        private void OnNextLevelClicked()
        {
            if (IsSchoolFinalWin())
            {
                InterstitialAds.TryShowThen(InterstitialGate.Menu, GoToMenuNow);
                return;
            }

            InterstitialAds.TryShowThen(InterstitialGate.NextLevel, NextLevelNow);
        }

        /// <summary>Вызывается либо сразу, либо из onClose интерстишела — уровень не собирается под роликом.</summary>
        private void RestartNow()
        {
            if (this == null) return;
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null) gameManager.Restart();
        }

        private void NextLevelNow()
        {
            if (this == null) return;
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null) gameManager.NextLevel();
        }

        private void OnMenuClicked()
        {
            InterstitialAds.TryShowThen(InterstitialGate.Menu, GoToMenuNow);
        }

        private void GoToMenuNow()
        {
            if (this == null) return;
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.GoToMainMenu();
                return;
            }

            GameplaySession.EndLevel();
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.MainMenuSceneName);
        }

        private void OnSettingsClicked()
        {
            HideShop();
            HideDailyHints();
            if (settings != null)
            {
                settings.Open();
                return;
            }

            if (settingsPanel == null) return;
            settingsPanel.SetActive(true);
            RefreshSoundLabel();
        }

        private void OnShopClicked()
        {
            InterstitialAds.TryShowThen(InterstitialGate.Shop, OpenShopNow);
        }

        private void OpenShopNow()
        {
            if (this == null) return;
            HideSettings();
            HideDailyHints();
            ResolveShop();
            if (shop != null) shop.Open();
        }

        private void ResolveShop()
        {
            if (shop == null)
            {
                var sceneCanvas = GetComponent<SceneCanvas>();
                if (sceneCanvas != null && sceneCanvas.shop != null)
                    shop = sceneCanvas.shop;
                if (shop == null)
                    shop = GetComponentInChildren<ShopPanel>(true);
                if (shop == null)
                    shop = ShopPanel.FindOn(transform);
            }
        }

        private void OnSoundClicked()
        {
            SettingsPanel.SetSoundOn(!SettingsPanel.SoundOn);
            SettingsPanel.ApplyAudio(settings != null ? settings.musicSource : null);
            RefreshSoundLabel();
        }

        private void RefreshSoundLabel()
        {
            if (soundButtonLabel == null) return;
            soundButtonLabel.text = GameTexts.SoundButton(SettingsPanel.SoundOn);
        }

        private void OnHintClicked()
        {
            if (Bonuses != null && Bonuses.CanUseHint)
            {
                Bonuses.UseHint();
                Refresh();
                return;
            }

            if (Bonuses != null && Bonuses.CanOfferHintsAd)
                OpenBoosterOffer(BoosterKind.Hint);
        }

        private void OpenBoosterOffer(BoosterKind kind)
        {
            HideSettings();
            HideShop();
            ResolveDailyHints();
            if (dailyHintsPanel == null)
                dailyHintsPanel = DailyHintsPanel.EnsureOnCanvas(transform);
            if (dailyHintsPanel == null)
            {
                AuthoredUi.Missing(DailyHintsPanel.OverlayName);
                return;
            }

            dailyHintsPanel.ShowOutOfStockOffer(kind);
        }

        private void ResolveDailyHints()
        {
            if (dailyHintsPanel != null) return;

            var sceneCanvas = GetComponent<SceneCanvas>();
            if (sceneCanvas != null)
                dailyHintsPanel = DailyHintsPanel.FindOn(sceneCanvas.transform);
            if (dailyHintsPanel == null)
                dailyHintsPanel = DailyHintsPanel.FindOn(transform);
            if (dailyHintsPanel == null)
                dailyHintsPanel = GetComponentInChildren<DailyHintsPanel>(true);
        }

        private void OnUndoClicked()
        {
            if (Bonuses != null && Bonuses.UndosLeft > 0)
            {
                Bonuses.UseUndo();
                Refresh();
                return;
            }

            if (Bonuses != null && Bonuses.CanOfferUndoAd)
                OpenBoosterOffer(BoosterKind.Undo);
        }

        private void OnExtraMoveClicked()
        {
            if (Bonuses != null && Bonuses.CanUseExtraMove)
            {
                Bonuses.UseExtraMove();
                Refresh();
                return;
            }

            if (Bonuses != null && Bonuses.CanOfferExtraMoveAd)
                OpenBoosterOffer(BoosterKind.ExtraMove);
        }
    }
}
