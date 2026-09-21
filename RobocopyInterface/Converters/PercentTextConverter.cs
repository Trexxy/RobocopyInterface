using Microsoft.UI.Xaml.Data;

namespace RobocopyInterface.Converters;

public sealed class PercentTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is double d ? $"{d:F1}%" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
