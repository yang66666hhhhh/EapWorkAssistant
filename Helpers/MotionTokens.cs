using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Helpers
{
    /// <summary>
    /// 动效令牌读取器：从 DesignTokens.xaml 读取时长 / 缓动 / 位移令牌。
    /// 供 code-behind 动画（BeginAnimation）使用；取不到令牌时回退默认值，
    /// 保证设计器预览与单元测试环境（无 Application 资源）不会崩。
    /// 注意：ResourceDictionary 只实现 IDictionary，没有泛型 TryGetValue，必须 Contains + 索引器。
    /// </summary>
    internal static class MotionTokens
    {
        /// <summary>读取 Duration 令牌（如 DurationEnter / DurationFast），取不到时回退 fallbackMilliseconds。</summary>
        public static Duration GetDuration(string resourceKey, double fallbackMilliseconds)
        {
            var resources = Application.Current?.Resources;
            if (resources != null
                && resources.Contains(resourceKey)
                && resources[resourceKey] is Duration d
                && d.HasTimeSpan)
            {
                return d.TimeSpan;
            }
            return TimeSpan.FromMilliseconds(fallbackMilliseconds);
        }

        /// <summary>读取缓动函数令牌（如 EaseStandard），取不到时回退 null（线性）。</summary>
        public static IEasingFunction? GetEasing(string resourceKey)
        {
            return Application.Current?.TryFindResource(resourceKey) as IEasingFunction;
        }

        /// <summary>读取 sys:Double 位移量令牌（如 MotionPageEnterY），取不到时回退 fallback。</summary>
        public static double GetDouble(string resourceKey, double fallback)
        {
            var resources = Application.Current?.Resources;
            if (resources != null
                && resources.Contains(resourceKey)
                && resources[resourceKey] is double d)
            {
                return d;
            }
            return fallback;
        }
    }
}
