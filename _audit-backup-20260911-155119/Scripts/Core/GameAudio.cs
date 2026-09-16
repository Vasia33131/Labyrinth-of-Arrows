using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// SFX и музыка из Resources/Audio. Один объект на сессию.
    /// Громкость — Assets/Resources/AudioLevels (инспектор Unity).
    /// Клик — на каждую Button и Toggle.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public enum Sfx
        {
            UiClick,
            UiOpen,
            UiClose,
            ArrowExit,
            ArrowBlocked,
            ArrowWrongColor,
            CandyLand,
            Win,
            Lose,
            Hint,
            Undo,
            ExtraMove,
            Gold,
            BuyOk,
            BuyFail,
            Equip,
            SchoolBookKey,
            SchoolBookVanish,
            SchoolBookBlocked,
            SchoolPointerRotate,
            SchoolBackpackSlide,
            SchoolBackpackBlocked,
            SchoolTutorialPage
        }

        private const string SfxFolder = "Audio/Sfx/";
        private const string MusicFolder = "Audio/Music/";
        private const int MusicBoostCount = 1;
        private const float BindInterval = 0.35f;

        private static GameAudio instance;
        private static bool sceneHooked;

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private readonly HashSet<int> boundSelectables = new HashSet<int>();
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private AudioSource[] musicBoosts;
        private AudioClip currentMusic;
        private bool clipsLoaded;
        private float nextMusicRetry;
        private float nextBindTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
            if (sceneHooked) return;
            sceneHooked = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure();
            if (instance == null) return;
            instance.boundSelectables.Clear();
            BindUiClicks(null);
            instance.RefreshMusic();
        }

        public static GameAudio Ensure()
        {
            if (instance != null) return instance;

            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
            instance.Build();
            return instance;
        }

        public static void ApplySettings()
        {
            Ensure();
            if (instance == null) return;
            instance.ApplySettingsInternal();
        }

        public static void Play(Sfx id)
        {
            Ensure();
            if (instance == null) return;
            instance.PlayInternal(id);
        }

        public static void PlayUiClick() => Play(Sfx.UiClick);
        public static void PlayUiOpen() => PlayUiClick();
        public static void PlayUiClose() => PlayUiClick();

        public static void BindUiClicks(Transform root)
        {
            Ensure();
            if (instance != null) instance.BindUiClicksInternal(root);
        }

        private void Build()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(sfxSource, false, 1f);
            sfxSource.priority = 32;

            musicSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(musicSource, true, 1f);
            musicSource.priority = 80;

            musicBoosts = new AudioSource[MusicBoostCount];
            for (int i = 0; i < MusicBoostCount; i++)
            {
                AudioSource boost = gameObject.AddComponent<AudioSource>();
                ConfigureSource(boost, true, 0f);
                boost.priority = 80;
                musicBoosts[i] = boost;
            }

            LoadClips();
            Backgrounds.OnChanged -= HandleBackgroundChanged;
            Backgrounds.OnChanged += HandleBackgroundChanged;
            ApplySettingsInternal();
            BindUiClicksInternal(null);
            RefreshMusic();
        }

        private void OnDestroy()
        {
            Backgrounds.OnChanged -= HandleBackgroundChanged;
            if (instance == this) instance = null;
        }

        private void HandleBackgroundChanged()
        {
            RefreshMusic();
        }

        private void LoadClips()
        {
            if (clipsLoaded) return;
            clipsLoaded = true;

            Bind(Sfx.UiClick, "sfx_ui_click");
            Bind(Sfx.UiOpen, "sfx_ui_click");
            Bind(Sfx.UiClose, "sfx_ui_click");
            Bind(Sfx.ArrowExit, "sfx_arrow_exit");
            Bind(Sfx.ArrowBlocked, "sfx_arrow_blocked");
            Bind(Sfx.ArrowWrongColor, "sfx_arrow_wrong_color");
            Bind(Sfx.CandyLand, "sfx_candy_land");
            Bind(Sfx.Win, "sfx_win");
            Bind(Sfx.Lose, "sfx_lose");
            Bind(Sfx.Hint, "sfx_hint");
            Bind(Sfx.Undo, "sfx_undo");
            Bind(Sfx.ExtraMove, "sfx_extra_move");
            Bind(Sfx.Gold, "sfx_gold");
            Bind(Sfx.BuyOk, "sfx_buy_ok");
            Bind(Sfx.BuyFail, "sfx_buy_fail");
            Bind(Sfx.Equip, "sfx_equip");
            Bind(Sfx.SchoolBookKey, "sfx_school_book_key");
            Bind(Sfx.SchoolBookVanish, "sfx_school_book_vanish");
            Bind(Sfx.SchoolBookBlocked, "sfx_school_book_blocked");
            Bind(Sfx.SchoolPointerRotate, "sfx_school_pointer_rotate");
            Bind(Sfx.SchoolBackpackSlide, "sfx_school_backpack_slide");
            Bind(Sfx.SchoolBackpackBlocked, "sfx_school_backpack_blocked");
            Bind(Sfx.SchoolTutorialPage, "sfx_school_tutorial_page");
        }

        private void             Bind(Sfx id, string fileName)
        {
            AudioClip clip = Resources.Load<AudioClip>(SfxFolder + fileName);
            if (clip == null) clip = Resources.Load<AudioClip>(fileName);
            if (clip == null)
            {
                GameLog.Warn($"[GameAudio] Нет клипа Resources/{SfxFolder}{fileName}");
                return;
            }

            EnsureClipLoaded(clip);
            clips[id] = clip;
        }

        private void Update()
        {
            if (musicSource != null && currentMusic != null && ShouldPlayMusic() && !musicSource.isPlaying)
                TryResumeMusic(0.45f);
            else if (musicSource != null && ShouldPlayMusic())
                ApplyMusicGain();

            if (Time.unscaledTime >= nextBindTime)
            {
                nextBindTime = Time.unscaledTime + BindInterval;
                BindUiClicksInternal(null);
            }
        }

        private static void ConfigureSource(AudioSource source, bool loop, float volume)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.volume = volume;
        }

        private static void EnsureClipLoaded(AudioClip clip)
        {
            if (clip == null) return;
            if (clip.loadState == AudioDataLoadState.Loaded) return;
            clip.LoadAudioData();
        }

        private void PlayInternal(Sfx id)
        {
            if (!SettingsPanel.SoundOn) return;
            if (!clips.TryGetValue(id, out AudioClip clip) || clip == null) return;
            if (sfxSource == null) return;
            EnsureClipLoaded(clip);
            sfxSource.spatialBlend = 0f;
            sfxSource.PlayOneShot(clip, AudioLevels.Sfx);
            TryResumeMusic(0f);
        }

        private void ApplySettingsInternal()
        {
            if (sfxSource != null)
            {
                sfxSource.mute = !SettingsPanel.SoundOn;
                sfxSource.volume = 1f;
            }
            if (musicSource == null) return;

            bool musicOn = ShouldPlayMusic();
            musicSource.mute = !musicOn;
            MuteBoosts(!musicOn);
            ApplyMusicGain();
            if (musicOn) TryResumeMusic(0f);
        }

        private void RefreshMusic()
        {
            AudioClip next = LoadMusicForBackground(Backgrounds.SelectedId);
            if (next == currentMusic && musicSource != null && (musicSource.isPlaying || !ShouldPlayMusic()))
            {
                ApplySettingsInternal();
                return;
            }

            currentMusic = next;
            if (musicSource == null) return;
            EnsureClipLoaded(next);
            musicSource.clip = next;
            ApplySettingsInternal();
            if (ShouldPlayMusic() && next != null)
            {
                musicSource.spatialBlend = 0f;
                musicSource.Play();
                ApplyMusicGain();
            }
            else
            {
                musicSource.Stop();
                StopBoosts();
            }
        }

        private void TryResumeMusic(float retryDelay)
        {
            if (musicSource == null || currentMusic == null) return;
            if (!ShouldPlayMusic()) return;
            if (musicSource.isPlaying) return;
            if (retryDelay > 0f && Time.unscaledTime < nextMusicRetry) return;

            EnsureClipLoaded(currentMusic);
            if (musicSource.clip != currentMusic) musicSource.clip = currentMusic;
            musicSource.spatialBlend = 0f;
            musicSource.mute = false;
            musicSource.Play();
            ApplyMusicGain();
            nextMusicRetry = Time.unscaledTime + Mathf.Max(0.2f, retryDelay);
        }

        private void ApplyMusicGain()
        {
            float level = AudioLevels.Music;
            if (musicSource != null)
                musicSource.volume = ShouldPlayMusic() ? Mathf.Clamp01(level) : 0f;

            if (musicBoosts == null) return;

            float extra = ShouldPlayMusic() && musicSource != null && musicSource.isPlaying
                ? Mathf.Max(0f, level - 1f)
                : 0f;

            for (int i = 0; i < musicBoosts.Length; i++)
            {
                AudioSource boost = musicBoosts[i];
                if (boost == null) continue;

                float vol = Mathf.Clamp01(extra - i);
                boost.spatialBlend = 0f;
                boost.loop = true;
                boost.mute = musicSource != null && musicSource.mute;

                if (vol <= 0.001f || musicSource == null || !musicSource.isPlaying)
                {
                    boost.volume = 0f;
                    if (boost.isPlaying) boost.Stop();
                    continue;
                }

                if (boost.clip != musicSource.clip) boost.clip = musicSource.clip;
                boost.volume = vol;
                if (!boost.isPlaying)
                {
                    boost.timeSamples = musicSource.timeSamples;
                    boost.Play();
                }
            }
        }

        private void MuteBoosts(bool mute)
        {
            if (musicBoosts == null) return;
            for (int i = 0; i < musicBoosts.Length; i++)
            {
                if (musicBoosts[i] != null) musicBoosts[i].mute = mute;
            }
        }

        private void StopBoosts()
        {
            if (musicBoosts == null) return;
            for (int i = 0; i < musicBoosts.Length; i++)
            {
                if (musicBoosts[i] != null) musicBoosts[i].Stop();
            }
        }

        private static bool ShouldPlayMusic()
        {
            return SettingsPanel.SoundOn && SettingsPanel.MusicOn;
        }

        private static AudioClip LoadMusicForBackground(string backgroundId)
        {
            string fileName = MusicFileName(backgroundId);
            AudioClip clip = Resources.Load<AudioClip>(MusicFolder + fileName);
            if (clip == null)
                clip = Resources.Load<AudioClip>(MusicFolder + "music_classic_loop");
            return clip;
        }

        private static string MusicFileName(string backgroundId)
        {
            if (backgroundId == BackgroundCatalog.WitchGroveId) return "music_witch_grove_loop";
            if (backgroundId == BackgroundCatalog.NightElfId) return "music_night_elf_loop";
            if (backgroundId == BackgroundCatalog.NeonCityId) return "music_neon_city_loop";
            if (backgroundId == BackgroundCatalog.ClassroomId) return "music_classroom_loop";
            return "music_classic_loop";
        }

        private void BindUiClicksInternal(Transform root)
        {
            if (root != null)
            {
                BindButtons(root.GetComponentsInChildren<Button>(true));
                BindToggles(root.GetComponentsInChildren<Toggle>(true));
                return;
            }

            BindButtons(FindObjectsOfType<Button>(true));
            BindToggles(FindObjectsOfType<Toggle>(true));
        }

        private void BindButtons(Button[] buttons)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;
                if (!boundSelectables.Add(button.GetInstanceID())) continue;
                button.onClick.AddListener(PlayUiClick);
            }
        }

        private void BindToggles(Toggle[] toggles)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle == null) continue;
                if (!boundSelectables.Add(toggle.GetInstanceID())) continue;
                toggle.onValueChanged.AddListener(OnToggleClicked);
            }
        }

        private static void OnToggleClicked(bool _)
        {
            PlayUiClick();
        }
    }
}
