using Microsoft.UI;
using Microsoft.UI.Xaml;
using RobocopyInterface.ViewModels;

namespace RobocopyInterface;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Title = "Robocopy Helper";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(880, 660));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        RootGrid.DataContext = viewModel;

        // Auto-scroll the log to the bottom whenever new text is appended.
        LogTextBox.TextChanged += (_, _) => LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }
}
