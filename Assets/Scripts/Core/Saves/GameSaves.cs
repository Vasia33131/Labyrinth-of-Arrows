using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Единая точка доступа к прогрессу. Данные живут в YG2.saves: плагин синхронно поднимает
    /// их из облака ещё до загрузки первой сцены и дублирует в localStorage браузера.
    /// PlayerPrefs остались только источником разового импорта игроков со старой версии.
    /// </summary>
    public static class GameSaves
    {
        /// <summary>
        /// Пауза перед записью мелких тиков. П. 1.9: осознанные действия зовут SaveNow()
        /// сразу; пачка 0.35 с только для санитарной чистки списков и импорта.
        /// </summary>
        private const float SaveDelaySeconds = 0.35f;

        private static bool ready;
        private static bool dirty;
        private static bool writesSuspended;
        private static float flushAt;
        private static Scheduler scheduler;
        private static SavesYG known;

        /// <summary>
        /// Плагин подменяет объект сейвов при каждой перезагрузке облака, в том числе после
        /// входа в аккаунт, поэтому ссылку на него нельзя кешировать нигде в игре.
        /// </summary>
        public static SavesYG Data
        {
            get
            {
                EnsureReady();
                return YG2.saves;
            }
        }

        /// <summary>Время сервера. Локальные часы игрока переводом даты назад открывают ежедневные награды.</summary>
        public static DateTime UtcNow
        {
            get
            {
                long serverTime = YG2.ServerTime();
                return serverTime > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(serverTime).UtcDateTime
                    : DateTime.UtcNow;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ready = false;
            dirty = false;
            writesSuspended = false;
            flushAt = 0f;
            scheduler = null;
            known = null;
        }

        /// <summary>Пометить прогресс изменённым. Запись уйдёт пачкой через SaveDelaySeconds.</summary>
        public static void RequestSave()
        {
            EnsureReady();

            if (!dirty)
            {
                dirty = true;
                flushAt = Time.unscaledTime + SaveDelaySeconds;
            }

            EnsureScheduler();
        }

        /// <summary>П. 1.9: записать сразу после осознанного действия игрока.</summary>
        public static void SaveNow()
        {
            RequestSave();
            Flush();
        }

        /// <summary>
        /// Диалог выбора аккаунта: не писать текущий прогресс поверх выбранного сейва.
        /// Грязный флаг сохраняется, запись возобновится после перечитывания облака.
        /// </summary>
        public static void SetWritesSuspended(bool suspended)
        {
            writesSuspended = suspended;
        }

        /// <summary>Записать немедленно: уход со вкладки, показ рекламы, конец уровня, настройка.</summary>
        public static void Flush()
        {
            if (!dirty) return;
            if (writesSuspended) return;

            // До конца инициализации плагин писать не даст. Флаг остаётся, попробуем следующим кадром.
            if (!YG2.isSDKEnabled) return;

            dirty = false;
            YG2.saves.unpuzzleRevision++;
            YG2.SaveProgress();
        }

        private static void EnsureReady()
        {
            if (ready) return;
            ready = true;

            known = YG2.saves;
            YG2.onGetSDKData -= HandleSavesReloaded;
            YG2.onGetSDKData += HandleSavesReloaded;

            if (!YG2.saves.unpuzzleImported)
                ImportFromPlayerPrefs();
        }

        /// <summary>
        /// Плагин перезагрузил сейвы. Если объект подменился на пустой, значит игрок вошёл
        /// в аккаунт, который игру ещё не открывал: переносим туда прогресс сессии, иначе
        /// авторизация посреди игры обнулила бы всё только что пройденное.
        /// </summary>
        private static void HandleSavesReloaded()
        {
            SavesYG current = YG2.saves;
            bool skipMerge = AccountSelection.ConsumeSkipNextSaveMerge();

            if (!ReferenceEquals(current, known))
            {
                SavesYG previous = known;
                known = current;

                if (skipMerge)
                {
                    dirty = false;
                }
                else if (current.unpuzzleRevision == 0 && previous != null && previous.unpuzzleRevision > 0)
                {
                    CopyProgress(previous, current);
                    dirty = true;
                    flushAt = Time.unscaledTime;
                    EnsureScheduler();
                }
                else if (!current.unpuzzleImported)
                {
                    ImportFromPlayerPrefs();
                }
            }

            AccountSelection.HandleSavesReloaded();
            NotifyChanged();
        }

        /// <summary>Разбудить подписчиков после подмены сейвов: счётчики и оформление читают данные заново.</summary>
        private static void NotifyChanged()
        {
            Wallet.RaiseChanged();
            Hints.RaiseChanged();
            UndoCharges.RaiseChanged();
            ExtraMoves.RaiseChanged();
            CharacterSkins.RaiseChanged();
            Backgrounds.RaiseChanged();
            SettingsPanel.ApplyAudio();
        }

        private static void EnsureScheduler()
        {
            if (scheduler != null || !Application.isPlaying) return;

            var go = new GameObject("UnpuzzleSaves");
            scheduler = go.AddComponent<Scheduler>();
        }

        private static void HandleFocusChanged(bool focused)
        {
            if (!focused) Flush();
        }

        private static void HandlePauseChanged(bool paused)
        {
            if (paused) Flush();
        }

        private static void ImportFromPlayerPrefs()
        {
            SavesYG data = YG2.saves;
            data.unpuzzleImported = true;

            if (!HasLegacyData()) return;

            data.playMode = PlayerPrefs.GetString(GameConstants.PlayModePrefsKey, GameConstants.PlayModeNormal);
            data.campaignLevel = PlayerPrefs.GetInt(LevelCatalog.PrefsKey, 0);
            data.schoolLevel = PlayerPrefs.GetInt(GameConstants.SchoolLevelPrefsKey, 1);
            data.schoolUnlocked = PlayerPrefs.GetInt(GameConstants.SchoolUnlockedPrefsKey, 1);
            data.schoolTutorialSeen = PlayerPrefs.GetInt(GameConstants.SchoolTutorialSeenPrefsKey, 0) == 1;

            data.gold = PlayerPrefs.GetInt(GameConstants.GoldPrefsKey, 0);
            data.hintsCount = PlayerPrefs.GetInt(GameConstants.HintsCountPrefsKey, 0);
            data.undoCount = PlayerPrefs.GetInt(GameConstants.UndoCountPrefsKey, 0);
            data.extraMoveCount = PlayerPrefs.GetInt(GameConstants.ExtraMoveCountPrefsKey, 0);
            data.hintsLastClaimDate = PlayerPrefs.GetString(GameConstants.HintsLastClaimDatePrefsKey, string.Empty);

            data.battlePassCompletedSchools = PlayerPrefs.GetInt(GameConstants.BattlePassCompletedSchoolPrefsKey, 0);
            data.battlePassPremium = PlayerPrefs.GetInt(GameConstants.BattlePassPremiumPrefsKey, 0) == 1;
            data.battlePassClaimedFree = PlayerPrefs.GetInt(GameConstants.BattlePassClaimedFreePrefsKey, 0);
            data.battlePassClaimedPremium = PlayerPrefs.GetInt(GameConstants.BattlePassClaimedPremiumPrefsKey, 0);
            data.battlePassSchoolSynced = PlayerPrefs.GetInt(GameConstants.BattlePassSchoolSyncedPrefsKey, 0) == 1;

            data.unlockedSkins = SplitIds(PlayerPrefs.GetString(GameConstants.UnlockedSkinsPrefsKey, string.Empty));
            data.selectedSkin = PlayerPrefs.GetString(GameConstants.SelectedSkinPrefsKey, GameConstants.DefaultSkinId);
            data.unlockedBackgrounds = SplitIds(PlayerPrefs.GetString(GameConstants.UnlockedBackgroundsPrefsKey, string.Empty));
            data.selectedBackground = PlayerPrefs.GetString(
                GameConstants.SelectedBackgroundPrefsKey, GameConstants.DefaultBackgroundId);

            // Старый ключ звука пережил переименование, поэтому служит запасным значением.
            int legacySound = PlayerPrefs.GetInt(GameConstants.LegacySoundPrefsKey, 1);
            data.soundOn = PlayerPrefs.GetInt(GameConstants.SoundPrefsKey, legacySound) == 1;
            data.musicOn = PlayerPrefs.GetInt(GameConstants.MusicPrefsKey, 1) == 1;

            data.hasPlayed = PlayerPrefs.GetInt(GameConstants.HasPlayedPrefsKey, 0) == 1;

            SaveNow();
        }

        private static bool HasLegacyData()
        {
            return PlayerPrefs.HasKey(LevelCatalog.PrefsKey)
                || PlayerPrefs.HasKey(GameConstants.GoldPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.SchoolUnlockedPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.SchoolLevelPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.HintsCountPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.UnlockedSkinsPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.UnlockedBackgroundsPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.BattlePassCompletedSchoolPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.SoundPrefsKey)
                || PlayerPrefs.HasKey(GameConstants.LegacySoundPrefsKey);
        }

        private static List<string> SplitIds(string raw)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(raw)) return ids;

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (id.Length > 0 && !ids.Contains(id)) ids.Add(id);
            }

            return ids;
        }

        private static void CopyProgress(SavesYG from, SavesYG to)
        {
            to.unpuzzleImported = from.unpuzzleImported;
            to.playMode = from.playMode;
            to.campaignLevel = from.campaignLevel;
            to.schoolLevel = from.schoolLevel;
            to.schoolUnlocked = from.schoolUnlocked;
            to.schoolTutorialSeen = from.schoolTutorialSeen;

            to.gold = from.gold;
            to.hintsCount = from.hintsCount;
            to.undoCount = from.undoCount;
            to.extraMoveCount = from.extraMoveCount;
            to.hintsLastClaimDate = from.hintsLastClaimDate;

            to.battlePassCompletedSchools = from.battlePassCompletedSchools;
            to.battlePassPremium = from.battlePassPremium;
            to.battlePassClaimedFree = from.battlePassClaimedFree;
            to.battlePassClaimedPremium = from.battlePassClaimedPremium;
            to.battlePassSchoolSynced = from.battlePassSchoolSynced;

            to.unlockedSkins = new List<string>(from.unlockedSkins ?? new List<string>());
            to.selectedSkin = from.selectedSkin;
            to.unlockedBackgrounds = new List<string>(from.unlockedBackgrounds ?? new List<string>());
            to.selectedBackground = from.selectedBackground;

            to.soundOn = from.soundOn;
            to.musicOn = from.musicOn;
            to.hasPlayed = from.hasPlayed;
            to.goldAwardedLevel = from.goldAwardedLevel;
        }

        /// <summary>Держит окно дебаунса и дописывает прогресс, когда игрок уходит со вкладки.</summary>
        private sealed class Scheduler : MonoBehaviour
        {
            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
                YG2.onFocusWindowGame -= HandleFocusChanged;
                YG2.onFocusWindowGame += HandleFocusChanged;
                YG2.onPauseGame -= HandlePauseChanged;
                YG2.onPauseGame += HandlePauseChanged;
                YG2.onOpenAnyAdv -= Flush;
                YG2.onOpenAnyAdv += Flush;
            }

            private void OnDestroy()
            {
                YG2.onFocusWindowGame -= HandleFocusChanged;
                YG2.onPauseGame -= HandlePauseChanged;
                YG2.onOpenAnyAdv -= Flush;
            }

            private void Update()
            {
                if (dirty && Time.unscaledTime >= flushAt) Flush();
            }

            private void OnApplicationFocus(bool focus)
            {
                if (!focus) Flush();
            }

            private void OnApplicationPause(bool pause)
            {
                if (pause) Flush();
            }

            private void OnApplicationQuit()
            {
                Flush();
            }
        }
    }
}
