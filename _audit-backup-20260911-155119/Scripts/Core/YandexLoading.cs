using System.Collections;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// LoadingAPI.ready() через Game Ready API. Зовём, когда экран уже собран и кнопки кликабельны,
    /// поэтому AutoGRA в настройках плагина выключен.
    /// </summary>
    public static class YandexLoading
    {
        private static bool requested;

        public static void NotifyReady()
        {
            if (requested) return;
            requested = true;

            var go = new GameObject("YandexLoadingReady");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                YG2.GameReadyAPI();
                Destroy(gameObject);
            }
        }
    }
}
