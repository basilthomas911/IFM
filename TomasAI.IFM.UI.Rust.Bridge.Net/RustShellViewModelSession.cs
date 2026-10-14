using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>
/// Attaches the unchanged WinForms shell view model to the Rust client lifecycle.
/// Its actual event handlers produce all shell status-console messages; rendering stays native.
/// Initialize/Dispose attach and detach observers. Feed/trading/import operations remain explicit user actions.
/// </summary>
sealed class RustShellViewModelSession : IAsyncDisposable
{
    readonly IFMAppViewModel viewModel;
    readonly CancellationTokenSource stopping = new();
    readonly Task initialization;

    public RustShellViewModelSession(RustClientServiceScope scope, Action<Exception> reportError, Action<IReadOnlyList<StatusConsoleLogReadModel>> publishLogs)
    {
        viewModel = new IFMAppViewModel(scope,
            typeof(IFMAppViewModel).Assembly.GetName().Version ?? new Version(1,0,0,0), scope.AppEnvironment,
            new NativeShellAdapter(), scope.Container.GetInstance<IEconomicCalendarService>());
        viewModel.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(IFMAppViewModel.StatusLogs)) publishLogs(viewModel.StatusLogs);
        };
        initialization = Task.Run(async () =>
        {
            try { await viewModel.InitializeAsync(stopping.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (Exception exception)
            {
                reportError(exception);
                try { await scope.GetStatusConsoleWriter().WriteConsoleAsync(LogSourceType.IFMApp,
                    $"Rust shell presentation initialization is unavailable ({exception.GetType().Name}: {exception.Message}); other UI features remain active.").ConfigureAwait(false); }
                catch (Exception logError) { reportError(logError); }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        stopping.Cancel();
        try
        {
            await initialization.ConfigureAwait(false);
            await viewModel.DisposeAsync().ConfigureAwait(false);
        }
        finally { stopping.Dispose(); }
    }

    sealed class NativeShellAdapter : IIFMAppLiveViewAdapter
    {
        // No WinForms windows exist in this client. Closing the Rust host owns its native dialogs.
        public ValueTask CloseTradeBlottersAsync() => ValueTask.CompletedTask;
    }
}
