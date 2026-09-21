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
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 820));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);

        RootGrid.DataContext = viewModel;

        // Auto-scroll the log to the bottom whenever new text is appended.
        LogTextBox.TextChanged += (_, _) => LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }
}
