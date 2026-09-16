using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Громкость для редактора: Assets/Resources/AudioLevels.
    /// 1 = обычно, 2 = в два раза громче.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLevels", menuName = "Unpuzzle/Audio Levels")]
    public class AudioLevels : ScriptableObject
    {
        public const string ResourceName = "AudioLevels";
        public const float Max = 2f;

        [Range(0f, Max)]
        [Tooltip("Эффекты и клики. 2 = в два раза громче.")]
        public float sfxVolume = 2f;

        [Range(0f, Max)]
        [Tooltip("Фоновая музыка. 2 = в два раза громче.")]
        public float musicVolume = 2f;

        private static AudioLevels cached;

        public static AudioLevels Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<AudioLevels>(ResourceName);
                return cached;
            }
        }

        public static float Sfx => Current != null ? Mathf.Clamp(Current.sfxVolume, 0f, Max) : Max;
        public static float Music => Current != null ? Mathf.Clamp(Current.musicVolume, 0f, Max) : Max;
    }
}
