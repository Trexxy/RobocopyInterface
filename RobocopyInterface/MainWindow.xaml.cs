using Microsoft.UI.Xaml;
using RobocopyInterface.ViewModels;

namespace RobocopyInterface;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Title = "Robocopy Helper";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 620));

        RootGrid.DataContext = viewModel;

        // Auto-scroll the log to the bottom whenever new text is appended.
        LogTextBox.TextChanged += (_, _) => LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }
}
