using Microsoft.UI.Xaml.Data;

namespace RobocopyInterface.Converters;

public sealed class PathTypeToGlyphConverter : IValueConverter
{
    private const string FolderGlyph = "";
    private const string FileGlyph = "";

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string path && File.Exists(path) ? FileGlyph : FolderGlyph;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
