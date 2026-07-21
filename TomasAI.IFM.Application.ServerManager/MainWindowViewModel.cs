using System.ComponentModel;
using System.Windows;
using TomasAI.IFM.Shared.StatusConsole;
using System.Collections.ObjectModel;

namespace TomasAI.IFM.Application.ServerManager;

public class MainWindowViewModel : IMainWindowViewModel, INotifyPropertyChanged
{
    Visibility _visibility;
    WindowState _windowState;

    public MainWindowViewModel()
    {
        ConsoleStatus = new ObservableCollection<StatusLog>();
    }

    public ObservableCollection<StatusLog> ConsoleStatus { get; }

    public Visibility ConsoleVisibility
    {
        get => _visibility;
        set
        {
            _visibility = value;
            NotifyPropertyChanged("ConsoleVisibility");
        }
    }
    public WindowState ConsoleWindowState
    {
        get => _windowState;
        set
        {
            _windowState = value;
            NotifyPropertyChanged("ConsoleWindowState");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddServerLog(ServerLogType serverLogType, string? logEntry)
        => System.Windows.Application.Current.Dispatcher.Invoke(() => ConsoleStatus.Insert(0, new StatusLog { LogEntry = $"{serverLogType} {logEntry}" }));

    void NotifyPropertyChanged(string info)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
    
}
