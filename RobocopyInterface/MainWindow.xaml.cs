using RobocopyHelper.ViewModels;
using System.Windows;

namespace RobocopyHelper;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Auto-scroll the log to the bottom whenever new text is appended.
        LogTextBox.TextChanged += (_, _) => LogScrollViewer.ScrollToBottom();
    }
}
