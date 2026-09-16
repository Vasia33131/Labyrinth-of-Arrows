using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Сторона полосы под sticky: справа — только на широком ландшафтном экране, иначе снизу.
    /// Геометрия важнее флага SDK, иначе в редакторе и в узком окне на ПК полоса справа
    /// съедает часть портретного экрана.
    /// </summary>
    public static class YandexDevice
    {
        public static bool IsDesktop
        {
            get
            {
                // Не зависит от готовности SDK: телефон в ландшафте проходит и по ширине,
                // и по пропорции, поэтому до прихода envir его считали бы десктопом
                // и полоса баннера уезжала бы вправо с последующим скачком раскладки.
                if (Application.isMobilePlatform) return false;
                if (SystemInfo.deviceType == DeviceType.Handheld) return false;

                int width = Screen.width;
                int height = Mathf.Max(1, Screen.height);

                if (width < GameConstants.DesktopFallbackMinWidth) return false;
                if ((float)width / height < GameConstants.DesktopMinAspect) return false;

#if EnvirData_yg
                if (YG2.isSDKEnabled && (YG2.envir.isMobile || YG2.envir.isTablet))
                    return false;
#endif

                return true;
            }
        }

        public static bool IsMobile => !IsDesktop;
    }
}
