using System.ComponentModel;
using System.Windows;

namespace TomasAI.IFM.Application.ServerManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IMainWindowViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(IMainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        _viewModel = mainWindowViewModel;
        DataContext = mainWindowViewModel;
        Closing += OnClosing;
    }

    public void PrepareForShutdown() => _allowClose = true;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _viewModel.ConsoleVisibility = Visibility.Hidden;
        _viewModel.ConsoleWindowState = WindowState.Minimized;
        Hide();
    }
}
