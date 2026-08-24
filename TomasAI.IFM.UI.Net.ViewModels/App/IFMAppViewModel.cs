using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Operations.Domain;
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
    const string MarketOutlookSymbol = "ES";
    internal static readonly TimeSpan FuturesBarChartHistory = TimeSpan.FromHours(6);
    internal static readonly TimeSpan FuturesBarContinuityTolerance = TimeSpan.FromSeconds(45);
    static readonly TimeSpan DefaultStartupReferenceDataImportTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DefaultMarketDataFeedTerminalTimeout = TimeSpan.FromSeconds(60);
    readonly object _statusLogGate = new();
    readonly object _marketDataStreamGate = new();
    readonly object _realtimeStreamGate = new();
    readonly object _tradePlacementGate = new();
    readonly SemaphoreSlim _marketDataFeedOperationGate = new(1, 1);
    readonly IAppRoot _appRoot;
    readonly IIFMAppLiveViewAdapter _liveViewAdapter;
    readonly IEconomicCalendarService _economicCalendarService;
    readonly TimeProvider _timeProvider;
    readonly TimeSpan _startupReferenceDataImportTimeout;
    readonly TimeSpan _marketDataFeedTerminalTimeout;
    readonly TerminalEventCorrelation _marketDataFeedTerminalCorrelation = new();
    readonly MarketDataFeedHealthMonitor _marketDataFeedHealthMonitor = new();
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly Guid _siteId;
    readonly Version _appVersion;
    readonly string _appEnvironment;
    readonly List<StatusConsoleLogReadModel> _statusLogBuffer = [];
    readonly List<PlaceTradeUIViewModel> _tradePlacementBuffer = [];
    readonly Dictionary<string, FuturesBarDataReadModel[]> _futuresBarSnapshots = [];
    readonly Dictionary<string, LatestValueChannelMetrics> _futuresBarMetrics = [];
    KeyedLatestValueAsyncChannel<string, FuturesBarDataInsertedCompleteEvent>? _futuresBarChannels;
    LatestValueAsyncChannel<MarketOutlookSnapshotReadModel>? _compositeMarketOutlookChannel;
    OrderedBatchAsyncChannel<IEvent>? _tradePlacementChannel;
    OrderedBatchAsyncChannel<StatusConsoleLogReadModel>? _statusConsoleChannel;
    IUiEventSubscription? _economicCalendarStartupSubscription;
    DateOnly? _valueDate;
    IReadOnlyList<FuturesContractV2ReadModel> _baseContracts = [];
    IReadOnlyList<StatusConsoleLogReadModel> _statusLogs = [];
    StatusConsoleLogReadModel? _latestStatusLog;
    string _statusLine = string.Empty;
    bool _isMenuEnabled;
    bool _isMarketDataFeedActive;
    bool _isMarketDataFeedOperationInProgress;
    bool _isCloseRequested;
    PresentationError? _lastError;
    StartupReferenceDataImportStatus? _yieldCurveStartupImport;
    StartupReferenceDataImportStatus? _economicCalendarStartupImport;
    IntradaySignalLifecycleResult? _intradaySignalStartup;
    OperationsViewModel? _operations;
    long _errorSequence;
    MarketDataFeedHealthState _marketDataFeedHealthState = MarketDataFeedHealthState.Inactive;
    long _marketOutlookRevision;
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
    /// <param name="economicCalendarService">Economic-calendar operations and event subscriptions.</param>
    /// <param name="timeProvider">Optional time provider used by startup delays and the feed watchdog.</param>
    /// <param name="startupReferenceDataImportTimeout">Bounded wait for each startup import terminal event.</param>
    public IFMAppViewModel(
        IAppRoot appRoot,
        Version appVersion,
        string appEnvironment,
        IIFMAppLiveViewAdapter liveViewAdapter,
        IEconomicCalendarService economicCalendarService,
        TimeProvider? timeProvider = null,
        TimeSpan? startupReferenceDataImportTimeout = null,
        TimeSpan? marketDataFeedTerminalTimeout = null)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
        _appEnvironment = string.IsNullOrWhiteSpace(appEnvironment)
            ? throw new ArgumentException("Application environment is required.", nameof(appEnvironment))
            : appEnvironment;
        _liveViewAdapter = liveViewAdapter ?? throw new ArgumentNullException(nameof(liveViewAdapter));
        _economicCalendarService = economicCalendarService
            ?? throw new ArgumentNullException(nameof(economicCalendarService));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startupReferenceDataImportTimeout = startupReferenceDataImportTimeout
            ?? DefaultStartupReferenceDataImportTimeout;
        _marketDataFeedTerminalTimeout = marketDataFeedTerminalTimeout
            ?? DefaultMarketDataFeedTerminalTimeout;
        if (_startupReferenceDataImportTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startupReferenceDataImportTimeout),
                "The startup reference-data import timeout must be positive and bounded.");
        }
        if (_marketDataFeedTerminalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(marketDataFeedTerminalTimeout),
                "The market-data feed terminal timeout must be positive and bounded.");
        }
        _siteId = Guid.NewGuid();
        _appRoot.Services.CommandResponses.SetSiteId(_siteId);
        _lifecycle = new AsyncLifecycleCoordinator(InitializeCoreAsync, StopCoreAsync);
        StartupOperation = new AsyncOperation(StartupCorePresentationAsync, () => !_lifecycle.IsRunning);
        ShutdownOperation = new AsyncOperation(ShutdownCorePresentationAsync, () => _lifecycle.IsRunning);
    }

    /// <summary>Gets the current trading value date when live services are available.</summary>
    public DateOnly? ValueDate
    {
        get => _valueDate;
        private set
        {
            if (SetProperty(ref _valueDate, value))
                OnPropertyChanged(nameof(CanToggleMarketDataFeed));
        }
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
        private set
        {
            if (SetProperty(ref _isMenuEnabled, value))
                OnPropertyChanged(nameof(CanToggleMarketDataFeed));
        }
    }

    /// <summary>Gets whether the current market-data feed was accepted as active by the backend.</summary>
    public bool IsMarketDataFeedActive
    {
        get => _isMarketDataFeedActive;
        private set
        {
            if (!SetProperty(ref _isMarketDataFeedActive, value))
                return;
            OnPropertyChanged(nameof(MarketDataFeedActionText));
            OnPropertyChanged(nameof(MarketDataFeedStateText));
        }
    }

    /// <summary>Gets the downstream health of all currently traded futures feeds.</summary>
    public MarketDataFeedHealthState MarketDataFeedHealthState
    {
        get => _marketDataFeedHealthState;
        private set
        {
            if (SetProperty(ref _marketDataFeedHealthState, value))
                OnPropertyChanged(nameof(MarketDataFeedStateText));
        }
    }

    /// <summary>Gets whether a shell-initiated market-data feed transition is in progress.</summary>
    public bool IsMarketDataFeedOperationInProgress
    {
        get => _isMarketDataFeedOperationInProgress;
        private set
        {
            if (!SetProperty(ref _isMarketDataFeedOperationInProgress, value))
                return;
            OnPropertyChanged(nameof(CanToggleMarketDataFeed));
            OnPropertyChanged(nameof(MarketDataFeedStateText));
        }
    }

    /// <summary>Gets whether the operator can start or stop the current market-data feed.</summary>
    public bool CanToggleMarketDataFeed
        => IsMenuEnabled && ValueDate.HasValue && !IsMarketDataFeedOperationInProgress;

    /// <summary>Gets the operator action that will be performed by the shell feed control.</summary>
    public string MarketDataFeedActionText
        => IsMarketDataFeedActive ? "Stop Market Feed" : "Start Market Feed";

    /// <summary>Gets the visible current market-data feed state.</summary>
    public string MarketDataFeedStateText
        => IsMarketDataFeedOperationInProgress
            ? "Market Feed: Changing"
            : MarketDataFeedHealthState switch
            {
                MarketDataFeedHealthState.Healthy
                    => "Market Feed: Healthy — current contracts updated within 1 minute",
                MarketDataFeedHealthState.Intermittent
                    => "Market Feed: Intermittent — a current contract update is overdue",
                MarketDataFeedHealthState.Failed
                    => "Market Feed: Failed — current contract updates have remained intermittent for 5 minutes",
                MarketDataFeedHealthState.Critical
                    => "Market Feed: Critical — stop and restart the market feed",
                MarketDataFeedHealthState.OutsidePositionEntryWindow
                    => "Market Feed: Active — monitoring paused outside 03:00–16:00 Eastern; exits only",
                _ => "Market Feed: Inactive"
            };

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

    /// <summary>Gets the lifecycle-owned Operations region, with Strategy selected by default.</summary>
    public OperationsViewModel? Operations
    {
        get => _operations;
        private set => SetProperty(ref _operations, value);
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
        _marketDataFeedOperationGate.Dispose();
    }

    /// <summary>
    /// Starts or stops the current market-data feed through the same domain command path used at startup and
    /// shutdown. Transitions are serialized so repeated UI clicks cannot submit overlapping commands.
    /// </summary>
    public async Task ToggleMarketDataFeedAsync(CancellationToken cancellationToken = default)
    {
        await _marketDataFeedOperationGate.WaitAsync(cancellationToken);
        try
        {
            if (!ValueDate.HasValue)
                throw new InvalidOperationException("The market-data feed is unavailable without a trading value date.");

            IsMarketDataFeedOperationInProgress = true;
            if (IsMarketDataFeedActive)
                await DisableTradeLiveFeed(cancellationToken: cancellationToken);
            else
                await EnableTradeLiveFeed(cancellationToken);
        }
        finally
        {
            IsMarketDataFeedOperationInProgress = false;
            _marketDataFeedOperationGate.Release();
        }
    }

    async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await StartStatusConsoleListener();
        await StartApplicationEventsListener();
        await StartMarketDataFeedStatusListener();
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
        if (Operations is not null)
        {
            Operations.PropertyChanged -= OperationsPropertyChanged;
            await Operations.DisposeAsync();
        }
        await StopMarketOutlookEventConsumer();
        await StopFuturesBarDataEventConsumer();
        await StopTradePlacementEventConsumer();
        await StopFuturesIntradaySignalServices();
        await DisableMarketDataFeedResetListener();
        if (IsMarketDataFeedActive)
            await DisableTradeLiveFeed(cancellationToken: cancellationToken);
        await StopMarketDataFeedStatusListener();
        await _appRoot.Services.ApplicationEvents.StopApplicationEventConsumerAsync();
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
        => _appRoot.Services.MarketDataQueries.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) => PublishError(
                errorCode,
                $"Unable to connect to IFM servers {Environment.NewLine}{errorMsg}",
                "Market Data Error"));
            ICollection<FuturesContractV2ReadModel>? futuresContracts = null;
            await model.GetCurrentlyTradedFuturesContractsAsync(values => futuresContracts = values);
            BaseContracts = futuresContracts?.ToArray() ?? [];
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
            var strategyContractId = BaseContracts
                .Where(contract => contract.Symbol == MarketOutlookSymbol)
                .Select(contract => contract.ContractId)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(strategyContractId))
            {
                Operations = new OperationsViewModel(
                    _appRoot,
                    strategyContractId,
                    ValueDate.Value);
                Operations.PropertyChanged += OperationsPropertyChanged;
                await Operations.InitializeAsync(cancellationToken);
            }

            await GetLastFuturesBarData(valueDate.Value);
            await StartMarketOutlookEventConsumer(cancellationToken);
            await StartFuturesBarDataEventConsumer(cancellationToken);
            await StartTradePlacementEventConsumer(cancellationToken);
            await EnableMarketDataFeedResetListener(cancellationToken);
            await EnableTradeLiveFeed(cancellationToken);
            _lifecycle.RunAsync(MonitorMarketDataFeedHealthAsync);
            await StartFuturesIntradaySignalServices(cancellationToken);
            await WriteStatusConsoleAsync(
                $"IFMApp v{_appVersion} - {_appEnvironment}...initialization complete");
            IsMenuEnabled = true;
            StartupOperation.NotifyCanExecuteChanged();
            ShutdownOperation.NotifyCanExecuteChanged();
        });

    void OperationsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
        => OnPropertyChanged(nameof(Operations));


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
        return _appRoot.Services.StatusConsole.ExecuteAsync(async model => {
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
            await _appRoot.Services.StatusConsole.StopStatusConsoleLogListener(_siteId);
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
        => _appRoot.Services.ApplicationEvents.ExecuteAsync(async model => {
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

    /// <summary>
    /// Subscribes before loading the persisted composite snapshot. Revision checks
    /// make the subscription/query overlap deterministic.
    /// </summary>
    async Task StartMarketOutlookEventConsumer(CancellationToken cancellationToken)
    {
        if (_compositeMarketOutlookChannel is not null)
            return;
        var channel = new LatestValueAsyncChannel<MarketOutlookSnapshotReadModel>(
            ProcessMarketOutlookSnapshotAsync,
            minimumInterval: TimeSpan.FromMilliseconds(50),
            timeProvider: _timeProvider,
            metricsChanged: PublishMarketOutlookMetrics);
        _compositeMarketOutlookChannel = channel;
        PublishMarketOutlookMetrics(channel.Metrics);

        await _appRoot.Services.AnalyticsCommands.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Market Outlook Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Market Outlook Event Consumer...");
            await model.StartMarketOutlookEventConsumerAsync(_siteId, notification =>
            {
                var expectedContractId = GetMarketOutlookContract()?.ContractId;
                if (notification?.MarketOutlook is { } snapshot
                    && IsMarketOutlookUpdate(expectedContractId, snapshot.ContractId))
                {
                    channel.TryWrite(snapshot);
                }
            });
        });

        var contract = GetMarketOutlookContract();
        if (contract is null || !_valueDate.HasValue)
            return;
        await _appRoot.Services.AnalyticsQueries.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Loading Market Outlook Snapshot Error"));
            await model.GetMarketOutlookSnapshotAsync(
                contract.ContractId,
                _valueDate.Value,
                snapshot =>
                {
                    if (snapshot is not null
                        && IsMarketOutlookUpdate(contract.ContractId, snapshot.ContractId))
                        channel.TryWrite(snapshot);
                });
        });
    }

    async ValueTask ProcessMarketOutlookSnapshotAsync(
        MarketOutlookSnapshotReadModel snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.Revision <= Interlocked.Read(ref _marketOutlookRevision))
            return;
        Interlocked.Exchange(ref _marketOutlookRevision, snapshot.Revision);
        PublishMarketOutlook(snapshot.FuturesEodData);
        if (snapshot.FuturesTradeSignal is { } tradeSignal)
            PublishFuturesTradeSignal(tradeSignal);
        await WriteStatusConsoleAsync(
            $"{snapshot.ContractId} Market Outlook revision {snapshot.Revision}",
            LogSourceType.MarketDataFeedEvent);
    }

    async Task StopMarketOutlookEventConsumer()
    {
        var channel = Interlocked.Exchange(ref _compositeMarketOutlookChannel, null);
        try
        {
            await _appRoot.Services.AnalyticsCommands.ExecuteAsync(
                async model => await model.StopMarketOutlookEventConsumerAsync(_siteId));
        }
        finally
        {
            if (channel is not null)
                await channel.StopAsync();
        }
    }

        Task GetLastFuturesBarData(DateOnly valueDate)
            => _appRoot.Services.FeedQueries.ExecuteAsync(async model =>
            {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Loading Latest Futures Bar Data Error"));
                await WriteStatusConsoleAsync("Loading Latest Futures Bar Data...");
                foreach (var contract in _baseContracts ?? [])
                {
                    var (startDate, endDate) = GetFuturesBarChartWindow(
                        _timeProvider.GetUtcNow().UtcDateTime);
                    FuturesBarDataReadModel[] bars = [];
                    await model.GetFuturesBarDataAsync(
                        contract.ContractId,
                        contract.Symbol,
                        valueDate,
                        startDate,
                        endDate,
                        values => bars = values ?? []);
                    if (bars.Length > 0)
                        PublishFuturesBarSnapshot(contract.Symbol, bars);
                }
            });

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
        return _appRoot.Services.TradePlacementEvents.ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Starting Trade Placement Event Consumer Error"));
            await WriteStatusConsoleAsync("Starting Trade Placement Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartTradePlacementListenerAsync(e => channel.WriteAsync(e));
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
            await _appRoot.Services.TradePlacementEvents.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Stopping Trade Placement Event Consumer Error"));
                await WriteStatusConsoleAsync("Stopping Trade Placement Event Consumer...");
                await model.StopTradePlacementListenerAsync();
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
        => _appRoot.Services.AnalyticsCommands.ExecuteAsync(async model =>
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
        => _appRoot.Services.AnalyticsCommands.ExecuteAsync(async model =>
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
        => _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
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
            await _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
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

    async ValueTask QueueFuturesBarRefreshAsync(FuturesBarDataInsertedCompleteEvent e)
    {
        var symbol = e.FuturesBarData.Symbol;
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        await ApplyMarketDataFeedHealthAsync(_marketDataFeedHealthMonitor.RecordUpdate(
            e.FuturesBarData.ContractId,
            _timeProvider.GetUtcNow()));
        _futuresBarChannels?.TryWrite(symbol, e);
    }

    async ValueTask ProcessFuturesBarRefreshAsync(
        FuturesBarDataInsertedCompleteEvent e,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _appRoot.Services.FeedQueries.ExecuteAsync(async queryModel =>
        {
            queryModel.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Loading Futures Bar Data Error"));
            var (startDate, endDate) = GetFuturesBarChartWindow(
                _timeProvider.GetUtcNow().UtcDateTime);
            await queryModel.GetFuturesBarDataAsync(
                e.FuturesBarData.ContractId,
                e.FuturesBarData.Symbol,
                e.FuturesBarData.ValueDate,
                startDate,
                endDate,
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
        var importDate = EasternTime.GetNow(_timeProvider);
        var yieldCurveCorrelation = new TerminalEventCorrelation();
        var economicCalendarCorrelation = new TerminalNotificationCorrelation();
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
                ? ExecuteEconomicCalendarStartupImportAsync(
                    economicCalendarDataset,
                    importDate,
                    economicCalendarCorrelation,
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
        => _appRoot.Services.MarketDataEvents.ExecuteObservableAsync(
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
        => _appRoot.Services.MarketDataEvents.ExecuteObservableAsync(
            model => model.StopMarketDataListenerAsync().AsTask(),
            CancellationToken.None);

    async Task StartEconomicCalendarStartupImportListenerAsync(
        TerminalNotificationCorrelation correlation,
        CancellationToken cancellationToken)
    {
        var subscription = _economicCalendarService.CreateSubscription(
            notification => correlation.TryPublish(notification));
        _economicCalendarStartupSubscription = subscription;
        try
        {
            await subscription.StartAsync(cancellationToken);
        }
        catch
        {
            Interlocked.CompareExchange(
                ref _economicCalendarStartupSubscription,
                null,
                subscription);
            await subscription.DisposeAsync();
            throw;
        }
    }

    async Task StopEconomicCalendarStartupImportListenerAsync()
    {
        var subscription = Interlocked.Exchange(
            ref _economicCalendarStartupSubscription,
            null);
        if (subscription is null)
            return;

        try
        {
            await subscription.StopAsync(CancellationToken.None);
        }
        finally
        {
            await subscription.DisposeAsync();
        }
    }

    async Task<StartupReferenceDataImportStatus> ExecuteStartupImportAsync(
        string dataset,
        DateTime importDate,
        TerminalEventCorrelation correlation,
        Func<MarketDataCommandService, Task<Guid>> submitCommand,
        CancellationToken cancellationToken)
    {
        var commandId = Guid.Empty;
        correlation.BeginAttempt();
        try
        {
            await _appRoot.Services.MarketDataCommands.ExecuteObservableAsync(
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

    async Task<StartupReferenceDataImportStatus> ExecuteEconomicCalendarStartupImportAsync(
        string dataset,
        DateTime importDate,
        TerminalNotificationCorrelation correlation,
        CancellationToken cancellationToken)
    {
        var commandId = Guid.Empty;
        correlation.BeginAttempt();
        try
        {
            commandId = (await _economicCalendarService.ImportAsync(
                importDate,
                [],
                cancellationToken)).RequireValue();
            if (commandId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"The {dataset} startup import returned an empty correlation identifier.");
            }

            var notification = await correlation.AwaitAsync(
                commandId,
                _startupReferenceDataImportTimeout,
                _timeProvider,
                cancellationToken);
            if (notification.IsFailure)
            {
                return new StartupReferenceDataImportStatus(
                    dataset,
                    StartupReferenceDataImportOutcome.Failed,
                    commandId,
                    notification.ErrorCode,
                    $"{dataset} automatic import failed: {notification.ErrorMessage}");
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
            exception switch
            {
                UiServiceOperationException modelFailure => modelFailure.ErrorCode,
                UiOperationException serviceFailure => serviceFailure.ErrorCode,
                _ => 0
            },
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
    async Task<Guid> EnableTradeLiveFeed(CancellationToken cancellationToken = default)
    {
        var commandId = Guid.Empty;
        ApplyMarketDataFeedHealth(_marketDataFeedHealthMonitor.Activate(
            _baseContracts.Select(contract => contract.ContractId),
            _timeProvider.GetUtcNow()));
        _marketDataFeedTerminalCorrelation.BeginAttempt();
        try
        {
            await _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Enable Trade Live Feed Error"));
                await WriteStatusConsoleAsync("Starting Trade Data Feeds...");
                await DelayStartupAsync(cancellationToken);
                if (_valueDate is not null)
                    commandId = await model.StartDataFeedAsync([.. _baseContracts], _valueDate.Value);
            });
            if (commandId != Guid.Empty
                && await ObserveMarketDataFeedTerminalAsync<MarketDataFeedStartedCompleteEvent>(
                    commandId,
                    "Enable Trade Live Feed Error",
                    cancellationToken))
                IsMarketDataFeedActive = true;
            return commandId;
        }
        finally
        {
            if (!IsMarketDataFeedActive)
                ApplyMarketDataFeedHealth(_marketDataFeedHealthMonitor.Deactivate());
            _marketDataFeedTerminalCorrelation.EndAttempt();
        }
    }

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="resetAction"></param>
    async Task<Guid> DisableTradeLiveFeed(
        Action? resetAction = null,
        CancellationToken cancellationToken = default)
    {
        var commandId = Guid.Empty;
        _marketDataFeedTerminalCorrelation.BeginAttempt();
        try
        {
            await _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) =>
                    PublishError(errorCode, errorMessage, "Disable Trade Live Feed Error"));
                await WriteStatusConsoleAsync("Stopping Trade Data Feeds...");
                if (_valueDate is not null)
                    commandId = await model.StopDataFeedAsync(_valueDate.Value, async () => {
                        if (_baseContracts == null)
                            return;
                        foreach (var contract in _baseContracts)
                            await model.StopStreamingFuturesTickDataAsync(contract.ContractId, _valueDate.Value);
                        resetAction?.Invoke();
                    });
            });
            if (commandId != Guid.Empty
                && await ObserveMarketDataFeedTerminalAsync<MarketDataFeedStoppedCompleteEvent>(
                    commandId,
                    "Disable Trade Live Feed Error",
                    cancellationToken))
            {
                IsMarketDataFeedActive = false;
                ApplyMarketDataFeedHealth(_marketDataFeedHealthMonitor.Deactivate());
            }
            return commandId;
        }
        finally
        {
            _marketDataFeedTerminalCorrelation.EndAttempt();
        }
    }

    async Task<bool> ObserveMarketDataFeedTerminalAsync<TCompleteEvent>(
        Guid commandId,
        string errorCaption,
        CancellationToken cancellationToken)
        where TCompleteEvent : class, IEvent
    {
        try
        {
            var terminal = await _marketDataFeedTerminalCorrelation.AwaitAsync(
                commandId,
                _marketDataFeedTerminalTimeout,
                _timeProvider,
                cancellationToken);
            if (terminal is TCompleteEvent)
                return true;
            if (terminal is IErrorEvent error)
                PublishError(error.ErrorCode, error.ErrorMessage, errorCaption);
            else
                PublishError(0, $"Unexpected terminal event {terminal.EventName}.", errorCaption);
        }
        catch (TimeoutException)
        {
            PublishError(
                0,
                $"Market-data feed command {commandId} did not produce a terminal event within {_marketDataFeedTerminalTimeout}.",
                errorCaption);
        }
        return false;
    }

    Task StartMarketDataFeedStatusListener()
        => _appRoot.Services.FeedCommands.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Market Data Feed Status Listener Error"));
            await model.StartMarketDataFeedStatusListenerAsync(@event =>
            {
                _marketDataFeedTerminalCorrelation.TryPublish(@event);
                return ValueTask.CompletedTask;
            });
        });

    Task StopMarketDataFeedStatusListener()
        => _appRoot.Services.FeedCommands.ExecuteAsync(
            model => model.StopMarketDataFeedStatusListenerAsync());

    /// <summary>
    /// enable market data feed reset listener
    /// </summary>
    Task EnableMarketDataFeedResetListener(CancellationToken cancellationToken)
        => _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
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
        => _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
            model.OnError((errorCode, errorMessage) =>
                PublishError(errorCode, errorMessage, "Disable MarketData Feed Reset Listener Error"));
            await model.StopMarketDataFeedResetListenerAsync();
        });

    /// <summary>
    /// Evaluates downstream updates for the current futures contracts until shutdown.
    /// </summary>
    async Task MonitorMarketDataFeedHealthAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            await ApplyMarketDataFeedHealthAsync(
                _marketDataFeedHealthMonitor.Evaluate(_timeProvider.GetUtcNow()));
        }
    }

    void ApplyMarketDataFeedHealth(MarketDataFeedHealthSnapshot snapshot)
        => MarketDataFeedHealthState = snapshot.State;

    async Task ApplyMarketDataFeedHealthAsync(MarketDataFeedHealthSnapshot snapshot)
    {
        ApplyMarketDataFeedHealth(snapshot);
        if (!snapshot.EnteredCritical)
            return;

        var contracts = snapshot.StaleContractIds.Count == 0
            ? "currently traded contracts"
            : string.Join(", ", snapshot.StaleContractIds);
        var message = $"Currently traded market-data feeds have failed ({contracts}). "
            + "Select Stop Market Feed, then Start Market Feed to reconnect.";
        PublishError(0, message, "Market Data Feed Problem");
        await WriteStatusConsoleAsync(message);
    }

    Task DelayStartupAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);

    internal void PublishMarketOutlook(FuturesEodDataV2ReadModel futuresEodData)
        => MarketOutlook = new FuturesEodDataUIViewModel(futuresEodData);

    FuturesContractV2ReadModel? GetMarketOutlookContract()
        => _baseContracts.FirstOrDefault(contract =>
            string.Equals(contract.Id.Symbol, MarketOutlookSymbol, StringComparison.Ordinal));

    internal static bool IsMarketOutlookUpdate(
        string? expectedContractId,
        FuturesEodDataV2ReadModel? futuresEodData)
        => futuresEodData is not null
           && string.Equals(futuresEodData.Symbol, MarketOutlookSymbol, StringComparison.Ordinal)
           && IsMarketOutlookUpdate(expectedContractId, futuresEodData.ContractId);

    internal static bool IsMarketOutlookUpdate(string? expectedContractId, string? actualContractId)
        => !string.IsNullOrWhiteSpace(expectedContractId)
           && string.Equals(expectedContractId, actualContractId, StringComparison.Ordinal);

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

        var bounded = SelectContinuousFuturesBarWindow(
            futuresBarData,
            _timeProvider.GetUtcNow().UtcDateTime);
        lock (_marketDataStreamGate)
        {
            _futuresBarSnapshots[symbol] = bounded;
            FuturesBarSnapshots = new Dictionary<string, FuturesBarDataReadModel[]>(_futuresBarSnapshots);
            LatestFuturesBarSnapshot = new FuturesBarChartSnapshot(symbol, bounded);
        }
    }

    internal static (DateTime StartDate, DateTime EndDate) GetFuturesBarChartWindow(
        DateTime marketCurrentTimeUtc)
    {
        var normalizedCurrentTime = marketCurrentTimeUtc.Kind == DateTimeKind.Utc
            ? marketCurrentTimeUtc
            : marketCurrentTimeUtc.ToUniversalTime();
        var endDate = normalizedCurrentTime.AddSeconds(1);
        return (normalizedCurrentTime.Subtract(FuturesBarChartHistory), endDate);
    }

    internal static FuturesBarDataReadModel[] SelectContinuousFuturesBarWindow(
        IEnumerable<FuturesBarDataReadModel> futuresBarData,
        DateTime marketCurrentTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(futuresBarData);
        var (startDate, endDate) = GetFuturesBarChartWindow(marketCurrentTimeUtc);
        var ordered = futuresBarData
            .Where(bar => bar.BarRateType == BarRateType.FifteenSeconds
                          && bar.BarDate >= startDate
                          && bar.BarDate <= endDate)
            .OrderBy(bar => bar.BarDate)
            .TakeLast(FuturesBarChartCapacity)
            .ToArray();
        if (ordered.Length < 2)
            return ordered;

        var segmentStart = ordered.Length - 1;
        while (segmentStart > 0
               && ordered[segmentStart].BarDate - ordered[segmentStart - 1].BarDate
               <= FuturesBarContinuityTolerance)
        {
            segmentStart--;
        }

        return segmentStart == 0 ? ordered : ordered[segmentStart..];
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
