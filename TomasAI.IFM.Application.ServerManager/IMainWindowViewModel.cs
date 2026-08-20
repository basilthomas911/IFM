using System.Collections.ObjectModel;
using System.Windows;

namespace TomasAI.IFM.Application.ServerManager;

public interface IMainWindowViewModel
{
    ObservableCollection<StatusLog> ConsoleStatus { get; }

    Visibility ConsoleVisibility { get; set; }

    WindowState ConsoleWindowState { get; set; }

    void AddLog(ManagedProcessLogEntry entry);
}
