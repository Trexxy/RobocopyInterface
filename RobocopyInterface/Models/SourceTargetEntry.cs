using CommunityToolkit.Mvvm.ComponentModel;

namespace RobocopyInterface.Models;

public partial class SourceTargetEntry : ObservableObject
{
    public string Source { get; }

    [ObservableProperty]
    private string _target;

    public SourceTargetEntry(string source, string target = "")
    {
        Source = source;
        _target = target;
    }
}
