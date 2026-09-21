using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace RobocopyInterface.Converters;

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isEmpty = value is int count && count == 0;
        if (string.Equals(parameter as string, "Invert", StringComparison.Ordinal)) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
