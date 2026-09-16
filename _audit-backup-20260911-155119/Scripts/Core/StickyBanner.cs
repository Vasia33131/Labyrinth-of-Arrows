using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Включает Яндекс sticky при старте. В Editor полосу резервирует BannerSafeArea.
    /// </summary>
    public static class StickyBanner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            YG2.onGetSDKData -= HandleSdkReady;
            YG2.onGetSDKData += HandleSdkReady;
            Activate();
        }

        private static void HandleSdkReady() => Activate();

        public static void Activate()
        {
#if StickyAdv_yg
            YG2.StickyAdActivity(true);
#endif
        }
    }
}
