using System;
using System.Globalization;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Глобальный запас подсказок. Хранение в облачных сохранениях, как у Wallet.
    /// Ежедневный claim выдаёт пак всех трёх бустеров (подсказка / отмена / +1 ход).
    /// День считается по времени сервера: локальные часы игрока можно перевести назад.
    /// </summary>
    public static class Hints
    {
        public static event Action<int> OnChanged;

        public static bool ClaimedDailyThisSession { get; private set; }

        private static bool claiming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            claiming = false;
            ClaimedDailyThisSession = false;
        }

        public static int GetHintsCount()
        {
            return Mathf.Max(0, GameSaves.Data.hintsCount);
        }

        /// <summary>
        /// Один claim в календарный день: пак подсказок, отмен и +1 ход.
        /// true, если сегодня только что выдали пак.
        /// </summary>
        public static bool TryClaimDailyHints()
        {
            string today = TodayStamp();
            if (GameSaves.Data.hintsLastClaimDate == today || claiming) return false;

            claiming = true;
            try
            {
                GameSaves.Data.hintsLastClaimDate = today;
                GameSaves.Data.hintsCount = GetHintsCount() + GameConstants.DailyHintsAmount;
                UndoCharges.Add(GameConstants.DailyUndoAmount);
                ExtraMoves.Add(GameConstants.DailyExtraMoveAmount);
                ClaimedDailyThisSession = true;
            }
            finally
            {
                claiming = false;
            }

            GameSaves.RequestSave();
            GameSaves.Flush();
            OnChanged?.Invoke(GetHintsCount());
            return true;
        }

        public static bool SpendHint()
        {
            int count = GetHintsCount();
            if (count <= 0) return false;

            SetCount(count - 1);
            return true;
        }

        public static void AddHints(int amount)
        {
            if (amount <= 0) return;

            SetCount(GetHintsCount() + amount);
        }

        internal static void RaiseChanged()
        {
            OnChanged?.Invoke(GetHintsCount());
        }

        private static string TodayStamp()
        {
            return GameSaves.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static void SetCount(int value)
        {
            value = Mathf.Max(0, value);
            if (GameSaves.Data.hintsCount == value) return;

            GameSaves.Data.hintsCount = value;
            GameSaves.SaveNow();
            OnChanged?.Invoke(value);
        }
    }
}
