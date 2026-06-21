using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SystemOptimizer.Models;

namespace SystemOptimizer.Converters;

/// <summary>Colors a startup recommendation: green keep, amber delay, red disable.</summary>
public sealed class RecommendationToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StartupRecommendation r
            ? r switch
            {
                StartupRecommendation.Keep => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                StartupRecommendation.Delay => new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x00)),
                StartupRecommendation.Disable => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
                _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            }
            : Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Colors a volume usage bar: green &lt; 75%, amber &lt; 90%, red above.</summary>
public sealed class UsedPercentToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 0;
        return pct switch
        {
            >= 90 => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
            >= 75 => new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
