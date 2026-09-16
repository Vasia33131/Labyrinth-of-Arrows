using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>Открытые и выбранный скин персонажа. Облачные сохранения, как у Wallet.</summary>
    public static class CharacterSkins
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
                return GameSaves.Data.selectedSkin;
            }
        }

        public static CharacterSkinData Selected
        {
            get
            {
                CharacterSkinData skin = CharacterSkinCatalog.Current.Get(SelectedId);
                return skin != null ? skin : CharacterSkinCatalog.Current.Get(GameConstants.DefaultSkinId);
            }
        }

        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == GameConstants.DefaultSkinId) return true;

            EnsureSanitized();
            return GameSaves.Data.unlockedSkins.Contains(id);
        }

        public static bool IsSelected(string id)
        {
            return !string.IsNullOrEmpty(id) && SelectedId == id;
        }

        public static bool Select(string id)
        {
            if (!IsUnlocked(id)) return false;
            if (CharacterSkinCatalog.Current.Get(id) == null) return false;
            if (GameSaves.Data.selectedSkin == id) return true;

            GameSaves.Data.selectedSkin = id;
            GameSaves.SaveNow();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Открыть скин без оплаты. Используется наградой боевого пропуска.</summary>
        public static void UnlockSkin(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (CharacterSkinCatalog.Current.Get(id) == null) return;
            if (IsUnlocked(id)) return;

            GameSaves.Data.unlockedSkins.Add(id);
            GameSaves.SaveNow();
            OnChanged?.Invoke();
        }

        public static bool TryBuyAndEquip(string id, out string failMessage)
        {
            failMessage = null;

            CharacterSkinData skin = CharacterSkinCatalog.Current.Get(id);
            if (skin == null)
            {
                failMessage = GameTexts.SkinNotFound;
                return false;
            }

            if (IsUnlocked(id))
                return Select(id);

            if (skin.unlockByBattlePass)
            {
                failMessage = GameTexts.BattlePassLocked;
                return false;
            }

            if (!skin.IsFree)
            {
                if (Wallet.Gold < skin.priceGold || !Wallet.TrySpend(skin.priceGold))
                {
                    failMessage = GameTexts.NotEnoughGold;
                    return false;
                }
            }

            UnlockSkin(id);
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
            if (data.unlockedSkins == null)
            {
                data.unlockedSkins = new List<string>();
                changed = true;
            }

            CharacterSkinCatalog catalog = CharacterSkinCatalog.Current;
            for (int i = data.unlockedSkins.Count - 1; i >= 0; i--)
            {
                string id = data.unlockedSkins[i];
                if (!string.IsNullOrEmpty(id) && catalog.Has(id) && data.unlockedSkins.IndexOf(id) == i)
                    continue;

                data.unlockedSkins.RemoveAt(i);
                changed = true;
            }

            if (!catalog.Has(data.selectedSkin)
                || (data.selectedSkin != GameConstants.DefaultSkinId && !data.unlockedSkins.Contains(data.selectedSkin)))
            {
                data.selectedSkin = GameConstants.DefaultSkinId;
                changed = true;
            }

            if (changed) GameSaves.RequestSave();
        }
    }
}
