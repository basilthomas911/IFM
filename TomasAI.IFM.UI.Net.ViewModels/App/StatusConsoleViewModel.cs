using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>
/// Owns the status-console analytics listener and exposes framework-neutral observable snapshots.
/// </summary>
public sealed class StatusConsoleViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly object _stateGate = new();
    readonly string _contractId;
    readonly DateOnly _valueDate;
    readonly MarketDataAnalyticsQueryModel _analyticsQueryModel;
    readonly ReferenceQueryModel _referenceQueryModel;
    readonly MarketDataAnalyticsEventModel _eventModel;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly Guid _siteId = Guid.NewGuid();
    List<FuturesItiSignalV2ReadModel> _tradeSignals = [];
    FuturesTradeSignalV2ReadModel? _futuresTradeSignal;
    IReadOnlyList<FuturesItiSignalV2ReadModel> _publishedTradeSignals = [];
    IReadOnlyList<MDIForwardLossRatioUIViewModel> _mdiForwardLossRatios = [];
    FuturesItiSignalV2ReadModel? _latestTrendExtreme;
    FuturesTradeStatusUIViewModel _tradeStatus = CreateDefaultTradeStatus();
    PresentationError? _lastError;
    long _errorSequence;
    int _acceptEvents;

    /// <summary>Creates status-console state for one underlying contract and value date.</summary>
    public StatusConsoleViewModel(IAppRoot appRoot, string contractId, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        _contractId = contractId;
        _valueDate = valueDate;
        _analyticsQueryModel = appRoot.GetModel<MarketDataAnalyticsQueryModel>();
        _referenceQueryModel = appRoot.GetModel<ReferenceQueryModel>();
        _eventModel = appRoot.GetModel<MarketDataAnalyticsEventModel>();
        _lifecycle = new AsyncLifecycleCoordinator(StartConsumerCoreAsync, StopConsumerCoreAsync);
        LoadTradeStatusOperation = new AsyncOperation(LoadTradeStatusCoreAsync);
        LoadMDIForwardLossRatiosOperation = new AsyncOperation(LoadMDIForwardLossRatiosCoreAsync);
    }

    /// <summary>Gets the currently published trend-direction history, newest first.</summary>
    public IReadOnlyList<FuturesItiSignalV2ReadModel> TradeSignals
    {
        get => _publishedTradeSignals;
        private set => SetProperty(ref _publishedTradeSignals, value);
    }

    /// <summary>Gets the latest computed trade-status presentation.</summary>
    public FuturesTradeStatusUIViewModel TradeStatus
    {
        get => _tradeStatus;
        private set => SetProperty(ref _tradeStatus, value);
    }

    /// <summary>Gets the latest trend-extreme notification.</summary>
    public FuturesItiSignalV2ReadModel? LatestTrendExtreme
    {
        get => _latestTrendExtreme;
        private set => SetProperty(ref _latestTrendExtreme, value);
    }

    /// <summary>Gets the combined up-trend and down-trend forward-loss-ratio snapshot.</summary>
    public IReadOnlyList<MDIForwardLossRatioUIViewModel> MDIForwardLossRatios
    {
        get => _mdiForwardLossRatios;
        private set => SetProperty(ref _mdiForwardLossRatios, value);
    }

    /// <summary>Gets the latest listener or query error notification.</summary>
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets the single-flight operation that reloads trend-direction history.</summary>
    public IAsyncOperation LoadTradeStatusOperation { get; }

    /// <summary>Gets the single-flight operation that reloads forward-loss ratios.</summary>
    public IAsyncOperation LoadMDIForwardLossRatiosOperation { get; }

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
        await DisposeOperationAsync(LoadTradeStatusOperation);
        await DisposeOperationAsync(LoadMDIForwardLossRatiosOperation);
    }

    /// <summary>Computes the current trade-status snapshot from the latest trend and trade signals.</summary>
    public FuturesTradeStatusUIViewModel GetTradeStatus()
    {
        lock (_stateGate)
            return BuildTradeStatus(_tradeSignals.FirstOrDefault(), _futuresTradeSignal);
    }

    async Task LoadTradeStatusCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            FuturesItiSignalV2ReadModel[] loaded = [];
            await _analyticsQueryModel.ExecuteObservableAsync(
                async model => await model.GetFuturesItiTrendDirectionChangedSignalsAsync(
                    _contractId,
                    _valueDate,
                    TimeFrameType.Weekly,
                    values => loaded = values ?? []),
                cancellationToken);

            lock (_stateGate)
                _tradeSignals = [.. loaded];
            PublishTradeState();
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception.ErrorCode, exception.Message, "Trade Status Error");
            throw;
        }
    }

    async Task LoadMDIForwardLossRatiosCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            MDIForwardLossRatioReadModel[] upTrend = [];
            MDIForwardLossRatioReadModel[] downTrend = [];
            await _referenceQueryModel.ExecuteObservableAsync(
                async model =>
                {
                    await model.LoadMDIFowardLossRatiosAsync(
                        IntrinsicTimeTrendType.UpTrend,
                        TradeType.ShortIronCondor,
                        values => upTrend = values ?? []);
                    await model.LoadMDIFowardLossRatiosAsync(
                        IntrinsicTimeTrendType.DownTrend,
                        TradeType.LongIronCondor,
                        values => downTrend = values ?? []);
                },
                cancellationToken);

            MDIForwardLossRatios = upTrend
                .OrderByDescending(value => value.MDI)
                .Concat(downTrend.OrderByDescending(value => value.MDI))
                .Select(value => new MDIForwardLossRatioUIViewModel(value))
                .ToArray();
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception.ErrorCode, exception.Message, "Forward Loss Ratio Error");
            throw;
        }
    }

    async Task StartConsumerCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acceptEvents, 1);
        _eventModel.OnError((errorCode, errorMessage) =>
            PublishError(errorCode, errorMessage, "Market Data Analytics Listener Error"));
        try
        {
            await _eventModel.ExecuteAsync(
                async model => await model.StartFuturesItiSignalEventListenersAsync(
                    _siteId,
                    OnFuturesItiSignal,
                    OnFuturesTradeSignal),
                cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _acceptEvents, 0);
            _eventModel.OnError(null!);
            throw;
        }
    }

    async Task StopConsumerCoreAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _acceptEvents, 0);
        try
        {
            await _eventModel.ExecuteAsync(
                async model => await model.StopFuturesItiSignalEventListenersAsync(_siteId),
                cancellationToken);
        }
        finally
        {
            _eventModel.OnError(null!);
        }
    }

    void OnFuturesItiSignal(FuturesItiSignalUpdatedNotifyEvent notification)
    {
        var signal = notification.FuturesItiSignal;
        switch (signal.IntrinsicTimeMode)
        {
            case IntrinsicTimeModeType.TrendDirectionChanged:
                OnTrendDirectionChanged(signal);
                break;
            case IntrinsicTimeModeType.TrendExtremeChanged:
                OnTrendExtremeChanged(signal);
                break;
        }
    }

    void OnTrendDirectionChanged(FuturesItiSignalV2ReadModel signal)
    {
        if (Volatile.Read(ref _acceptEvents) == 0
            || signal?.IntrinsicTimeMode != IntrinsicTimeModeType.TrendDirectionChanged)
        {
            return;
        }

        lock (_stateGate)
            _tradeSignals.Insert(0, signal);
        PublishTradeState();
    }

    void OnTrendExtremeChanged(FuturesItiSignalV2ReadModel signal)
    {
        if (Volatile.Read(ref _acceptEvents) == 1
            && signal?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged)
        {
            LatestTrendExtreme = signal;
        }
    }

    void OnFuturesTradeSignal(FuturesTradeSignalUpdatedNotifyEvent notification)
    {
        var signal = notification.FuturesTradeSignal;
        if (Volatile.Read(ref _acceptEvents) == 0 || signal is null)
            return;

        lock (_stateGate)
            _futuresTradeSignal = signal;
        PublishTradeState();
    }

    void PublishTradeState()
    {
        FuturesItiSignalV2ReadModel[] signals;
        FuturesTradeStatusUIViewModel status;
        lock (_stateGate)
        {
            signals = [.. _tradeSignals];
            status = BuildTradeStatus(_tradeSignals.FirstOrDefault(), _futuresTradeSignal);
        }

        TradeSignals = signals;
        TradeStatus = status;
    }

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);

    static FuturesTradeStatusUIViewModel BuildTradeStatus(
        FuturesItiSignalV2ReadModel? futuresItiSignal,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal)
    {
        var tradeStatus = "No Trade Entry";
        if (futuresItiSignal is not null)
        {
            var trendTrade = futuresItiSignal.IntrinsicTimeTrend switch
            {
                IntrinsicTimeTrendType.UpTrend => "ShortIronCondor",
                IntrinsicTimeTrendType.DownTrend => "LongIronCondor",
                _ => null
            };
            if (trendTrade is not null)
            {
                tradeStatus = futuresTradeSignal?.TradeExecuteState switch
                {
                    null => tradeStatus,
                    TradeExecuteState.Enter => $"Open {trendTrade} Trade",
                    TradeExecuteState.ExitOnTrendReversion => $"Close {trendTrade} On Trade Reversion",
                    TradeExecuteState.ExitOnEntryLimit =>
                        $"Close {trendTrade} On Trade {(futuresItiSignal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? "Below" : "Above")} Entry Limit",
                    TradeExecuteState.Hold =>
                        $"Hold {futuresItiSignal.IntrinsicTimeTrend} Trade Entry Due To Market Volatility",
                    TradeExecuteState.No => $"No {futuresItiSignal.IntrinsicTimeTrend} Trade Entry",
                    TradeExecuteState.InTrade => $"In {futuresItiSignal.IntrinsicTimeTrend} Trade",
                    TradeExecuteState.RangeBound => "Trend is RangeBound",
                    _ => tradeStatus
                };
            }
        }

        return new FuturesTradeStatusUIViewModel(
            new FuturesTradeStatusReadModel(tradeStatus, futuresTradeSignal?.TradeExecuteState));
    }

    static FuturesTradeStatusUIViewModel CreateDefaultTradeStatus()
        => new(new FuturesTradeStatusReadModel("No Trade Entry", null));

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
}
