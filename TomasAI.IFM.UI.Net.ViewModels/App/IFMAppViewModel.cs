using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>
/// Identifies the newest bounded futures-bar chart snapshot published for one symbol.
/// </summary>
public sealed record FuturesBarChartSnapshot(
    string Symbol,
    FuturesBarDataReadModel[] Bars);

/// <summary>
/// Provides diagnostics for the main shell's replaceable market-data streams.
/// </summary>
public sealed record IFMAppMarketDataStreamMetricsSnapshot(
    LatestValueChannelMetrics MarketOutlook,
    IReadOnlyDictionary<string, LatestValueChannelMetrics> FuturesBars);

/// <summary>
/// Provides diagnostics for the main shell's trade-signal, trade-placement, and status-console streams.
/// </summary>
public sealed record IFMAppRealtimeStreamMetricsSnapshot(
    LatestValueChannelMetrics FuturesTradeSignals,
    OrderedBatchChannelMetrics TradePlacements,
    OrderedBatchChannelMetrics StatusConsole);

/// <summary>
/// Describes main-shell dispatcher wait and render duration.
/// </summary>
public readonly record struct IFMAppUiDispatchMetricsSnapshot(
    long DispatchCount,
    TimeSpan LastDispatchDelay,
    TimeSpan MaximumDispatchDelay,
    TimeSpan LastRenderDuration,
    TimeSpan MaximumRenderDuration);

/// <summary>
/// Identifies the terminal state observed for one automatic startup reference-data import.
/// </summary>
public enum StartupReferenceDataImportOutcome
{
    Completed,
    Failed,
    NotObserved
}

/// <summary>
/// Describes the accepted command and terminal result of one automatic startup reference-data import.
/// </summary>
public sealed record StartupReferenceDataImportStatus(
    string Dataset,
    StartupReferenceDataImportOutcome Outcome,
    Guid CommandId,
    int ErrorCode,
    string Message);

public sealed class IFMAppViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    const int StatusLogCapacity = 500;
    const int TradePlacementDisplayCapacity = 500;
    const int OrderedEventChannelCapacity = 512;
    const int OrderedEventBatchSize = 32;
    const int FuturesBarChartCapacity = 2_048;
    static readonly TimeSpan DefaultStartupReferenceDataImportTimeout = TimeSpan.FromSeconds(30);
    readonly object _statusLogGate = new();
    readonly object _marketDataStreamGate = new();
    readonly object _realtimeStreamGate = new();
    readonly object _tradePlacementGate = new();
    readonly IAppRoot _appRoot;
    readonly IIFMAppLiveViewAdapter _liveViewAdapter;
    readonly TimeProvider _timeProvider;
    readonly TimeSpan _startupReferenceDataImportTimeout;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly Guid _siteId;
    readonly Version _appVersion;
    readonly string _appEnvironment;
    readonly List<StatusConsoleLogReadModel> _statusLogBuffer = [];
    readonly List<PlaceTradeUIViewModel> _tradePlacementBuffer = [];
    readonly Dictionary<string, FuturesBarDataReadModel[]> _futuresBarSnapshots = [];
    readonly Dictionary<string, LatestValueChannelMetrics> _futuresBarMetrics = [];
    LatestValueAsyncChannel<FuturesEodDataV2ReadModel>? _marketOutlookChannel;
    KeyedLatestValueAsyncChannel<string, FuturesBarDataInsertedCompleteEvent>? _futuresBarChannels;
    LatestValueAsyncChannel<FuturesTradeSignalV2ReadModel>? _futuresTradeSignalChannel;
    OrderedBatchAsyncChannel<IEvent>? _tradePlacementChannel;
    OrderedBatchAsyncChannel<StatusConsoleLogReadModel>? _statusConsoleChannel;
    DateOnly? _valueDate;
    IReadOnlyList<FuturesContractV2ReadModel> _baseContracts = [];
    IReadOnlyList<StatusConsoleLogReadModel> _statusLogs = [];
    StatusConsoleLogReadModel? _latestStatusLog;
    string _statusLine = string.Empty;
    bool _isMenuEnabled;
    bool _isCloseRequested;
    PresentationError? _lastError;
    StartupReferenceDataImportStatus? _yieldCurveStartupImport;
    StartupReferenceDataImportStatus? _economicCalendarStartupImport;
    IntradaySignalLifecycleResult? _intradaySignalStartup;
    StatusConsoleViewModel? _statusConsole;
    long _errorSequence;
    int _resetTicks;
    FuturesEodDataUIViewModel? _marketOutlook;
    FuturesTradeSignalUIViewModel? _futuresTradeSignal;
    PlaceTradeUIViewModel? _latestTradePlacement;
    IReadOnlyList<PlaceTradeUIViewModel> _tradePlacements = [];
    FuturesBarChartSnapshot? _latestFuturesBarSnapshot;
    IReadOnlyDictionary<string, FuturesBarDataReadModel[]> _futuresBarSnapshotState = new Dictionary<string, FuturesBarDataReadModel[]>();
    IFMAppMarketDataStreamMetricsSnapshot _marketDataStreamMetrics = new(default, new Dictionary<string, LatestValueChannelMetrics>());
    IFMAppRealtimeStreamMetricsSnapshot _realtimeStreamMetrics = new(default, default, default);
    IFMAppUiDispatchMetricsSnapshot _uiDispatchMetrics;
    long _uiDispatchCount;
    long _maximumUiDispatchDelayTicks;
    long _maximumUiRenderDurationTicks;

    /// <summary>
    /// create IFM app root view model
    /// </summary>
    /// <param name="appRoot">Application composition root.</param>
    /// <param name="appVersion">Desktop application version.</param>
    /// <param name="appEnvironment">Configured application environment.</param>
    /// <param name="liveViewAdapter">Transitional adapter for later live-dashboard and trading slices.</param>
    /// <param name="timeProvider">Optional time provider used by startup delays and the feed watchdog.</param>
    /// <param name="startupReferenceDataImportTimeout">Bounded wait for each startup import terminal event.</param>
    public IFMAppViewModel(
        IAppRoot appRoot,
        Version appVersion,
        string appEnvironment,
        IIFMAppLiveViewAdapter liveViewAdapter,
        TimeProvider? timeProvider = null,
        TimeSpan? startupReferenceDataImportTimeout = null)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
        _appEnvironment = string.IsNullOrWhiteSpace(appEnvironment)
            ? throw new ArgumentException("Application environment is required.", nameof(appEnvironment))
            : appEnvironment;
        _liveViewAdapter = liveViewAdapter ?? throw new ArgumentNullException(nameof(liveViewAdapter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startupReferenceDataImportTimeout = startupReferenceDataImportTimeout
            ?? DefaultStartupReferenceDataImportTimeout;
        if (_startupReferenceDataImportTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startupReferenceDataImportTimeout),
                "The startup reference-data import timeout must be positive and bounded.");
        }
        _siteId = Guid.NewGuid();
        _appRoot.GetModel<EventModel>().SetSiteId(_siteId);
        _lifecycle = new AsyncLifecycleCoordinator(InitializeCoreAsync, StopCoreAsync);
        StartupOperation = new AsyncOperation(StartupCorePresentationAsync, () => !_lifecycle.IsRunning);
        ShutdownOperation = new AsyncOperation(ShutdownCorePresentationAsync, () => _lifecycle.IsRunning);
    }

    /// <summary>Gets the current trading value date when live services are available.</summary>
    public DateOnly? ValueDate
    {
        get => _valueDate;
        private set => SetProperty(ref _valueDate, value);
    }

    /// <summary>Gets the currently traded base futures contracts.</summary>
    public IReadOnlyList<FuturesContractV2ReadModel> BaseContracts
    {
        get => _baseContracts;
        private set => SetProperty(ref _baseContracts, value);
    }

    /// <summary>Gets the bounded status-log snapshot, newest first.</summary>
    public IReadOnlyList<StatusConsoleLogReadModel> StatusLogs
    {
        get => _statusLogs;
        private set => SetProperty(ref _statusLogs, value);
    }

    /// <summary>Gets the most recently received status-log entry.</summary>
    public StatusConsoleLogReadModel? LatestStatusLog
    {
        get => _latestStatusLog;
        private set => SetProperty(ref _latestStatusLog, value);
    }

    /// <summary>Gets the current one-line application status.</summary>
    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    /// <summary>Gets whether primary navigation is enabled.</summary>
    public bool IsMenuEnabled
    {
        get => _isMenuEnabled;
        private set => SetProperty(ref _isMenuEnabled, value);
    }

    /// <summary>Gets whether a backend application event requested desktop shutdown.</summary>
    public bool IsCloseRequested
    {
        get => _isCloseRequested;
        private set => SetProperty(ref _isCloseRequested, value);
    }

    /// <summary>Gets the latest shell error notification.</summary>
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets the latest automatic startup yield-curve import result.</summary>
    public StartupReferenceDataImportStatus? YieldCurveStartupImport
    {
        get => _yieldCurveStartupImport;
        private set => SetProperty(ref _yieldCurveStartupImport, value);
    }

    /// <summary>Gets the latest automatic startup economic-calendar import result.</summary>
    public StartupReferenceDataImportStatus? EconomicCalendarStartupImport
    {
        get => _economicCalendarStartupImport;
        private set => SetProperty(ref _economicCalendarStartupImport, value);
    }

    /// <summary>Gets the latest automatic intraday signal startup result.</summary>
    public IntradaySignalLifecycleResult? IntradaySignalStartup
    {
        get => _intradaySignalStartup;
        private set => SetProperty(ref _intradaySignalStartup, value);
    }

    /// <summary>Gets the configured status-console ViewModel after live startup succeeds.</summary>
    public StatusConsoleViewModel? StatusConsole
    {
        get => _statusConsole;
        private set => SetProperty(ref _statusConsole, value);
    }

    /// <summary>Gets the newest Market Outlook display snapshot.</summary>
    public FuturesEodDataUIViewModel? MarketOutlook
    {
        get => _marketOutlook;
        private set => SetProperty(ref _marketOutlook, value);
    }

    /// <summary>Gets the newest replaceable futures trade-signal display snapshot.</summary>
    public FuturesTradeSignalUIViewModel? FuturesTradeSignal
    {
        get => _futuresTradeSignal;
        private set => SetProperty(ref _futuresTradeSignal, value);
    }

    /// <summary>Gets the newest losslessly processed trade-placement event.</summary>
    public PlaceTradeUIViewModel? LatestTradePlacement
    {
        get => _latestTradePlacement;
        private set => SetProperty(ref _latestTradePlacement, value);
    }

    /// <summary>Gets the bounded, newest-first display history of processed trade-placement events.</summary>
    public IReadOnlyList<PlaceTradeUIViewModel> TradePlacements
    {
        get => _tradePlacements;
        private set => SetProperty(ref _tradePlacements, value);
    }

    /// <summary>Gets the newest bounded futures-bar snapshot across symbols.</summary>
    public FuturesBarChartSnapshot? LatestFuturesBarSnapshot
    {
        get => _latestFuturesBarSnapshot;
        private set => SetProperty(ref _latestFuturesBarSnapshot, value);
    }

    /// <summary>Gets the newest bounded futures-bar chart snapshot for every observed symbol.</summary>
    public IReadOnlyDictionary<string, FuturesBarDataReadModel[]> FuturesBarSnapshots
    {
        get => _futuresBarSnapshotState;
        private set => SetProperty(ref _futuresBarSnapshotState, value);
    }

    /// <summary>Gets latest-value event-rate, coalescing, latency, failure, and lifecycle metrics.</summary>
    public IFMAppMarketDataStreamMetricsSnapshot MarketDataStreamMetrics
    {
        get => _marketDataStreamMetrics;
        private set => SetProperty(ref _marketDataStreamMetrics, value);
    }

    /// <summary>Gets rate, coalescing, backpressure, lag, failure, and lifecycle stream metrics.</summary>
    public IFMAppRealtimeStreamMetricsSnapshot RealtimeStreamMetrics
    {
        get => _realtimeStreamMetrics;
        private set => SetProperty(ref _realtimeStreamMetrics, value);
    }

    /// <summary>Gets main-shell dispatcher wait and render-duration metrics.</summary>
    public IFMAppUiDispatchMetricsSnapshot UiDispatchMetrics
    {
        get => _uiDispatchMetrics;
        private set => SetProperty(ref _uiDispatchMetrics, value);
    }

    /// <summary>
    /// Records one completed main-shell UI dispatch and render operation.
    /// </summary>
    public void RecordUiDispatch(TimeSpan dispatchDelay, TimeSpan renderDuration)
    {
        if (dispatchDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dispatchDelay));
        if (renderDuration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(renderDuration));

        UpdateMaximum(ref _maximumUiDispatchDelayTicks, dispatchDelay.Ticks);
        UpdateMaximum(ref _maximumUiRenderDurationTicks, renderDuration.Ticks);
        UiDispatchMetrics = new IFMAppUiDispatchMetricsSnapshot(
            Interlocked.Increment(ref _uiDispatchCount),
            dispatchDelay,
            TimeSpan.FromTicks(Interlocked.Read(ref _maximumUiDispatchDelayTicks)),
            renderDuration,
            TimeSpan.FromTicks(Interlocked.Read(ref _maximumUiRenderDurationTicks)));
    }

    /// <summary>Gets the single-flight application-startup operation.</summary>
    public IAsyncOperation StartupOperation { get; }

    /// <summary>Gets the single-flight graceful-shutdown operation.</summary>
    public IAsyncOperation ShutdownOperation { get; }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => _lifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.DisposeAsync();
        await DisposeOperationAsync(StartupOperation);
        await DisposeOperationAsync(ShutdownOperation);
    }

    async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await StartStatusConsoleListener();
        await StartApplicationEventsListener();
        await StartApplicationCoreAsync(cancellationToken);
    }

    async Task StartupCorePresentationAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        StartupOperation.NotifyCanExecuteChanged();
        ShutdownOperation.NotifyCanExecuteChanged();
    }

    async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (StatusConsole is not null)
            await StatusConsole.DisposeAsync();
        await StopFuturesEodDataEventConsumer();
        await StopFuturesBarDataEventConsumer();
        await StopFuturesTradeSignalEventConsumer();
        await StopTradePlacementEventConsumer();
        await StopFuturesIntradaySignalServices();
        await DisableMarketDataFeedResetListener();
        await DisableTradeLiveFeed();
        await _appRoot.GetModel<ApplicationEventModel>().StopApplicationEventConsumerAsync();
        await StopStatusConsoleListener();
        IsMenuEnabled = false;
        StartupOperation.NotifyCanExecuteChanged();
        ShutdownOperation.NotifyCanExecuteChanged();
    }

    async Task ShutdownCorePresentationAsync(CancellationToken cancellationToken)
    {
        await WriteStatusConsoleAsync(
            $"IFMApp v{_appVersion} - {_appEnvironment}...shutting down");
        await _liveViewAdapter.CloseTradeBlottersAsync();
        await StopAsync(cancellationToken);
    }

    /// <summary>
    /// application startup
    /// </summary>
    Task StartApplicationCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) => PublishError(
                errorCode,
                $"Unable to connect to IFM servers {Environment.NewLine}{errorMsg}",
                "Market Data Error"));
            ICollection<FuturesContractV2ReadModel>? futuresContracts = null;
            await model.GetCurrentlyTradedFuturesContractsAsync(values => futuresContracts = values);
            BaseContracts = futuresContracts?.ToArray() ?? [];
            await GetLastFuturesEodData();
            await GetLastFuturesTradeSignal();
            await GetLastFuturesBarData();
            await ImportReferenceDataAtStartupAsync(cancellationToken);

            DateOnly? valueDate = null;
            await model.GetValueDateAsync(value => valueDate = value);
            if (!valueDate.HasValue)
            {
                PublishError(
                    0,
                    "Market Data Live Feed unavailable outside of valid Trading Hours",
                    "Market Data Feed Error");
                return;
            }

            ValueDate = valueDate;
            await StartFuturesEodDataEventConsumer(cancellationToken);
            await StartFuturesBarDataEventConsumer(cancellationToken);
            await StartFuturesTradeSignalEventConsumer(cancellationToken);
            await StartTradePlacementEventConsumer(cancellationToken);
            await EnableMarketDataFeedResetListener(cancellationToken);
            await EnableTradeLiveFeed(cancellationToken);
            _lifecycle.RunAsync(ResetLiveFeedAsync);
            await StartFuturesIntradaySignalServices(cancellationToken);
            await WriteStatusConsoleAsync(
                $"IFMApp v{_appVersion} - {_appEnvironment}...initialization complete");
            var statusContractId = BaseContracts
                .Where(contract => contract.Symbol == "ES")
                .Select(contract => contract.ContractId)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(statusContractId))
            {
                StatusConsole = new StatusConsoleViewModel(_appRoot, statusContractId, ValueDate.Value);
                await StatusConsole.InitializeAsync(cancellationToken);
                await LoadStatusConsoleSnapshotsAsync(StatusConsole, cancellationToken);
            }

            IsMenuEnabled = true;
            StartupOperation.NotifyCanExecuteChanged();
            ShutdownOperation.NotifyCanExecuteChanged();
        });


    /// <summary>
    /// start console listener
    /// </summary>
    Task StartStatusConsoleListener()
    {
        if (_statusConsoleChannel is not null)
            return Task.CompletedTask;

        var channel = new OrderedBatchAsyncChannel<StatusConsoleLogReadModel>(
            ProcessStatusConsoleBatchAsync,
            capacity: OrderedEventChannelCapacity,
            maximumBatchSize: OrderedEventBatchSize,
            timeProvider: _timeProvider,
            metricsChanged: PublishStatusConsoleMetrics);
        _statusConsoleChannel = channel;
        PublishStatusConsoleMetrics(channel.Metrics);
        return _appRoot.GetModel<StatusConsoleModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Status Console Log Error"));
            await model.StartStatusConsoleLogListenerAsync(async o => {
                if (o is not null && o.StatusConsoleLog is not null)
                    await channel.WriteAsync(o.StatusConsoleLog);
            }, _siteId);
        });

        ValueTask ProcessStatusConsoleBatchAsync(
            IReadOnlyList<StatusConsoleLogReadModel> logs,
            CancellationToken channelCancellationToken)
        {
            channelCancellationToken.ThrowIfCancellationRequested();
            AppendStatusLogs(logs);
            return ValueTask.CompletedTask;
        }
    }

    async Task StopStatusConsoleListener()
    {
        var channel = Interlocked.Exchange(ref _statusConsoleChannel, null);
        try
        {
            await _appRoot.GetModel<StatusConsoleModel>().StopStatusConsoleLogListener(_siteId);
        }
        finally
        {
            if (channel is not null)
                await channel.StopAsync();
        }
    }

    /// <summary>
    /// start application events listener
    /// </summary>
    Task StartApplicationEventsListener()
        => _appRoot.GetModel<ApplicationEventModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Application Events Listener Error"));
            await model.StartApplicationEventConsumerAsync(
                startupAction: _ =>
                {
                    StartupOpenTrades();
                    return ValueTask.CompletedTask;
                },
                shutdownAction: _ =>
                {
                    IsCloseRequested = true;
                    return ValueTask.CompletedTask;
                });
        });

    /// <summary>
    /// startup any open trades
    /// </summary>
    void StartupOpenTrades()
    {

    }

    Task GetLastFuturesEodData()
        => _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Loading Latest Futures Eod Data Error"));
            await WriteStatusConsoleAsync("Loading Latest Futures Eod Data...");
            foreach (var contract in _baseContracts ?? [])
                await model.GetLastFuturesEodDataAsync(contract.ContractId, contract.LastTradeDate, futuresEodData => {
                    if (futuresEodData is not null)
                        PublishMarketOutlook(futuresEodData);
                });
        });

    Task GetLastFuturesTradeSignal()
        => _appRoot.GetModel<MarketDataAnalyticsQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Loading Latest Futures Trade Signal Error"));
            await WriteStatusConsoleAsync("Loading Latest Futures Trade Signal...");
            await model.GetLastFuturesTradeSignalAsync(futuresTradeSignal =>
            {
                if (futuresTradeSignal is not null)
                    PublishFuturesTradeSignal(futuresTradeSignal);
            });
        });

        Task GetLastFuturesBarData()
            => _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async model =>
            {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Loading Latest Futures Bar Data Error"));
                await WriteStatusConsoleAsync("Loading Latest Futures Bar Data...");
                foreach (var contract in _baseContracts ?? [])
                    await model.GetLastFuturesBarDataAsync(contract.ContractId, contract.Symbol, DateOnly.FromDateTime(DateTime.UtcNow), futuresBarData =>
                    {
                        if (futuresBarData is not null)
                            PublishFuturesBarSnapshot(futuresBarData.Symbol, [futuresBarData]);
                    });
            });

    /// <summary>
    /// start futures eod data event consumer
    /// </summary>
    async Task StartFuturesEodDataEventConsumer(CancellationToken cancellationToken)
    {
        if (_marketOutlookChannel is not null)
            return;
        _marketOutlookChannel = new LatestValueAsyncChannel<FuturesEodDataV2ReadModel>(
            ProcessMarketOutlookAsync,
            minimumInterval: TimeSpan.FromMilliseconds(50),
            timeProvider: _timeProvider,
            metricsChanged: PublishMarketOutlookMetrics);
        PublishMarketOutlookMetrics(_marketOutlookChannel.Metrics);
        await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Futures Eod Data Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Futures Eod Data Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartFuturesEodDataEventConsumerAsync(
                _siteId, e =>
                {
                    Interlocked.Exchange(ref _resetTicks, 0);
                    _marketOutlookChannel?.TryWrite(e.FuturesEodData);
                });
        });

        async ValueTask ProcessMarketOutlookAsync(
            FuturesEodDataV2ReadModel futuresEodData,
            CancellationToken channelCancellationToken)
        {
            channelCancellationToken.ThrowIfCancellationRequested();
            PublishMarketOutlook(futuresEodData);
            await WriteStatusConsoleAsync(
                $"{futuresEodData.ContractId}={futuresEodData.ClosePrice:F2}@{futuresEodData.DailyPercentChange:P} {futuresEodData.MarketDirection}:{futuresEodData.MarketVolatility}:{futuresEodData.PriceDirection}:{futuresEodData.PriceVolatility}",
                LogSourceType.MarketDataFeedEvent);
        }
    }

    /// <summary>
    /// stop futures eod data consumer
    /// </summary>
    async Task StopFuturesEodDataEventConsumer()
    {
        var channel = Interlocked.Exchange(ref _marketOutlookChannel, null);
        try
        {
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Stopping Futures Eod Data Error"));
                await WriteStatusConsoleAsync("Stopping Futures Eod Data...");
                await model.StopFuturesEodDataEventConsumerAsync(_siteId);
            });
        }
        finally
        {
            if (channel is not null)
                await channel.StopAsync();
        }
    }

    /// <summary>
    /// start futures trade signal event consumer
    /// </summary>
    async Task StartFuturesTradeSignalEventConsumer(CancellationToken cancellationToken)
    {
        if (_futuresTradeSignalChannel is not null)
            return;

        var channel = new LatestValueAsyncChannel<FuturesTradeSignalV2ReadModel>(
            ProcessFuturesTradeSignalAsync,
            minimumInterval: TimeSpan.FromMilliseconds(50),
            timeProvider: _timeProvider,
            metricsChanged: PublishFuturesTradeSignalMetrics);
        _futuresTradeSignalChannel = channel;
        PublishFuturesTradeSignalMetrics(channel.Metrics);
        await _appRoot.GetModel<MarketDataAnalyticsQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Futures Trade Signal Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Futures Trade Signal event consumer...");
            await DelayStartupAsync(cancellationToken);
            var contractId = _baseContracts?.FirstOrDefault(e => e.Id.Symbol == "ES")?.ContractId;
            if (contractId is not null)
                await model.GetFuturesTradeSignalAsync(
                    contractId, _valueDate ?? DateOnly.MinValue, futuresTradeSignal =>
                    {
                        if (futuresTradeSignal is not null)
                            channel.TryWrite(futuresTradeSignal);
                    });
        });
        await _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Futures Trade Signal Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Futures Trade Signal Event Consumer...");
            await model.StartFuturesTradeSignalEventConsumerAsync(
                _siteId, e =>
                {
                    if (e is not null && e.FuturesTradeSignal is not null)
                        channel.TryWrite(e.FuturesTradeSignal);
                });
        });

        ValueTask ProcessFuturesTradeSignalAsync(
            FuturesTradeSignalV2ReadModel signal,
            CancellationToken channelCancellationToken)
        {
            channelCancellationToken.ThrowIfCancellationRequested();
            PublishFuturesTradeSignal(signal);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// stop futures trade signal consumer
    /// </summary>
    async Task StopFuturesTradeSignalEventConsumer()
    {
        var channel = Interlocked.Exchange(ref _futuresTradeSignalChannel, null);
        try
        {
            await _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Stopping Futures Trade Signal Error"));
                await WriteStatusConsoleAsync("Stopping Futures Trade Signal...");
                await model.StopFuturesTradeSignalEventConsumerAsync(_siteId);
            });
        }
        finally
        {
            if (channel is not null)
                await channel.StopAsync();
        }
    }

    /// <summary>
    /// start trade placement event consumer
    /// </summary>
    Task StartTradePlacementEventConsumer(CancellationToken cancellationToken)
    {
        if (_tradePlacementChannel is not null)
            return Task.CompletedTask;

        var channel = new OrderedBatchAsyncChannel<IEvent>(
            ProcessTradePlacementBatchAsync,
            capacity: OrderedEventChannelCapacity,
            maximumBatchSize: OrderedEventBatchSize,
            timeProvider: _timeProvider,
            metricsChanged: PublishTradePlacementMetrics);
        _tradePlacementChannel = channel;
        PublishTradePlacementMetrics(channel.Metrics);
        return _appRoot.GetModel<TradePlacementEventModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Trade Placement Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Trade Placement Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartTradePlacementListenerAsync(e => channel.WriteAsync(e));
            await _appRoot.GetModel<TradePlacementCommandModel>().ExecuteAsync(async tradePlacementModel => {
                var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
                if (esContract is not null && _valueDate.HasValue)
                {
                    await tradePlacementModel.StartTradePlacementAsync(esContract.ContractId, _valueDate.Value);
                    await WriteStatusConsoleAsync("Starting Trade Placement Signal Service...");
                }
            });

        });

        ValueTask ProcessTradePlacementBatchAsync(
            IReadOnlyList<IEvent> events,
            CancellationToken channelCancellationToken)
        {
            channelCancellationToken.ThrowIfCancellationRequested();
            PublishTradePlacementBatch(events);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// stop trade placement consumer
    /// </summary>
    async Task StopTradePlacementEventConsumer()
    {
        var channel = Interlocked.Exchange(ref _tradePlacementChannel, null);
        try
        {
            await _appRoot.GetModel<TradePlacementEventModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Stopping Trade Placement Event Consumer Error"));
                await WriteStatusConsoleAsync("Stopping Trade Placement Event Consumer...");
                await model.StopTradePlacementListenerAsync();
                await _appRoot.GetModel<TradePlacementCommandModel>().ExecuteAsync(async tradePlacementModel => {
                    var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
                    if (esContract is not null && _valueDate.HasValue)
                    {
                        await tradePlacementModel.StopTradePlacementAsync(esContract.ContractId, _valueDate.Value);
                        await WriteStatusConsoleAsync("Stopping Trade Placement Signal Service...");
                    }
                });
            });
        }
        finally
        {
            if (channel is not null)
                await channel.StopAsync();
        }
    }

    /// <summary>
    /// Starts the authoritative RSI, ATR, ADX, and MACD intraday actor profile.
    /// </summary>
    Task StartFuturesIntradaySignalServices(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model =>
        {
            await DelayStartupAsync(cancellationToken);
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                await WriteStatusConsoleAsync(
                    "Starting RSI-13, ATR-14, ADX-14, and MACD-9/12/26 for all configured intraday timeframes...");
                IntradaySignalStartup = await model.StartFuturesIntradaySignalsAsync(
                    esContract.ContractId,
                    _valueDate.Value,
                    cancellationToken);

                if (IntradaySignalStartup.AllSucceeded)
                {
                    await WriteStatusConsoleAsync(
                        $"Started all {IntradaySignalStartup.SuccessfulCount} intraday signal actors.");
                    return;
                }

                var failureMessage = string.Join(
                    Environment.NewLine,
                    IntradaySignalStartup.Failures.Select(failure =>
                        $"{failure.SignalType} {failure.TimeFrame}: {failure.ErrorMessage}"));
                PublishError(
                    IntradaySignalStartup.Failures.FirstOrDefault()?.ErrorCode ?? 0,
                    $"Started {IntradaySignalStartup.SuccessfulCount} of {IntradaySignalStartup.RequestedCount} intraday signal actors."
                        + Environment.NewLine
                        + failureMessage
                        + Environment.NewLine
                        + "No automatic retry was attempted.",
                    "Intraday Signal Startup");
            }
        });

    /// <summary>
    /// Stops every actor in the authoritative intraday signal profile.
    /// </summary>
    Task StopFuturesIntradaySignalServices()
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model =>
        {
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                await WriteStatusConsoleAsync("Stopping intraday signal actors...");
                var result = await model.StopFuturesIntradaySignalsAsync(
                    esContract.ContractId,
                    _valueDate.Value);
                if (!result.AllSucceeded)
                {
                    PublishError(
                        result.Failures.FirstOrDefault()?.ErrorCode ?? 0,
                        $"Stopped {result.SuccessfulCount} of {result.RequestedCount} intraday signal actors.",
                        "Intraday Signal Shutdown");
                }
            }
        });

    /// <summary>
    /// start futures bar data event consumer
    /// </summary>
    Task StartFuturesBarDataEventConsumer(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            if (_futuresBarChannels is not null)
                return;
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Futures Bar Data Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Futures Bar Data Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            _futuresBarChannels = new KeyedLatestValueAsyncChannel<string, FuturesBarDataInsertedCompleteEvent>(
                (_, e, channelCancellationToken) => ProcessFuturesBarRefreshAsync(e, channelCancellationToken),
                minimumInterval: TimeSpan.FromMilliseconds(100),
                timeProvider: _timeProvider,
                metricsChanged: PublishFuturesBarMetrics);
            await model.StartFuturesBarDataEventConsumerAsync(
                _siteId,
                QueueFuturesBarRefreshAsync);
        });

    /// <summary>
    /// stop futures bar data consumer
    /// </summary>
    async Task StopFuturesBarDataEventConsumer()
    {
        var channels = Interlocked.Exchange(ref _futuresBarChannels, null);

        try
        {
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Stopping Futures Bar Data Event Consumer Error"));
                await WriteStatusConsoleAsync("Stopping Futures Bar Data Event Consumer...");
                await model.StopFuturesBarDataEventConsumerAsync(_siteId);
            });
        }
        finally
        {
            if (channels is not null)
                await channels.StopAsync();
        }
    }

    ValueTask QueueFuturesBarRefreshAsync(FuturesBarDataInsertedCompleteEvent e)
    {
        var symbol = e.FuturesBarData.Symbol;
        if (string.IsNullOrWhiteSpace(symbol))
            return ValueTask.CompletedTask;

        _futuresBarChannels?.TryWrite(symbol, e);
        return ValueTask.CompletedTask;
    }

    async ValueTask ProcessFuturesBarRefreshAsync(
        FuturesBarDataInsertedCompleteEvent e,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async queryModel =>
        {
            queryModel.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Loading Futures Bar Data Error"));
            await queryModel.GetFuturesBarDataAsync(
                e.FuturesBarData.ContractId,
                e.FuturesBarData.Symbol,
                e.FuturesBarData.ValueDate,
                e.FuturesBarData.BarDate.AddHours(-6),
                e.FuturesBarData.BarDate.AddSeconds(1),
                futuresBarData => PublishFuturesBarSnapshot(e.FuturesBarData.Symbol, futuresBarData));
        });
        await WriteStatusConsoleAsync(
            $"FuturesBarData := {e.FuturesBarData.ContractId} @ {e.FuturesBarData.BarValue:F2}");
    }

    /// <summary>
    /// Attempts each automatic reference-data import once and observes its correlated terminal event.
    /// Failure is reported without retrying or preventing the remaining application startup flow.
    /// </summary>
    internal async Task ImportReferenceDataAtStartupAsync(CancellationToken cancellationToken)
    {
        const string yieldCurveDataset = "Yield Curve";
        const string economicCalendarDataset = "Economic Calendar";
        var importDate = _timeProvider.GetLocalNow().DateTime;
        var yieldCurveCorrelation = new TerminalEventCorrelation();
        var economicCalendarCorrelation = new TerminalEventCorrelation();
        var cleanupFailures = new List<string>();
        StartupReferenceDataImportStatus? yieldCurveStatus = null;
        StartupReferenceDataImportStatus? economicCalendarStatus = null;
        var yieldCurveListenerStarted = false;
        var economicCalendarListenerStarted = false;

        try
        {
            try
            {
                await StartYieldCurveStartupImportListenerAsync(
                    yieldCurveCorrelation,
                    cancellationToken);
                yieldCurveListenerStarted = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                yieldCurveStatus = CreateStartupImportFailure(
                    yieldCurveDataset,
                    exception,
                    "terminal-event listener could not start");
            }

            try
            {
                await StartEconomicCalendarStartupImportListenerAsync(
                    economicCalendarCorrelation,
                    cancellationToken);
                economicCalendarListenerStarted = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                economicCalendarStatus = CreateStartupImportFailure(
                    economicCalendarDataset,
                    exception,
                    "terminal-event listener could not start");
            }

            var yieldCurveTask = yieldCurveListenerStarted
                ? ExecuteStartupImportAsync(
                    yieldCurveDataset,
                    importDate,
                    yieldCurveCorrelation,
                    model => model.ImportYieldCurveRatesAsync(importDate),
                    cancellationToken)
                : Task.FromResult(yieldCurveStatus!);
            var economicCalendarTask = economicCalendarListenerStarted
                ? ExecuteStartupImportAsync(
                    economicCalendarDataset,
                    importDate,
                    economicCalendarCorrelation,
                    model => model.ImportEconomicCalendarsAsync(importDate),
                    cancellationToken)
                : Task.FromResult(economicCalendarStatus!);

            var statuses = await Task.WhenAll(yieldCurveTask, economicCalendarTask);
            yieldCurveStatus = statuses[0];
            economicCalendarStatus = statuses[1];
        }
        finally
        {
            if (yieldCurveListenerStarted)
            {
                try
                {
                    await StopYieldCurveStartupImportListenerAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(
                        $"{yieldCurveDataset} terminal-event listener could not stop: {exception.Message}");
                }
            }

            if (economicCalendarListenerStarted)
            {
                try
                {
                    await StopEconomicCalendarStartupImportListenerAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(
                        $"{economicCalendarDataset} terminal-event listener could not stop: {exception.Message}");
                }
            }
        }

        YieldCurveStartupImport = yieldCurveStatus;
        EconomicCalendarStartupImport = economicCalendarStatus;
        await ReportStartupImportFailuresAsync(
            [yieldCurveStatus!, economicCalendarStatus!],
            cleanupFailures);
    }

    Task StartYieldCurveStartupImportListenerAsync(
        TerminalEventCorrelation correlation,
        CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataEventModel>().ExecuteObservableAsync(
            model => model.StartMarketDataListenerAsync(
            [
                new YieldCurveRatesImportedCompleteEvent()
                    .SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRatesImportedFailEvent()
                    .SetEventSource($"{EventTopic.MarketDataEvents}")
            ],
            @event =>
            {
                correlation.TryPublish(@event);
                return ValueTask.CompletedTask;
            }).AsTask(),
            cancellationToken);

    Task StopYieldCurveStartupImportListenerAsync()
        => _appRoot.GetModel<MarketDataEventModel>().ExecuteObservableAsync(
            model => model.StopMarketDataListenerAsync().AsTask(),
            CancellationToken.None);

    Task StartEconomicCalendarStartupImportListenerAsync(
        TerminalEventCorrelation correlation,
        CancellationToken cancellationToken)
        => _appRoot.GetModel<EconomicCalendarEventModel>().ExecuteObservableAsync(
            model => model.StartEconomicCalendarEventListenersAsync(
                _ => { },
                _ => { },
                _ => { },
                @event => correlation.TryPublish(@event),
                @event => correlation.TryPublish(@event)),
            cancellationToken);

    Task StopEconomicCalendarStartupImportListenerAsync()
        => _appRoot.GetModel<EconomicCalendarEventModel>().ExecuteObservableAsync(
            model => model.StopEconomicCalendarEventListenersAsync(),
            CancellationToken.None);

    async Task<StartupReferenceDataImportStatus> ExecuteStartupImportAsync(
        string dataset,
        DateTime importDate,
        TerminalEventCorrelation correlation,
        Func<MarketDataCommandModel, Task<Guid>> submitCommand,
        CancellationToken cancellationToken)
    {
        var commandId = Guid.Empty;
        correlation.BeginAttempt();
        try
        {
            await _appRoot.GetModel<MarketDataCommandModel>().ExecuteObservableAsync(
                async model => commandId = await submitCommand(model),
                cancellationToken);
            if (commandId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"The {dataset} startup import returned an empty correlation identifier.");
            }

            var terminalEvent = await correlation.AwaitAsync(
                commandId,
                _startupReferenceDataImportTimeout,
                _timeProvider,
                cancellationToken);
            if (terminalEvent is IErrorEvent error)
            {
                return new StartupReferenceDataImportStatus(
                    dataset,
                    StartupReferenceDataImportOutcome.Failed,
                    commandId,
                    error.ErrorCode,
                    $"{dataset} automatic import failed: {error.ErrorMessage}");
            }

            return new StartupReferenceDataImportStatus(
                dataset,
                StartupReferenceDataImportOutcome.Completed,
                commandId,
                0,
                $"{dataset} automatic import completed for {importDate:yyyy-MM-dd}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new StartupReferenceDataImportStatus(
                dataset,
                StartupReferenceDataImportOutcome.NotObserved,
                commandId,
                0,
                $"{dataset} automatic import outcome was not observed within "
                    + $"{_startupReferenceDataImportTimeout}.");
        }
        catch (Exception exception)
        {
            return CreateStartupImportFailure(dataset, exception, "automatic import failed", commandId);
        }
        finally
        {
            correlation.EndAttempt();
        }
    }

    static StartupReferenceDataImportStatus CreateStartupImportFailure(
        string dataset,
        Exception exception,
        string context,
        Guid commandId = default)
        => new(
            dataset,
            StartupReferenceDataImportOutcome.Failed,
            commandId,
            exception is ModelOperationException modelFailure ? modelFailure.ErrorCode : 0,
            $"{dataset} {context}: {exception.Message}");

    async Task ReportStartupImportFailuresAsync(
        IReadOnlyCollection<StartupReferenceDataImportStatus> statuses,
        IReadOnlyCollection<string> cleanupFailures)
    {
        var failures = statuses
            .Where(status => status.Outcome != StartupReferenceDataImportOutcome.Completed)
            .Select(status => status.Message)
            .Concat(cleanupFailures)
            .ToArray();
        if (failures.Length == 0)
            return;

        foreach (var failure in failures)
            await WriteStatusConsoleAsync(failure);

        var errorCode = statuses
            .Where(status => status.Outcome == StartupReferenceDataImportOutcome.Failed)
            .Select(status => status.ErrorCode)
            .FirstOrDefault(code => code != 0);
        PublishError(
            errorCode,
            string.Join(Environment.NewLine, failures)
                + Environment.NewLine
                + "No automatic retry was attempted. The imports remain available from their maintenance screens.",
            "Startup Reference Data Import");
    }

    /// <summary>
    /// enable trade live feed
    /// </summary>
    Task EnableTradeLiveFeed(CancellationToken cancellationToken = default)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Enable Trade Live Feed Error"));
            await WriteStatusConsoleAsync("Starting Trade Data Feeds...");
            await DelayStartupAsync(cancellationToken);
            if (_valueDate is not null)
                await model.StartDataFeedAsync([.. _baseContracts], _valueDate.Value);
        });

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="resetAction"></param>
    Task DisableTradeLiveFeed(Action? resetAction = null)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Disable Trade Live Feed Error"));
            await WriteStatusConsoleAsync("Stopping Trade Data Feeds...");
            if (_valueDate is not null)
                await model.StopDataFeedAsync(_valueDate.Value, async () => {
                    if (_baseContracts == null)
                        return;
                    foreach (var contract in _baseContracts)
                        await model.StopStreamingFuturesTickDataAsync(contract.ContractId, _valueDate.Value);
                    resetAction?.Invoke();
                });
        });

    /// <summary>
    /// enable market data feed reset listener
    /// </summary>
    Task EnableMarketDataFeedResetListener(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Enable MarketData Feed Reset Listener Error"));
            await WriteStatusConsoleAsync("Starting Market Data Feed Reset Listener...");
            await DelayStartupAsync(cancellationToken);
            await model.StartMarketDataFeedResetListenerAsync(
                _ => new ValueTask(EnableTradeLiveFeed()));
        });

    /// <summary>
    /// disable market data feed reset listener
    /// </summary>
    Task DisableMarketDataFeedResetListener()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Disable MarketData Feed Reset Listener Error"));
            await model.StopMarketDataFeedResetListenerAsync();
        });

    /// <summary>
    /// Runs the market-data watchdog until the application lifetime is cancelled.
    /// </summary>
    async Task ResetLiveFeedAsync(CancellationToken cancellationToken)
    {
        const int resetMaxTicks = 900;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            if (Interlocked.Increment(ref _resetTicks) <= resetMaxTicks)
                continue;

            Interlocked.Exchange(ref _resetTicks, 0);
            DateOnly? valueDate = null;
            await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async marketDataModel =>
                await marketDataModel.GetValueDateAsync(value => valueDate = value));

            if (!valueDate.HasValue)
                continue;

            await WriteStatusConsoleAsync("Reseting Live Feed...Market Data Feed Failing To Respond");
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
            {
                await model.ResetDataFeedAsync([.. _baseContracts], valueDate.Value);
                foreach (var contract in _baseContracts)
                    await model.DeleteFuturesBarDataAsync(
                        new FuturesBarDataId(contract.ContractId, contract.Symbol, valueDate.Value));
            });
        }
    }

    Task DelayStartupAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);

    async Task LoadStatusConsoleSnapshotsAsync(
        StatusConsoleViewModel statusConsole,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                statusConsole.LoadTradeStatusOperation.ExecuteAsync(cancellationToken),
                statusConsole.LoadMDIForwardLossRatiosOperation.ExecuteAsync(cancellationToken));
        }
        catch (ModelOperationException)
        {
            // The child ViewModel publishes the coded failure for the view while shell startup remains available.
        }
    }

    internal void PublishMarketOutlook(FuturesEodDataV2ReadModel futuresEodData)
        => MarketOutlook = new FuturesEodDataUIViewModel(futuresEodData);

    internal void PublishFuturesTradeSignal(FuturesTradeSignalV2ReadModel signal)
        => FuturesTradeSignal = new FuturesTradeSignalUIViewModel(signal);

    internal void PublishTradePlacementBatch(IReadOnlyList<IEvent> events)
    {
        if (events.Count == 0)
            return;

        var placements = events.Select(@event => new PlaceTradeUIViewModel(@event)).ToArray();
        lock (_tradePlacementGate)
        {
            foreach (var placement in placements)
                _tradePlacementBuffer.Insert(0, placement);
            if (_tradePlacementBuffer.Count > TradePlacementDisplayCapacity)
                _tradePlacementBuffer.RemoveRange(
                    TradePlacementDisplayCapacity,
                    _tradePlacementBuffer.Count - TradePlacementDisplayCapacity);
            TradePlacements = [.. _tradePlacementBuffer];
            LatestTradePlacement = placements[^1];
        }
    }

    internal void PublishFuturesBarSnapshot(
        string symbol,
        IEnumerable<FuturesBarDataReadModel> futuresBarData)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var bounded = futuresBarData
            .OrderBy(bar => bar.BarDate)
            .TakeLast(FuturesBarChartCapacity)
            .ToArray();
        lock (_marketDataStreamGate)
        {
            _futuresBarSnapshots[symbol] = bounded;
            FuturesBarSnapshots = new Dictionary<string, FuturesBarDataReadModel[]>(_futuresBarSnapshots);
            LatestFuturesBarSnapshot = new FuturesBarChartSnapshot(symbol, bounded);
        }
    }

    void PublishMarketOutlookMetrics(LatestValueChannelMetrics metrics)
    {
        lock (_marketDataStreamGate)
            MarketDataStreamMetrics = new IFMAppMarketDataStreamMetricsSnapshot(
                metrics,
                new Dictionary<string, LatestValueChannelMetrics>(_futuresBarMetrics));
    }

    void PublishFuturesBarMetrics(string symbol, LatestValueChannelMetrics metrics)
    {
        lock (_marketDataStreamGate)
        {
            _futuresBarMetrics[symbol] = metrics;
            MarketDataStreamMetrics = new IFMAppMarketDataStreamMetricsSnapshot(
                MarketDataStreamMetrics.MarketOutlook,
                new Dictionary<string, LatestValueChannelMetrics>(_futuresBarMetrics));
        }
    }

    void PublishFuturesTradeSignalMetrics(LatestValueChannelMetrics metrics)
    {
        lock (_realtimeStreamGate)
            RealtimeStreamMetrics = RealtimeStreamMetrics with { FuturesTradeSignals = metrics };
    }

    void PublishTradePlacementMetrics(OrderedBatchChannelMetrics metrics)
    {
        lock (_realtimeStreamGate)
            RealtimeStreamMetrics = RealtimeStreamMetrics with { TradePlacements = metrics };
    }

    void PublishStatusConsoleMetrics(OrderedBatchChannelMetrics metrics)
    {
        lock (_realtimeStreamGate)
            RealtimeStreamMetrics = RealtimeStreamMetrics with { StatusConsole = metrics };
    }

    internal void AppendStatusLog(StatusConsoleLogReadModel log)
        => AppendStatusLogs([log]);

    internal void AppendStatusLogs(IReadOnlyList<StatusConsoleLogReadModel> logs)
    {
        if (logs.Count == 0)
            return;

        StatusConsoleLogReadModel[] snapshot;
        lock (_statusLogGate)
        {
            foreach (var log in logs)
                _statusLogBuffer.Insert(0, log);
            if (_statusLogBuffer.Count > StatusLogCapacity)
                _statusLogBuffer.RemoveRange(StatusLogCapacity, _statusLogBuffer.Count - StatusLogCapacity);
            snapshot = [.. _statusLogBuffer];
        }

        StatusLogs = snapshot;
        LatestStatusLog = logs[^1];
        StatusLine = logs[^1].Message;
    }

    internal void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);

    /// <summary>Writes one status-console message with observable asynchronous completion.</summary>
    async Task WriteStatusConsoleAsync(
        string message,
        LogSourceType logSourceType = LogSourceType.IFMApp)
    {
        try
        {
            await _appRoot.GetStatusConsoleWriter().WriteConsoleAsync(logSourceType, message);
        }
        catch (Exception exception)
        {
            PublishError(0, exception.Message, "Status Console Write Error");
        }
    }

    static async ValueTask DisposeOperationAsync(IAsyncOperation operation)
    {
        try
        {
            await ((IAsyncDisposable)operation).DisposeAsync();
        }
        catch (Exception exception) when (ReferenceEquals(operation.LastFailure, exception))
        {
            // The caller already observed this completed operation failure.
        }
    }

    static void UpdateMaximum(ref long location, long value)
    {
        var current = Interlocked.Read(ref location);
        while (current < value)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

}
