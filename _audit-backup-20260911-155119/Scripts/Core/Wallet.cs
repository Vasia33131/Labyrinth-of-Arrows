using System;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>Валюта «золото». Статичный доступ, хранение в облачных сохранениях.</summary>
    public static class Wallet
    {
        public static event Action<int> OnChanged;

        public static int Gold => Mathf.Max(0, GameSaves.Data.gold);

        public static void Add(int amount)
        {
            if (amount <= 0) return;

            SetGold(Gold + amount);
            GameAudio.Play(GameAudio.Sfx.Gold);
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            if (Gold < amount) return false;

            SetGold(Gold - amount);
            return true;
        }

        internal static void RaiseChanged()
        {
            OnChanged?.Invoke(Gold);
        }

        private static void SetGold(int value)
        {
            value = Mathf.Max(0, value);
            if (GameSaves.Data.gold == value) return;

            GameSaves.Data.gold = value;
            GameSaves.RequestSave();
            OnChanged?.Invoke(value);
        }
    }
}
