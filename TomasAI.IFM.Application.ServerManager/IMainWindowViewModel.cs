using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;

public interface IMainWindowViewModel
{
    ObservableCollection<StatusLog> ConsoleStatus { get; }

    ObservableCollection<ManagedApplicationSummary> Applications { get; }

    ObservableCollection<TaskCatalogItemDto> TaskCatalog { get; }

    ObservableCollection<ScheduleSummaryDto> Schedules { get; }

    ObservableCollection<TaskRunSummaryDto> TaskRuns { get; }

    Visibility ConsoleVisibility { get; set; }

    WindowState ConsoleWindowState { get; set; }

    void AddLog(ManagedProcessLogEntry entry);

    Task RefreshSchedulerAsync(CancellationToken cancellationToken = default);
}
