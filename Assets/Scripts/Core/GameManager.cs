using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unpuzzle
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene References")]
        public Transform levelRoot;
        public InputHandler inputHandler;
        public UIManager uiManager;
        public LevelGenerator levelGenerator;
        public ArrowController arrowPrefab;
        public BonusSystem bonuses;
        public ScreenLayoutBuilder screenLayout;
        public ArrowOrbitDirector orbitDirector;
        public CharacterView characterView;
        public CandyBoxView candyBoxView;

        [Header("Boot")]
        [Tooltip("ON — демо из 4 стрелок. OFF — Resources/Levels/level_XX.json.")]
        public bool autoSpawnTestLayout;

        [Min(1)] public int startLevelIndex = 1;
        public bool useSavedProgress = true;

        [Header("Правила")]
        [Min(0)] public int movesLimit = 8;

        [Tooltip("Классический Unpuzzle: выключено. Оставь пустым.")]
        public ColorPriorityConfig colorPriority;

        public OccupancyGrid Occupancy { get; private set; }

        public GameState State { get; private set; } = GameState.Idle;
        public int CurrentLevelIndex { get; private set; } = 1;
        public int ArrowsLeft { get; private set; }
        public int TotalArrows { get; private set; }
        public int MovesUsed { get; private set; }
        public int BonusMoves { get; private set; }

        public int CampaignLevelCount => Mathf.Max(1, LevelCatalog.Count);
        public bool IsSchoolMode => PlayProgress.IsSchool;
        public bool IsSchoolFinalLevel => IsSchoolMode && CurrentLevelIndex >= SchoolLevelCatalog.TargetCount;
        public int ActiveLevelCount => IsSchoolMode
            ? Mathf.Max(1, SchoolLevelCatalog.Count)
            : CampaignLevelCount;

        public float LevelProgress01
        {
            get
            {
                int total = ActiveLevelCount;
                return Mathf.Clamp01(CurrentLevelIndex / (float)total);
            }
        }

        public int LevelProgressPercent
        {
            get
            {
                int total = ActiveLevelCount;
                int current = Mathf.Clamp(CurrentLevelIndex, 1, total);
                int percent = Mathf.RoundToInt((current / (float)total) * 100f);
                return Mathf.Clamp(percent, 1, 100);
            }
        }

        public bool HasMoveLimit => movesLimit > 0;
        public int TotalMoves => movesLimit + BonusMoves;
        public int MovesLeft => HasMoveLimit ? Mathf.Max(0, TotalMoves - MovesUsed) : int.MaxValue;

        public IReadOnlyList<ArrowController> ActiveArrows => activeArrows;
        public IReadOnlyList<BookController> AllBooks => allBooks;
        public IReadOnlyList<PointerController> AllPointers => allPointers;
        public IReadOnlyList<BackpackController> AllBackpacks => allBackpacks;
        public bool CanUndo => undoSnapshot != null;
        public bool IsIntroPlaying { get; private set; }
        public bool IsBlockedBumpPlaying => blockedBumpLocks > 0;

        private const float IntroStagger = 0.04f;

        /// <summary>Описание управления читают, а не проглядывают — обычных 1.2 с на него мало.</summary>
        private const float ControlsMessageSeconds = 5f;

        private readonly List<ArrowController> activeArrows = new List<ArrowController>();
        private readonly List<ArrowController> allArrows = new List<ArrowController>();
        private readonly List<BookController> allBooks = new List<BookController>();
        private readonly List<PointerController> allPointers = new List<PointerController>();
        private readonly List<BackpackController> allBackpacks = new List<BackpackController>();
        private LevelSnapshot undoSnapshot;
        private Coroutine introRoutine;
        private int blockedBumpLocks;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Occupancy = new OccupancyGrid();
        }

        private void OnEnable()
        {
            ArrowController.OnArrowRemoved += HandleArrowRemoved;
        }

        private void OnDisable()
        {
            ArrowController.OnArrowRemoved -= HandleArrowRemoved;
        }

        private void Start()
        {
            Hints.TryClaimDailyHints();
            InterstitialAds.MarkHasPlayed();
            if (inputHandler == null) inputHandler = FindObjectOfType<InputHandler>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
            if (levelRoot == null)
            {
                GameObject levelRootObject = GameObject.Find("LevelRoot");
                if (levelRootObject != null) levelRoot = levelRootObject.transform;
            }

            if (bonuses == null) bonuses = GetComponent<BonusSystem>();
            if (bonuses != null && bonuses.gameManager == null) bonuses.gameManager = this;
            if (levelGenerator == null) levelGenerator = GetComponent<LevelGenerator>();
            if (screenLayout == null) screenLayout = FindObjectOfType<ScreenLayoutBuilder>();
            if (orbitDirector == null) orbitDirector = GetComponent<ArrowOrbitDirector>();
            if (orbitDirector == null) orbitDirector = FindObjectOfType<ArrowOrbitDirector>();
            if (characterView == null) characterView = FindObjectOfType<CharacterView>();
            if (candyBoxView == null) candyBoxView = FindObjectOfType<CandyBoxView>();
            if (screenLayout != null) screenLayout.Apply();

            ResolveArrowPrefab();
            BindGenerator();

            if (autoSpawnTestLayout)
            {
                SpawnTestLevel();
                return;
            }

            int index = startLevelIndex;
            if (useSavedProgress)
            {
                if (PlayProgress.IsSchool)
                    index = PlayProgress.SchoolLevel;
                else if (PlayProgress.HasCampaignProgress)
                    index = PlayProgress.CampaignLevel;
            }

            LoadLevel(index);
        }

        public void LoadLevel(int index)
        {
            BindGenerator();
            ResolveArrowPrefab();

            if (levelRoot == null)
            {
                GameLog.Error("[GameManager] LevelRoot is not assigned.");
                return;
            }

            CurrentLevelIndex = Mathf.Clamp(index, 1, ActiveLevelCount);
            if (useSavedProgress)
                PlayProgress.SaveActiveLevel(CurrentLevelIndex);

            if (levelGenerator == null)
            {
                GameLog.Error("[GameManager] LevelGenerator не назначен.");
                return;
            }

            int count = BuildActiveLevel();
            if (count <= 0)
            {
                GameLog.Warn($"[GameManager] Уровень {CurrentLevelIndex} не собрался — демо-раскладка.");
                SpawnTestLevel();
                return;
            }

            CaptureSpawnedArrows();
            levelGenerator.FitCameraToBoard();

            if (levelGenerator.LastLoaded != null && levelGenerator.LastLoaded.movesLimit > 0)
                movesLimit = levelGenerator.LastLoaded.movesLimit;

            BeginLevel();
            string modeLabel = IsSchoolMode ? "Школа" : "Уровень";
            GameLog.Info($"[GameManager] {modeLabel} {CurrentLevelIndex}/{ActiveLevelCount}: стрелок {ArrowsLeft}, ходов {(HasMoveLimit ? movesLimit.ToString() : "∞")}.");
        }

        public void UseArrowsFromScene()
        {
            if (levelRoot == null)
            {
                GameLog.Error("[GameManager] LevelRoot is not assigned.");
                return;
            }

            activeArrows.Clear();
            activeArrows.AddRange(levelRoot.GetComponentsInChildren<ArrowController>(true));
            allArrows.Clear();
            allArrows.AddRange(activeArrows);
            allBooks.Clear();
            allBooks.AddRange(levelRoot.GetComponentsInChildren<BookController>(true));
            allPointers.Clear();
            allPointers.AddRange(levelRoot.GetComponentsInChildren<PointerController>(true));
            allBackpacks.Clear();
            allBackpacks.AddRange(levelRoot.GetComponentsInChildren<BackpackController>(true));
            BeginLevel();
        }

        public void SpawnTestLevel()
        {
            BindGenerator();
            ResolveArrowPrefab();

            if (levelRoot == null || levelGenerator == null || arrowPrefab == null)
            {
                GameLog.Error("[GameManager] Нет LevelRoot / LevelGenerator / Arrow prefab.");
                return;
            }

            int count = levelGenerator.BuildFromData(DemoLevel.Create());
            if (count <= 0)
            {
                GameLog.Error("[GameManager] Демо-уровень не собрался.");
                return;
            }

            CaptureSpawnedArrows();
            levelGenerator.FitCameraToBoard();
            if (levelGenerator.LastLoaded != null && levelGenerator.LastLoaded.movesLimit > 0)
                movesLimit = levelGenerator.LastLoaded.movesLimit;

            BeginLevel();
        }

        private void CaptureSpawnedArrows()
        {
            activeArrows.Clear();
            allArrows.Clear();
            allBooks.Clear();
            allPointers.Clear();
            allBackpacks.Clear();
            activeArrows.AddRange(levelGenerator.spawnedArrows);
            allArrows.AddRange(activeArrows);
            if (levelGenerator.spawnedBooks != null)
                allBooks.AddRange(levelGenerator.spawnedBooks);
            if (levelGenerator.spawnedPointers != null)
                allPointers.AddRange(levelGenerator.spawnedPointers);
            if (levelGenerator.spawnedBackpacks != null)
                allBackpacks.AddRange(levelGenerator.spawnedBackpacks);
        }

        private void BeginLevel()
        {
            StopLevelIntro();

            if (Occupancy == null) Occupancy = new OccupancyGrid();
            if (levelGenerator != null)
            {
                Occupancy.Configure(levelGenerator.gridSize.x, levelGenerator.gridSize.y, levelGenerator.cellSize, levelGenerator.originOffset);
                levelGenerator.occupancy = Occupancy;
            }

            Occupancy.Rebuild(activeArrows, allBooks, allPointers, allBackpacks);
            RefreshLocks();
            if (levelGenerator != null) levelGenerator.FitCameraToBoard();

            ArrowsLeft = activeArrows.Count;
            TotalArrows = ArrowsLeft;
            MovesUsed = 0;
            BonusMoves = 0;
            undoSnapshot = null;
            blockedBumpLocks = 0;
            State = GameState.Playing;

            if (bonuses != null) bonuses.ResetForLevel();
            if (inputHandler != null) inputHandler.inputEnabled = false;
            if (orbitDirector != null) orbitDirector.CancelAll();
            if (characterView != null) characterView.ResetPose();
            if (candyBoxView != null) candyBoxView.ResetCount();
            if (screenLayout != null) screenLayout.Apply();

            if (uiManager != null)
            {
                uiManager.HidePanels();
                uiManager.ClearMessage();
                uiManager.RefreshAfterLevelStart();
            }

            if (PlayProgress.ConsumeSchoolTutorialReplay())
                PresentSchoolTutorialReplay();
            else if (ShouldHoldSchoolTutorial())
                PresentSchoolTutorial();
            StartLevelIntro();
            ShowCampaignControls();
            if (IsSchoolMode)
                SchoolIdleHint.Ensure()?.ResetIdle();
            else
                SchoolIdleHint.HideActive();

            GameplaySession.BeginLevel();
            LogExitStates();
            GameLog.Info($"[GameManager] Стрелок: {ArrowsLeft}/{TotalArrows}, ходов: {(HasMoveLimit ? movesLimit.ToString() : "∞")}.");
        }

        /// <summary>
        /// Описание управления для кампании: у школы есть туториал, а кампания объяснений
        /// не давала. Показываем на первом уровне, пока игрок не прошёл ни одного.
        /// </summary>
        private void ShowCampaignControls()
        {
            if (uiManager == null || IsSchoolMode) return;
            if (CurrentLevelIndex != 1 || PlayProgress.HasCampaignProgress) return;

            uiManager.ShowMessage(GameTexts.CampaignControls, ControlsMessageSeconds);
        }

        private bool ShouldHoldSchoolTutorial()
        {
            if (autoSpawnTestLayout) return false;
            return IsSchoolMode && CurrentLevelIndex == 1;
        }

        private void PresentSchoolTutorial()
        {
            SchoolTutorialPanel panel = ResolveSchoolTutorial();
            if (panel == null)
            {
                EnableInputAfterIntro();
                return;
            }

            if (inputHandler != null) inputHandler.inputEnabled = false;
            panel.OpenFirstRun(() => EnableInputAfterIntro());
        }

        private void PresentSchoolTutorialReplay()
        {
            SchoolTutorialPanel panel = ResolveSchoolTutorial();
            if (panel == null)
            {
                EnableInputAfterIntro();
                return;
            }

            if (inputHandler != null) inputHandler.inputEnabled = false;
            panel.OpenReplay(() => EnableInputAfterIntro());
        }

        private SchoolTutorialPanel ResolveSchoolTutorial()
        {
            Transform canvasTf = null;
            SceneCanvas scene = SceneCanvas.InScene;
            if (scene != null) canvasTf = scene.transform;
            else if (uiManager != null) canvasTf = uiManager.transform;

            SchoolTutorialPanel panel = SchoolTutorialPanel.FindOn(canvasTf);
            if (panel == null)
                panel = FindObjectOfType<SchoolTutorialPanel>(true);
            if (panel == null)
                AuthoredUi.Missing(SchoolTutorialPanel.OverlayName);
            return panel;
        }

        private void StartLevelIntro()
        {
            StopLevelIntro();
            introRoutine = StartCoroutine(LevelIntroRoutine());
        }

        private void StopLevelIntro()
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            IsIntroPlaying = false;
        }

        private IEnumerator LevelIntroRoutine()
        {
            IsIntroPlaying = true;
            if (inputHandler != null) inputHandler.inputEnabled = false;

            int started = 0;
            for (int i = 0; i < activeArrows.Count; i++)
            {
                ArrowController arrow = activeArrows[i];
                if (arrow == null || !arrow.gameObject.activeSelf) continue;
                arrow.PlayIntro(started * IntroStagger);
                started++;
            }

            if (started > 0)
            {
                while (AnyArrowPlayingIntro())
                    yield return null;
            }

            introRoutine = null;
            IsIntroPlaying = false;
            EnableInputAfterIntro();
        }

        private bool AnyArrowPlayingIntro()
        {
            for (int i = 0; i < activeArrows.Count; i++)
            {
                ArrowController arrow = activeArrows[i];
                if (arrow != null && arrow.IsPlayingIntro) return true;
            }

            return false;
        }

        private void EnableInputAfterIntro()
        {
            if (State != GameState.Playing) return;
            if (IsIntroPlaying) return;
            if (IsBlockedBumpPlaying) return;
            if (SchoolTutorialPanel.IsOpen) return;
            if (inputHandler != null) inputHandler.inputEnabled = true;
        }

        public void NotifyBlockedBumpStarted()
        {
            blockedBumpLocks++;
            if (inputHandler != null) inputHandler.inputEnabled = false;
        }

        public void NotifyBlockedBumpFinished()
        {
            if (blockedBumpLocks > 0) blockedBumpLocks--;
            if (blockedBumpLocks > 0) return;
            EnableInputAfterIntro();
        }

        /// <summary>
        /// Порядок Success: TryRemove (OnArrowRemoved → Dismiss книг ключа + UnregisterBlocker)
        /// → RefreshLocks → SpendMove → RotatePointersAfterSuccess.
        /// Указка не крутится, пока книга ключа ещё в occupancy.
        /// </summary>
        public void OnArrowTapped(ArrowController arrow)
        {
            if (SchoolTutorialPanel.IsOpen) return;
            if (IsIntroPlaying) return;
            if (State != GameState.Playing || arrow == null || arrow.IsRemoving || arrow.IsPlayingBlockedBump) return;
            if (IsBlockedBumpPlaying) return;

            MoveResult result = EvaluateTap(arrow, out ArrowColor blockingColor);

            LevelSnapshot snapshot = LevelSnapshot.Capture(allArrows, allBooks, allPointers, allBackpacks, MovesUsed);
            undoSnapshot = snapshot;

            switch (result)
            {
                case MoveResult.Success:
                    if (characterView != null) characterView.PlayHappy();
                    GameAudio.Play(GameAudio.Sfx.ArrowExit);
                    if (arrow.HasKeyedBook)
                        GameAudio.Play(GameAudio.Sfx.SchoolBookKey);
                    arrow.TryRemove();
                    RefreshLocks();
                    SpendMove();
                    RotatePointersAfterSuccess();
                    break;

                case MoveResult.WrongColor:
                    GameAudio.Play(GameAudio.Sfx.ArrowWrongColor);
                    arrow.PlayRejectFeedback();
                    if (characterView != null) characterView.PlaySad();
                    ShowMessage(GameTexts.WrongColor(blockingColor));
                    SpendMove();
                    break;

                case MoveResult.Blocked:
                    GameAudio.Play(GameAudio.Sfx.ArrowBlocked);
                    if (!arrow.PlayBlockedBump())
                        arrow.PlayRejectFeedback();
                    if (characterView != null) characterView.PlaySad();
                    ExitObstruction obstruction = Occupancy != null
                        ? Occupancy.GetExitObstruction(arrow)
                        : ExitObstruction.Arrow;
                    if (obstruction == ExitObstruction.Book)
                        ShowMessage(GameTexts.PathBlockedByBook);
                    else if (obstruction == ExitObstruction.Pointer)
                        ShowMessage(GameTexts.PathBlockedByPointer);
                    else if (obstruction == ExitObstruction.Backpack)
                        ShowMessage(GameTexts.PathBlockedByBackpack);
                    else
                        ShowMessage(GameTexts.PathBlocked);
                    SpendMove();
                    break;
            }

            if (uiManager != null) uiManager.Refresh();
        }

        /// <summary>Тап по указке: не ход, не поворот, короткий отказ.</summary>
        public void OnPointerTapped(PointerController pointer)
        {
            if (SchoolTutorialPanel.IsOpen) return;
            if (IsIntroPlaying) return;
            if (IsBlockedBumpPlaying) return;
            if (State != GameState.Playing || pointer == null) return;

            pointer.PlayRejectFeedback();
            if (characterView != null) characterView.PlaySad();
            GameAudio.Play(GameAudio.Sfx.ArrowBlocked);
            ShowMessage(GameTexts.PointerCannotBeRemoved);
        }

        /// <summary>Тап по книге: как указка — не ход, книгу не убрать.</summary>
        public void OnBookTapped(BookController book)
        {
            if (SchoolTutorialPanel.IsOpen) return;
            if (IsIntroPlaying) return;
            if (IsBlockedBumpPlaying) return;
            if (State != GameState.Playing || book == null) return;

            book.PlayRejectFeedback();
            if (characterView != null) characterView.PlaySad();
            GameAudio.Play(GameAudio.Sfx.SchoolBookBlocked);
            ShowMessage(GameTexts.BookCannotBeRemoved);
        }

        /// <summary>Свайп портфеля вдоль оси: один шаг. Не ход, указку не крутит.</summary>
        public void OnBackpackSwiped(BackpackController backpack, ArrowDirection slideDir)
        {
            if (SchoolTutorialPanel.IsOpen) return;
            if (IsIntroPlaying) return;
            if (IsBlockedBumpPlaying) return;
            if (State != GameState.Playing || backpack == null) return;

            LevelSnapshot snapshot = LevelSnapshot.Capture(allArrows, allBooks, allPointers, allBackpacks, MovesUsed);
            if (backpack.TrySlide(Occupancy, slideDir))
            {
                undoSnapshot = snapshot;
                GameAudio.Play(GameAudio.Sfx.SchoolBackpackSlide);
                if (uiManager != null) uiManager.Refresh();
                return;
            }

            backpack.PlayRejectFeedback();
            if (characterView != null) characterView.PlaySad();
            GameAudio.Play(GameAudio.Sfx.SchoolBackpackBlocked);
            ShowMessage(GameTexts.BackpackHasNoRoom);
        }

        /// <summary>Поворот указок только после Success. Занято / вне сетки — пропуск.</summary>
        private void RotatePointersAfterSuccess()
        {
            if (Occupancy == null) return;
            for (int i = 0; i < allPointers.Count; i++)
            {
                PointerController pointer = allPointers[i];
                if (pointer == null || !pointer.IsAlive) continue;
                pointer.TryRotateAfterSuccess(Occupancy);
            }
        }

        /// <summary>
        /// Порядок: (цвет, если включён и это не школа) → путь свободен.
        /// Книга, указка, портфель и чужая стрелка закрывают коридор, а не саму стрелку.
        /// </summary>
        public MoveResult EvaluateTap(ArrowController arrow, out ArrowColor blockingColor)
        {
            blockingColor = arrow != null ? arrow.color : ArrowColor.Grey;
            if (arrow == null) return MoveResult.Blocked;

            if (!IsSchoolMode && colorPriority != null && colorPriority.rulesEnabled
                && !colorPriority.IsRemovalAllowed(arrow.color, activeArrows, out blockingColor))
            {
                return MoveResult.WrongColor;
            }

            if (!arrow.CanExit()) return MoveResult.Blocked;

            return MoveResult.Success;
        }

        public bool TryUndo()
        {
            if (undoSnapshot == null) return false;
            if (IsBlockedBumpPlaying) return false;

            if (orbitDirector != null) orbitDirector.CancelAll();
            undoSnapshot.Apply();
            MovesUsed = undoSnapshot.MovesUsed;
            undoSnapshot = null;

            RebuildActiveArrows();
            Occupancy.Rebuild(activeArrows, allBooks, allPointers, allBackpacks);
            RefreshLocks();
            if (candyBoxView != null) candyBoxView.SyncFromArrows(allArrows);
            if (characterView != null) characterView.ResetPose();

            State = GameState.Playing;
            if (inputHandler != null) inputHandler.inputEnabled = true;

            if (uiManager != null)
            {
                uiManager.HidePanels();
                uiManager.Refresh();
            }

            return true;
        }

        public void AddMoves(int amount)
        {
            if (amount <= 0 || !HasMoveLimit) return;

            BonusMoves += amount;

            if (State == GameState.Lose && MovesLeft > 0)
            {
                State = GameState.Playing;
                if (inputHandler != null) inputHandler.inputEnabled = true;
                if (uiManager != null) uiManager.HidePanels();
            }

            if (uiManager != null) uiManager.Refresh();
        }

        public void NotifyFieldChanged()
        {
            Occupancy.Rebuild(activeArrows, allBooks, allPointers, allBackpacks);
            RefreshLocks();
            if (uiManager != null) uiManager.Refresh();
        }

        private void RebuildActiveArrows()
        {
            activeArrows.Clear();

            for (int i = 0; i < allArrows.Count; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow == null || !arrow.gameObject.activeSelf || !arrow.OccupiesGrid) continue;
                activeArrows.Add(arrow);
            }

            ArrowsLeft = activeArrows.Count;
        }

        private void SpendMove()
        {
            MovesUsed++;
            if (uiManager != null) uiManager.Refresh();

            if (HasMoveLimit && MovesLeft <= 0 && HasPlayableArrows()) Lose();
        }

        private bool HasPlayableArrows()
        {
            for (int i = 0; i < activeArrows.Count; i++)
            {
                ArrowController arrow = activeArrows[i];
                if (arrow != null && arrow.OccupiesGrid) return true;
            }

            return false;
        }

        public void NotifyArrowExited(ArrowController arrow)
        {
            if (arrow == null) return;

            activeArrows.Remove(arrow);
            ArrowsLeft = activeArrows.Count;
            RefreshLocks();

            if (uiManager != null) uiManager.Refresh();
            if (State != GameState.Playing) return;

            if (ArrowsLeft <= 0)
            {
                Win();
                return;
            }

            if (HasMoveLimit && MovesLeft <= 0 && HasPlayableArrows()) Lose();
        }

        public void RefreshLocks()
        {
            for (int i = 0; i < allArrows.Count; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow != null) arrow.RefreshLockState();
            }

            if (uiManager != null) uiManager.SetHint(BuildPriorityHint());
        }

        private string BuildPriorityHint()
        {
            if (IsSchoolMode) return string.Empty;
            if (colorPriority == null || !colorPriority.rulesEnabled) return string.Empty;
            if (!colorPriority.TryGetRequiredColor(activeArrows, out ArrowColor requiredColor)) return string.Empty;
            return GameTexts.PriorityHint(requiredColor);
        }

        public void Restart()
        {
            if (autoSpawnTestLayout)
            {
                SpawnTestLevel();
                return;
            }

            if (CurrentLevelIndex <= 0)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            LoadLevel(CurrentLevelIndex);
        }

        public void NextLevel()
        {
            GameSaves.Data.goldAwardedLevel = 0;

            if (autoSpawnTestLayout)
            {
                Restart();
                return;
            }

            if (IsSchoolMode)
            {
                if (IsSchoolFinalLevel)
                {
                    GoToMainMenu();
                    return;
                }

                int lastSchool = Mathf.Max(1, SchoolLevelCatalog.Count);
                int nextSchool = CurrentLevelIndex >= lastSchool ? lastSchool : CurrentLevelIndex + 1;
                LoadLevel(nextSchool);
                return;
            }

            int last = Mathf.Max(1, LevelCatalog.Count);
            int next = CurrentLevelIndex >= last ? 1 : CurrentLevelIndex + 1;
            LoadLevel(next);
        }

        public void GoToMainMenu()
        {
            GameplaySession.EndLevel();
            SceneManager.LoadScene(GameConstants.MainMenuSceneName);
        }

        public void Win()
        {
            if (State != GameState.Playing) return;

            State = GameState.Win;
            SchoolIdleHint.HideActive();
            GameplaySession.EndLevel();
            if (inputHandler != null) inputHandler.inputEnabled = false;
            if (characterView != null) characterView.PlayVictory();
            if (candyBoxView != null) candyBoxView.PlayWinBounce();
            if (IsSchoolMode)
            {
                PlayProgress.UnlockAfterWin(CurrentLevelIndex);
                BattlePass.NotifySchoolWon(CurrentLevelIndex);
            }
            AwardWinGold();
            GameSaves.Flush();
            GameAudio.Play(GameAudio.Sfx.Win);
            if (uiManager != null) uiManager.ShowWin();
        }

        /// <summary>Ключ выданной награды лежит в сейвах: иначе золото за уровень фармится перезагрузкой страницы.</summary>
        private void AwardWinGold()
        {
            int awardKey = IsSchoolMode ? CurrentLevelIndex + 1000 : CurrentLevelIndex;
            if (GameSaves.Data.goldAwardedLevel == awardKey) return;

            GameSaves.Data.goldAwardedLevel = awardKey;
            Wallet.Add(GameConstants.GoldPerWin);
        }

        public void Lose()
        {
            if (State != GameState.Playing) return;

            State = GameState.Lose;
            SchoolIdleHint.HideActive();
            GameplaySession.EndLevel();
            GameSaves.Flush();
            if (inputHandler != null) inputHandler.inputEnabled = false;
            GameAudio.Play(GameAudio.Sfx.Lose);
            if (characterView != null) characterView.PlaySad();
            if (uiManager != null) uiManager.ShowLose();
        }

        public void ShowMessage(string message)
        {
            if (uiManager != null) uiManager.ShowMessage(message);
            else GameLog.Info($"[GameManager] {message}");
        }

        private void HandleArrowRemoved(ArrowController arrow)
        {
            if (arrow == null) return;
            DismissBooksUnlockedBy(arrow);
            GameLog.Info($"[GameManager] Убрана: {arrow.name} ({arrow.color}), ходов осталось: {(HasMoveLimit ? MovesLeft.ToString() : "∞")}");
        }

        /// <summary>Ключ уехал — его книги исчезают, occupancy уже снимается в Dismiss.</summary>
        private void DismissBooksUnlockedBy(ArrowController key)
        {
            for (int i = 0; i < allBooks.Count; i++)
            {
                BookController book = allBooks[i];
                if (book == null || !book.IsAlive) continue;
                if (book.KeyArrow != key) continue;
                book.Dismiss();
            }
        }

        private int BuildActiveLevel()
        {
            if (IsSchoolMode)
            {
                LevelData data = SchoolLevelCatalog.Load(CurrentLevelIndex);
                if (data == null)
                {
                    GameLog.Error($"[GameManager] Нет файла Resources/{SchoolLevelCatalog.ResourcePath(CurrentLevelIndex)}.json.");
                    return 0;
                }

                return levelGenerator.BuildFromData(data);
            }

            return levelGenerator.Build(CurrentLevelIndex);
        }

        private void BindGenerator()
        {
            if (levelGenerator == null) levelGenerator = GetComponent<LevelGenerator>();
            if (levelGenerator == null) return;

            if (levelGenerator.levelRoot == null) levelGenerator.levelRoot = levelRoot;
            if (levelGenerator.arrowPrefab == null) levelGenerator.arrowPrefab = arrowPrefab;
            if (Occupancy == null) Occupancy = new OccupancyGrid();
            levelGenerator.occupancy = Occupancy;
        }

        private void ResolveArrowPrefab()
        {
            if (arrowPrefab != null) return;
            if (levelGenerator != null) arrowPrefab = levelGenerator.arrowPrefab;
            if (arrowPrefab != null) return;

#if UNITY_EDITOR
            arrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<ArrowController>("Assets/Prefabs/Arrow.prefab");
#endif
        }

        private void LogExitStates()
        {
            for (int i = 0; i < activeArrows.Count; i++)
            {
                ArrowController arrow = activeArrows[i];
                if (arrow == null) continue;
                GameLog.Info($"[GameManager] {arrow.name} CanExit={arrow.CanExit()} dir={arrow.direction} color={arrow.color}");
            }
        }

    }
}
