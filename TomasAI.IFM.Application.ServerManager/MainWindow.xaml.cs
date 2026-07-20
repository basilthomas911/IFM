using System;
using System.Windows;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.ServerManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(IMainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        this.DataContext = mainWindowViewModel;
    }

    public void AddServerLog(ServerLogType serverLogType, string? logEntry)
    {
        Console.WriteLine($"{logEntry ?? ""}");
    }

    public void Clear()
    {

    }
}
