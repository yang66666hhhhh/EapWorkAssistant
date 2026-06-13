using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 头像文件路径 → BitmapImage 转换器。
/// 路径有效且文件存在时返回 BitmapImage，否则返回 null。
/// </summary>
public class AvatarPathConverter : IValueConverter
{
    public static readonly AvatarPathConverter Instance = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 加载后释放文件锁
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.DecodePixelWidth = 128; // 限制解码尺寸，节省内存
                bitmap.EndInit();
                bitmap.Freeze(); // 允许跨线程访问
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
