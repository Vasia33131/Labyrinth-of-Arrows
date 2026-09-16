using System;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Глобальный запас бустера «+1 ход». Облачные сохранения, как у Hints. Не выдаётся с уровня.
    /// </summary>
    public static class ExtraMoves
    {
        public static event Action<int> OnChanged;

        public static int GetCount()
        {
            return Mathf.Max(0, GameSaves.Data.extraMoveCount);
        }

        public static bool Spend()
        {
            int count = GetCount();
            if (count <= 0) return false;

            SetCount(count - 1);
            return true;
        }

        public static void Add(int amount)
        {
            if (amount <= 0) return;

            SetCount(GetCount() + amount);
        }

        internal static void RaiseChanged()
        {
            OnChanged?.Invoke(GetCount());
        }

        private static void SetCount(int value)
        {
            value = Mathf.Max(0, value);
            if (GameSaves.Data.extraMoveCount == value) return;

            GameSaves.Data.extraMoveCount = value;
            GameSaves.SaveNow();
            OnChanged?.Invoke(value);
        }
    }
}
