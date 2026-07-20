using System.Windows;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.ServerManager;

public interface IMainWindowViewModel
{
    Visibility ConsoleVisibility { get; set; }
    WindowState ConsoleWindowState { get; set; }

    void AddServerLog(ServerLogType serverLogType, string? logEntry);

}
