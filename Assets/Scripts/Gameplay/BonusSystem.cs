using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Три бустера — глобальный запас (Hints / UndoCharges / ExtraMoves).
    /// Не выдаются заново на каждый уровень.
    /// Висит на объекте GameManager, кнопки Canvas дёргают методы Use*.
    /// </summary>
    public class BonusSystem : MonoBehaviour
    {
        [Header("Ссылки")]
        public GameManager gameManager;

        [Header("Заряды на уровень")]
        [Tooltip("Не используется: подсказки хранятся глобально в Hints.")]
        [Min(0)] public int hintCharges = 0;

        [Tooltip("Не используется: отмены хранятся глобально в UndoCharges.")]
        [Min(0)] public int undoCharges = 0;

        [Tooltip("Не используется: +1 ход хранится глобально в ExtraMoves.")]
        [Min(0)] public int extraMoveCharges = 0;

        [Header("Настройки")]
        [Tooltip("Сколько секунд мигает подсвеченная стрелка.")]
        [Min(0.2f)] public float hintDuration = 1.6f;

        [Tooltip("Сколько ходов даёт один заряд бонуса «+1 ход».")]
        [Min(1)] public int extraMoveAmount = 1;

        public int HintsLeft => Hints.GetHintsCount();
        public int UndosLeft => UndoCharges.GetCount();
        public int ExtraMovesLeft => ExtraMoves.GetCount();

        /// <summary>Дёргается после любого изменения зарядов — на него подписан UIManager.</summary>
        public event Action OnBonusesChanged;

        public bool CanUseHint => HintsLeft > 0 && IsPlaying && Manager != null && !Manager.IsBlockedBumpPlaying;
        public bool CanOfferHintsAd => HintsLeft <= 0 && IsPlaying && Manager != null && !Manager.IsBlockedBumpPlaying;
        public bool CanUseUndo => UndosLeft > 0 && Manager != null && Manager.CanUndo && !Manager.IsBlockedBumpPlaying;
        public bool CanOfferUndoAd => UndosLeft <= 0 && Manager != null && !Manager.IsBlockedBumpPlaying;
        public bool CanUseExtraMove => ExtraMovesLeft > 0 && Manager != null && Manager.HasMoveLimit
                                       && (Manager.State == GameState.Playing || Manager.State == GameState.Lose)
                                       && !Manager.IsBlockedBumpPlaying;
        public bool CanOfferExtraMoveAd => ExtraMovesLeft <= 0 && Manager != null && !Manager.IsBlockedBumpPlaying;

        private GameManager Manager
        {
            get
            {
                if (gameManager == null) gameManager = GameManager.Instance;
                return gameManager;
            }
        }

        private bool IsPlaying => Manager != null && Manager.State == GameState.Playing;

        private void Awake()
        {
            if (gameManager == null) gameManager = GetComponent<GameManager>();
            Hints.TryClaimDailyHints();
            ResetForLevel();
        }

        private void OnEnable()
        {
            Hints.OnChanged += HandleStockChanged;
            UndoCharges.OnChanged += HandleStockChanged;
            ExtraMoves.OnChanged += HandleStockChanged;
        }

        private void OnDisable()
        {
            Hints.OnChanged -= HandleStockChanged;
            UndoCharges.OnChanged -= HandleStockChanged;
            ExtraMoves.OnChanged -= HandleStockChanged;
        }

        /// <summary>
        /// Не восстанавливает заряды: запас глобальный. Только claim ежедневки и уведомление UI.
        /// </summary>
        public void ResetForLevel()
        {
            Hints.TryClaimDailyHints();
            OnBonusesChanged?.Invoke();
        }

        private void HandleStockChanged(int _)
        {
            OnBonusesChanged?.Invoke();
        }

        // ------------------------------------------------------------ подсказка

        public bool UseHint()
        {
            if (!CanUseHint) return false;

            ArrowController arrow = FindSafeArrow();
            if (arrow == null)
            {
                Manager.ShowMessage(GameTexts.NoSafeMoves);
                return false;
            }

            if (!Hints.SpendHint()) return false;

            GameAudio.Play(GameAudio.Sfx.Hint);
            arrow.PlayHintHighlight(hintDuration);
            Manager.ShowMessage(GameTexts.HintFound(arrow.color, arrow.direction));
            OnBonusesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Безопасная стрелка = тап по ней проходит по всем правилам хода.
        /// Из подходящих выбираем ключ к книге и ту, чей цвет сейчас требует приоритет.
        /// </summary>
        public ArrowController FindSafeArrow()
        {
            if (Manager == null) return null;

            IReadOnlyList<ArrowController> arrows = Manager.ActiveArrows;
            ArrowColor requiredColor = ArrowColor.Grey;
            bool hasRequiredColor = Manager.colorPriority != null
                                    && Manager.colorPriority.TryGetRequiredColor(arrows, out requiredColor);

            ArrowController best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow == null || arrow.IsRemoving) continue;
                if (Manager.EvaluateTap(arrow, out ArrowColor _) != MoveResult.Success) continue;

                int score = 0;
                if (arrow.HasKeyedBook) score += 4;
                if (hasRequiredColor && arrow.color == requiredColor) score += 2;
                score += Mathf.RoundToInt(arrow.transform.position.y);

                if (score <= bestScore) continue;

                bestScore = score;
                best = arrow;
            }

            return best;
        }

        // --------------------------------------------------------- отмена хода

        public bool UseUndo()
        {
            if (UndosLeft <= 0) return false;

            if (Manager == null || !Manager.TryUndo())
            {
                if (Manager != null) Manager.ShowMessage(GameTexts.NothingToUndo);
                return false;
            }

            if (!UndoCharges.Spend()) return false;

            GameAudio.Play(GameAudio.Sfx.Undo);
            Manager.ShowMessage(GameTexts.MoveUndone);
            OnBonusesChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------- +1 ход

        public bool UseExtraMove()
        {
            if (!CanUseExtraMove) return false;
            if (!ExtraMoves.Spend()) return false;

            GameAudio.Play(GameAudio.Sfx.ExtraMove);
            Manager.AddMoves(extraMoveAmount);
            Manager.ShowMessage(GameTexts.ExtraMoveAdded(extraMoveAmount));
            OnBonusesChanged?.Invoke();
            return true;
        }

    }
}
