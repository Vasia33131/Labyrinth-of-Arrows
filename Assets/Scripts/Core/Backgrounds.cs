using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>Открытые и выбранный фон. Облачные сохранения, как у Wallet и скинов.</summary>
    public static class Backgrounds
    {
        public static event Action OnChanged;

        /// <summary>Объект сейвов, для которого список уже почищен. Плагин подменяет его при перезагрузке облака.</summary>
        private static SavesYG sanitizedFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sanitizedFor = null;
        }

        public static string SelectedId
        {
            get
            {
                EnsureSanitized();
                return GameSaves.Data.selectedBackground;
            }
        }

        public static BackgroundData Selected
        {
            get
            {
                BackgroundData background = BackgroundCatalog.Current.Get(SelectedId);
                return background != null
                    ? background
                    : BackgroundCatalog.Current.Get(GameConstants.DefaultBackgroundId);
            }
        }

        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == GameConstants.DefaultBackgroundId) return true;

            EnsureSanitized();
            return GameSaves.Data.unlockedBackgrounds.Contains(id);
        }

        public static bool IsSelected(string id)
        {
            return !string.IsNullOrEmpty(id) && SelectedId == id;
        }

        public static bool Select(string id)
        {
            if (!IsUnlocked(id)) return false;
            if (BackgroundCatalog.Current.Get(id) == null) return false;
            if (GameSaves.Data.selectedBackground == id) return true;

            GameSaves.Data.selectedBackground = id;
            GameSaves.SaveNow();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Открыть фон без оплаты. Claim боевого пропуска только открывает, не надевает.</summary>
        public static void UnlockBackground(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (BackgroundCatalog.Current.Get(id) == null) return;
            if (IsUnlocked(id)) return;

            GameSaves.Data.unlockedBackgrounds.Add(id);
            GameSaves.SaveNow();
            OnChanged?.Invoke();
        }

        public static bool TryBuyAndEquip(string id, out string failMessage)
        {
            failMessage = null;

            BackgroundData background = BackgroundCatalog.Current.Get(id);
            if (background == null)
            {
                failMessage = GameTexts.BackgroundNotFound;
                return false;
            }

            if (IsUnlocked(id))
                return Select(id);

            if (background.unlockByBattlePass)
            {
                failMessage = GameTexts.BattlePassLocked;
                return false;
            }

            if (!background.IsFree)
            {
                if (Wallet.Gold < background.priceGold || !Wallet.TrySpend(background.priceGold))
                {
                    failMessage = GameTexts.NotEnoughGold;
                    return false;
                }
            }

            UnlockBackground(id);
            bool equipped = Select(id);
            GameSaves.Flush();
            return equipped;
        }

        internal static void RaiseChanged()
        {
            OnChanged?.Invoke();
        }

        /// <summary>Выбрасывает из сейва пустые, повторяющиеся и незнакомые каталогу id.</summary>
        private static void EnsureSanitized()
        {
            SavesYG data = GameSaves.Data;
            if (ReferenceEquals(sanitizedFor, data)) return;
            sanitizedFor = data;

            bool changed = false;
            if (data.unlockedBackgrounds == null)
            {
                data.unlockedBackgrounds = new List<string>();
                changed = true;
            }

            BackgroundCatalog catalog = BackgroundCatalog.Current;
            for (int i = data.unlockedBackgrounds.Count - 1; i >= 0; i--)
            {
                string id = data.unlockedBackgrounds[i];
                if (!string.IsNullOrEmpty(id) && catalog.Has(id) && data.unlockedBackgrounds.IndexOf(id) == i)
                    continue;

                data.unlockedBackgrounds.RemoveAt(i);
                changed = true;
            }

            if (!catalog.Has(data.selectedBackground)
                || (data.selectedBackground != GameConstants.DefaultBackgroundId
                    && !data.unlockedBackgrounds.Contains(data.selectedBackground)))
            {
                data.selectedBackground = GameConstants.DefaultBackgroundId;
                changed = true;
            }

            if (changed) GameSaves.RequestSave();
        }
    }
}
