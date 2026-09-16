using System.Diagnostics;

namespace Unpuzzle
{
    /// <summary>
    /// Логи только для редактора. Атрибут Conditional вырезает вызовы на этапе компиляции,
    /// поэтому в WebGL-билде консоль остаётся чистой.
    /// </summary>
    public static class GameLog
    {
        [Conditional("UNITY_EDITOR")]
        public static void Info(string message) => UnityEngine.Debug.Log(message);

        [Conditional("UNITY_EDITOR")]
        public static void Warn(string message) => UnityEngine.Debug.LogWarning(message);

        [Conditional("UNITY_EDITOR")]
        public static void Error(string message) => UnityEngine.Debug.LogError(message);
    }
}
