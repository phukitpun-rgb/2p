using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace JmdExplorer.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>Resolves a resource-key string (e.g. "BadgeGoodBrush") to its Brush.</summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current.TryFindResource(key) is Brush brush)
            return brush;
        return Application.Current.TryFindResource("TextDimBrush") ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats a nullable byte size: number with thousands separators, or "Unknown".</summary>
public sealed class NullableSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is long l ? $"{l:N0} B" : "Unknown";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
