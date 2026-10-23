using Microsoft.Extensions.Logging;
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
[MessagePack.MessagePackObject]
public sealed record ShellFeedControl([property: MessagePack.Key("ready")] bool Ready, [property: MessagePack.Key("canToggle")] bool CanToggle, [property: MessagePack.Key("busy")] bool Busy, [property: MessagePack.Key("active")] bool Active);

sealed class RustShellViewModelSession : IAsyncDisposable
{
    readonly IFMAppViewModel viewModel;
    readonly CancellationTokenSource stopping = new();
    readonly Task initialization;
    readonly RustClientServiceScope scope;
    bool initialized;
    public ShellFeedControl FeedControl => new(initialized, initialized && viewModel.CanToggleMarketDataFeed, viewModel.IsMarketDataFeedOperationInProgress, viewModel.IsMarketDataFeedActive);
    public async Task ToggleMarketDataFeedAsync(CancellationToken token)
    {
        await initialization.WaitAsync(token);
        if (!FeedControl.CanToggle) throw new InvalidOperationException("Market feed action is unavailable; wait for shell readiness or an open market session.");
        var expected = !viewModel.IsMarketDataFeedActive;
        scope.DiagnosticLogger.LogInformation("ShellFeed CommandStarted ExpectedRunning={ExpectedRunning} ValueDate={ValueDate}",expected,viewModel.ValueDate);
        await viewModel.ToggleMarketDataFeedAsync(token);
        if (viewModel.IsMarketDataFeedActive != expected)
            throw new InvalidOperationException(viewModel.LastError?.Message ?? "Backend did not confirm the requested feed state.");
        scope.DiagnosticLogger.LogInformation("ShellFeed CommandConfirmed IsRunning={IsRunning} ValueDate={ValueDate}",expected,viewModel.ValueDate);
    }

    public RustShellViewModelSession(RustClientServiceScope scope, Action<Exception> reportError, Action<IReadOnlyList<StatusConsoleLogReadModel>> publishLogs, Action? publishFeed = null)
    {
        this.scope = scope;
        viewModel = new IFMAppViewModel(scope,
            typeof(IFMAppViewModel).Assembly.GetName().Version ?? new Version(1,0,0,0), scope.AppEnvironment,
            new NativeShellAdapter(), scope.Container.GetInstance<IEconomicCalendarService>());
        viewModel.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(IFMAppViewModel.StatusLogs)) publishLogs(viewModel.StatusLogs);
            if (args.PropertyName is nameof(IFMAppViewModel.IsMarketDataFeedActive) or nameof(IFMAppViewModel.CanToggleMarketDataFeed) or nameof(IFMAppViewModel.IsMarketDataFeedOperationInProgress)) publishFeed?.Invoke();
            if (args.PropertyName == nameof(IFMAppViewModel.LastError) && viewModel.LastError is {} failure) reportError(new InvalidOperationException($"{failure.Caption}: {failure.Message}"));
        };
        initialization = Task.Run(async () =>
        {
            try { await viewModel.InitializeAsync(stopping.Token).ConfigureAwait(false); initialized=true; publishFeed?.Invoke(); }
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
