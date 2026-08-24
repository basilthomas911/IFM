using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

public sealed record IronCondorTradeLimitSnapshot(
    int OrderId,
    TradeLimitReadModel TradeLimit,
    decimal FundBalance);

public sealed record IronCondorPositionSnapshot(
    TradePositionEntityId Key,
    TradePositionReadModel PutSpread,
    TradePositionReadModel CallSpread,
    TradeLimitReadModel TradeLimit,
    decimal OpeningNetSpread,
    decimal FundBalance);

/// <summary>
/// Provides diagnostics for the replaceable live state consumed by the Iron Condor monitor.
/// </summary>
/// <param name="FuturesEod">Metrics for the latest-value futures EOD stream.</param>
/// <param name="TradePosition">Metrics for the latest-value trade-position stream.</param>
public sealed record IronCondorLiveStreamMetricsSnapshot(
    LatestValueChannelMetrics FuturesEod,
    LatestValueChannelMetrics TradePosition,
    OrderedBatchChannelMetrics TradePlan,
    IReadOnlyDictionary<string, LatestValueChannelMetrics> FuturesOptionTicks,
    LatestValueChannelMetrics SpreadBars);

/// <summary>
/// Describes the WinForms/WPF adapter dispatch and rendering latency observed by the monitor.
/// </summary>
public readonly record struct IronCondorUiDispatchMetricsSnapshot(
    long DispatchCount,
    TimeSpan LastDispatchDelay,
    TimeSpan MaximumDispatchDelay,
    TimeSpan LastRenderDuration,
    TimeSpan MaximumRenderDuration);

public sealed class IronCondorViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    public const int GetTradeInfoErrorCode = 6000;
    public const int GetTradePositionsErrorCode = 6001;
    public const int GetOptionTradeErrorCode = 6002;
    public const string LiveFeedOn = "LiveFeed ON";
    public const string LiveFeedOff = "LiveFeed OFF";

    IAppRoot _appRoot;
    Guid _siteId;
    FundReadModel _fund;
    FundOrderReadModel _fundOrder;
    FundOrderTradeReadModel _fundOrderTrade;
    List<FundOrderTradeReadModel> _fundOrderTrades;
    DateOnly? _valueDate;
    ICollection<FuturesContractV2ReadModel> _baseContracts;
    OptionTradeReadModel _optionTrade = null!;
    List<TradeHistoryReadModel> _tradeHistory = [];
    List<TradeInfoReadModel> _tradeInfo = [];
    FuturesContractV2ReadModel _futuresContract = null!;
    List<FuturesEodDataV2ReadModel> _futuresEodData;
    LatestValueAsyncChannel<FuturesEodDataV2ReadModel>? _futuresEodChannel;
    KeyedLatestValueAsyncChannel<string, OptionTradeTickPriceDataUpdatedEvent>? _futuresOptionTickChannels;
    LatestValueAsyncChannel<TradePositionChangeSourceReadModel>? _tradePositionChannel;
    OrderedBatchAsyncChannel<TradePlanReadModel>? _tradePlanChannel;
    LatestValueAsyncChannel<OptionTradeSpreadBarDataInsertedCompleteEvent>? _spreadBarChannel;
    ConcurrentStack<IronCondorSpreadPathDataModel> _spreadPathQueue;
    List<TradePositionReadModel> _tradePositions;
    TradeLimitReadModel _tradeLimits = null!;
    decimal _fundBalance;
    double _riskFreeRate;
    readonly TimeProvider _timeProvider;
    readonly AsyncLifecycleCoordinator _liveFeedLifecycle;
    CancellationTokenSource _resetListenerCancellation = new();
    Dictionary<string, OptionTradeLegDataReadModel> _optionLegDataMap;
    List<IronCondorSpreadPathDataModel> _spreadPaths;
    bool _liveFeedEnabled;
    Dictionary<FuturesOptionTickEntityId, string> _liveStreamsIds;
    FuturesEodDataV2ReadModel[] _futuresEodHistory = [];
    FuturesEodDataV2ReadModel? _currentFuturesEodData;
    TradeInfoReadModel[] _tradeInfoSnapshot = [];
    IronCondorTradeLimitSnapshot? _tradeLimitSnapshot;
    IronCondorPositionSnapshot? _positionSnapshot;
    OptionTradeSpreadBarUIViewModel[] _spreadBarData = [];
    TradeHistoryReadModel[] _tradeHistorySnapshot = [];
    TradePlanReadModel[] _tradePlans = [];
    PresentationError? _lastError;
    bool _isLoading;
    bool _isLoaded;
    long _positionRevision;
    long _futuresEodRevision;
    long _errorSequence;
    bool _resetListenerEnabled;
    readonly object _liveStreamMetricsGate = new();
    readonly Dictionary<string, LatestValueChannelMetrics> _futuresOptionTickMetrics = [];
    IronCondorLiveStreamMetricsSnapshot _liveStreamMetrics = new(
        default,
        default,
        default,
        new Dictionary<string, LatestValueChannelMetrics>(),
        default);
    IronCondorUiDispatchMetricsSnapshot _uiDispatchMetrics;
    long _dispatchCount;
    long _maximumDispatchDelayTicks;
    long _maximumRenderDurationTicks;

    /// <summary>
    /// create iron condor view model
    /// </summary>
    /// <param name="appRoot"></param>
    /// <param name="fundOrder"></param>
    /// <param name="fundOrderTrade"></param>
    /// <param name="valueDate"></param>
    /// <param name="baseContracts"></param>
    public IronCondorViewModel(IAppRoot appRoot, FundReadModel fund,  FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade, DateOnly? valueDate,
        ICollection<FuturesContractV2ReadModel> baseContracts,
        TimeProvider? timeProvider = null)
    {
        _appRoot = appRoot;
        _fund = fund;
        _fundOrder = fundOrder;
        _fundOrderTrade = fundOrderTrade;
        _valueDate = valueDate;
        _baseContracts = baseContracts;
        _fundOrderTrades = [];
        _fundOrderTrades.AddRange(_fundOrder.Trades);
        _tradePositions = [];
        _futuresEodData = [];
        _optionLegDataMap = [];
        _spreadPaths = [];
        _spreadPathQueue = new();
        _siteId = _appRoot.Services.CommandResponses.SiteId;
        _liveStreamsIds = [];
        _timeProvider = timeProvider ?? TimeProvider.System;
        _liveFeedLifecycle = new AsyncLifecycleCoordinator(EnableLiveFeedCoreAsync, DisableLiveFeedCoreAsync);
    }

    public IAppRoot AppRoot => _appRoot;
    public FundReadModel Fund => _fund;
    public FundOrderReadModel FundOrder => _fundOrder;
    public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade;
    public DateOnly? ValueDate => _valueDate;
    public ICollection<FuturesContractV2ReadModel> BaseContracts => _baseContracts;
    public int OrderId => _fundOrder.OrderId;
    public int TradeId => _fundOrderTrade.TradeId;
    public object[] LiveFeedLabels => new object[] { LiveFeedOff, LiveFeedOn };
    public bool IsLiveFeedEnabled
    {
        get => _liveFeedEnabled;
        private set => SetProperty(ref _liveFeedEnabled, value);
    }

    public TradeType PutSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
    public TradeType CallSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
    public OptionLegAction ShortOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction LongOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
    public OptionLegAction GetShortPutOptionLegAction(TradeType tradeType) => tradeType == TradeType.PutCreditSpread ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction GetLongPutOptionLegAction(TradeType tradeType) => tradeType == TradeType.PutCreditSpread ? OptionLegAction.Long : OptionLegAction.Short;
    public OptionLegAction GetShortCallOptionLegAction(TradeType tradeType) => tradeType == TradeType.CallCreditSpread ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction GetLongCallOptionLegAction(TradeType tradeType) => tradeType == TradeType.CallCreditSpread ? OptionLegAction.Long : OptionLegAction.Short;
    public OptionTradeReadModel? OptionTrade => _optionTrade;
    public FuturesEodDataV2ReadModel[] FuturesEodHistory
    {
        get => _futuresEodHistory;
        private set => SetProperty(ref _futuresEodHistory, value);
    }
    public FuturesEodDataV2ReadModel? CurrentFuturesEodData
    {
        get => _currentFuturesEodData;
        private set => SetProperty(ref _currentFuturesEodData, value);
    }
    public long FuturesEodRevision
    {
        get => _futuresEodRevision;
        private set => SetProperty(ref _futuresEodRevision, value);
    }
    public TradeInfoReadModel[] TradeInfo
    {
        get => _tradeInfoSnapshot;
        private set => SetProperty(ref _tradeInfoSnapshot, value);
    }
    public IronCondorTradeLimitSnapshot? TradeLimitSnapshot
    {
        get => _tradeLimitSnapshot;
        private set => SetProperty(ref _tradeLimitSnapshot, value);
    }
    public IronCondorPositionSnapshot? PositionSnapshot
    {
        get => _positionSnapshot;
        private set => SetProperty(ref _positionSnapshot, value);
    }
    public long PositionRevision
    {
        get => _positionRevision;
        private set => SetProperty(ref _positionRevision, value);
    }
    /// <summary>
    /// Gets event-rate, coalescing, queue-delay, processing-duration, and lifecycle metrics for live display streams.
    /// </summary>
    public IronCondorLiveStreamMetricsSnapshot LiveStreamMetrics
    {
        get => _liveStreamMetrics;
        private set => SetProperty(ref _liveStreamMetrics, value);
    }
    /// <summary>
    /// Gets dispatcher wait and adapter rendering latency recorded by the active UI host.
    /// </summary>
    public IronCondorUiDispatchMetricsSnapshot UiDispatchMetrics
    {
        get => _uiDispatchMetrics;
        private set => SetProperty(ref _uiDispatchMetrics, value);
    }

    /// <summary>
    /// Records one completed UI dispatch and render operation without introducing a framework dependency.
    /// </summary>
    public void RecordUiDispatch(TimeSpan dispatchDelay, TimeSpan renderDuration)
    {
        if (dispatchDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dispatchDelay));
        if (renderDuration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(renderDuration));

        UpdateMaximum(ref _maximumDispatchDelayTicks, dispatchDelay.Ticks);
        UpdateMaximum(ref _maximumRenderDurationTicks, renderDuration.Ticks);
        UiDispatchMetrics = new IronCondorUiDispatchMetricsSnapshot(
            Interlocked.Increment(ref _dispatchCount),
            dispatchDelay,
            TimeSpan.FromTicks(Interlocked.Read(ref _maximumDispatchDelayTicks)),
            renderDuration,
            TimeSpan.FromTicks(Interlocked.Read(ref _maximumRenderDurationTicks)));
    }
    public OptionTradeSpreadBarUIViewModel[] SpreadBarData
    {
        get => _spreadBarData;
        private set => SetProperty(ref _spreadBarData, value);
    }
    public TradeHistoryReadModel[] TradeHistory
    {
        get => _tradeHistorySnapshot;
        private set => SetProperty(ref _tradeHistorySnapshot, value);
    }
    public TradePlanReadModel[] TradePlans
    {
        get => _tradePlans;
        private set => SetProperty(ref _tradePlans, value);
    }
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    public async Task EnableMarketDataFeedResetListener()
    {
        if (_resetListenerEnabled)
            return;
        if (_resetListenerCancellation.IsCancellationRequested)
        {
            _resetListenerCancellation.Dispose();
            _resetListenerCancellation = new CancellationTokenSource();
        }
        var cancellationToken = _resetListenerCancellation.Token;
        await _appRoot.Services.FeedCommands.ExecuteAsync(async model =>
            await model.StartMarketDataFeedResetListenerAsync(async _ => {
               if (_liveFeedEnabled)
               {
                   await DisableLiveFeedAsync();
                   try
                   {
                       await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
                   }
                   catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                   {
                       return;
                   }
                   await EnableLiveFeedAsync();
                   await DeleteOptionTradeSpreadBarData();
               }
            }));
        _resetListenerEnabled = true;
    }

    private Task DeleteOptionTradeSpreadBarData()
        => _appRoot.Services.TradeCommands.ExecuteAsync(async model => {
            model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Delete Option Trade Spread Bar Data Error"));
            var optionTradeId = new OptionTradeEntityId(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId);
            await model.DeleteOptionTradeSpreadBarDataAsync(optionTradeId, _fundOrderTrade.TradeType, _valueDate.HasValue? _valueDate.Value: DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System)));
        });

    /// <summary>
    /// disable market data feed listener
    /// </summary>
    public async Task DisableMarketDataFeedResetListener()
    {
        if (!_resetListenerEnabled)
            return;
        _resetListenerCancellation.Cancel();
        await _appRoot.Services.FeedCommands.ExecuteAsync(async model =>
            await model.StopMarketDataFeedResetListenerAsync());
        _resetListenerEnabled = false;
    }

    /// <summary>
    /// load iron condor trade from storage
    /// </summary>
    public async Task<OptionTradeReadModel?> LoadIronCondorTrade()
    {
        if (_fundOrderTrades.Count == 0)
            return null;

        OptionTradeReadModel? trade = null;
        await _appRoot.Services.TradeQueries.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) =>
                PublishError(errorCode, errorMsg, "Loading Iron Condor Trade Error"));
            await model.GetOptionTradeAsync(
                _fundOrder.OrderId,
                _fundOrderTrade.TradeId,
                value => trade = value);
        });
        return trade;
    }

    /// <summary>
    /// load complete iron condor trade info from storage and display trade info
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public async Task LoadIronCondorTradeDetailsAsync(
        OptionTradeReadModel trade,
        int orderId,
        int tradeId)
    {
        ArgumentNullException.ThrowIfNull(trade);
        IsLoading = true;
        IsLoaded = false;
        var initialErrorSequence = Volatile.Read(ref _errorSequence);
        _optionTrade = trade;
        OnPropertyChanged(nameof(OptionTrade));
        try
        {
            await LoadFuturesEodDataHistory();
            await LoadOptionTradeSpreadBarDataByPositionValueDate();
            await LoadTradeInfo();
            await LoadTradePositions();
            await LoadRiskFreeRate();
            await LoadTradeHistory();
            await LoadTradeLimits(orderId, tradeId);
            IsLoaded = Volatile.Read(ref _errorSequence) == initialErrorSequence;
        }
        finally
        {
            IsLoading = false;
        }

        // load iron condor trade from storage
        Task LoadTradeInfo()
            => _appRoot.Services.TradeQueries.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Trade Info Error"));
                await model.GetTradeInfoAsync(_fundOrderTrades, tradeInfo => {
                    _tradeInfo = [.. tradeInfo];
                    TradeInfo = [.. tradeInfo];
                });
            });

        // load iron condor trade positions from storage
        Task LoadTradePositions()
            => _appRoot.Services.TradeQueries.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Trade Positions Error"));
                await model.GetTradePositionsAsync(orderId, tradeId, tradePositions => {
                    _tradePositions = [.. tradePositions];
                });
            });

        // load risk free rate from market data query model
        Task LoadRiskFreeRate()
            => _appRoot.Services.MarketDataQueries.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Risk Free Rate Error"));
                await model.GetRiskFreeRateAsync(riskFreeRate => _riskFreeRate = riskFreeRate);
            });

        // load option trade spread bar data by position value date
        Task LoadOptionTradeSpreadBarDataByPositionValueDate()
        {
            var positionValueDate = (_optionTrade?.TradePositions?.LastOrDefault()?.ValueDate ??
                (_valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System)))).ToDateTime(TimeOnly.MinValue);
            var startDate = positionValueDate.AddHours(10);
            return LoadOptionTradeSpreadBarData(
                       orderId,
                       tradeId,
                       tradeType: _fundOrderTrade.TradeType,
                       valueDate:  DateOnly.FromDateTime(positionValueDate),
                       startDate: positionValueDate.AddHours(10),
                       endDate: positionValueDate.AddHours(16));
        }
    }

    Task LoadIronCondorTradePlans()
           => _appRoot.Services.TradePlanQueries.ExecuteAsync(async model => {
               var valueDate = _valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System));
               model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Loading Iron Condor Trade Plans Error"));
               await model.GetTradePlansAsync(_fundOrder.OrderId, _fundOrderTrade.TradeId, valueDate, tradePlans => {
                   if (tradePlans is not null)
                       TradePlans = [.. tradePlans];
               });
           });

    /// <summary>
    /// load trade history from storage
    /// </summary>
    public Task LoadTradeHistory()
      => _appRoot.Services.TradeQueries.ExecuteAsync(async model => {
          model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Trade History Error"));
          await model.GetTradeHistoryAsync(_optionTrade.OrderId, tradeHistory =>
          {
              _tradeHistory = [.. tradeHistory];
              TradeHistory = [.. tradeHistory];
          });
      });

    /// <summary>
    /// Loads option trade spread bar data for a specific trade history entry identified by its index.
    /// </summary>
    /// <remarks>This method retrieves trade details such as order ID, trade ID, and trade type from the
    /// specified index in the trade history. It then calculates the start and end times for the trade spread bar data
    /// based on the value date of the trade.</remarks>
    /// <param name="index">The zero-based index of the trade history entry to load data for. Must be within the bounds of the trade history
    /// collection.</param>
    public Task LoadOptionTradeSpreadBarData(int index)
    {
        if (index < 0 || index >= _tradeHistory.Count)
            return Task.CompletedTask;
        var orderId = _fundOrderTrade.OrderId;
        var tradeId = _fundOrderTrade.TradeId;
        var tradeType = _fundOrderTrade.TradeType;
        var positionValueDate = _tradeHistory[index].ValueDate.ToDateTime(TimeOnly.MinValue);
        return LoadOptionTradeSpreadBarData(
                   orderId,
                   tradeId,
                   tradeType: tradeType,
                   valueDate: DateOnly.FromDateTime(positionValueDate),
                   startDate: positionValueDate.AddHours(10),
                   endDate: positionValueDate.AddHours(16));
    }

    /// <summary>
    /// load iron condor trade position
    /// </summary>
    /// <param name="index"></param>
    public async Task LoadIronCondorTradePosition(int index)
    {
        if (index < 0 || index >= _tradeHistory.Count)
            return;
        var orderId = _tradeHistory[index].OrderId;
        var tradeId = _tradeHistory[index].TradeId;
        var tradeType = _tradeHistory[index].TradeType;
        var tradeStatus = _tradeHistory[index].TradeStatus;
        var daysToExpiry = _tradeHistory[index].DaysToExpiry;
        var valueDate = _tradeHistory[index].ValueDate;
        var key = new TradePositionEntityId(orderId, tradeId, valueDate, tradeType, tradeStatus,daysToExpiry );
        await _appRoot.Services.TradeQueries.ExecuteAsync(async model =>
        {
            if (_tradeHistory is null || _tradeHistory.Count == 0)
                return;
            var openingOrderId = _tradeHistory[0].OrderId;
            var openingTradeId = _tradeHistory[0].TradeId;
            var openingTradeStatus = _tradeHistory[0].TradeStatus;
            var openingDaysToExpiry = _tradeHistory[0].DaysToExpiry;
            var openingValueDate = _tradeHistory[0].ValueDate;
            await model.GetTradePositionTradeTypesAsync(openingOrderId, openingTradeId, openingValueDate, openingDaysToExpiry, openingTradeStatus, async openingTradeTypes =>
            {
                if (openingTradeTypes?.Length == 2)
                {
                    var putSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                        openingTradeTypes.Where(e => e.StartsWith("put", StringComparison.CurrentCultureIgnoreCase)).SingleOrDefault(), out var putSpreadTradeType);
                    var callSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                        openingTradeTypes.Where(e => e.StartsWith("call", StringComparison.CurrentCultureIgnoreCase)).SingleOrDefault(), out var callSpreadTradeType);
                    var openingNetSpread = await model.GetIronCondorNetSpreadAsync(
                        openingOrderId,
                        openingTradeId,
                        putSpreadTradeType,
                        callSpreadTradeType,
                        openingValueDate,
                        openingDaysToExpiry,
                        openingTradeStatus);

                    await model.GetTradePositionTradeTypesAsync(orderId, tradeId, valueDate, daysToExpiry, tradeStatus, async tradeTypes =>
                    {
                        if (tradeTypes?.Length == 2)
                        {
                            putSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                                tradeTypes.Where(e => e.ToLower().StartsWith("put")).SingleOrDefault(), out var putSpreadTradeType);
                            callSpreadTradeTypeValue = Enum.TryParse<TradeType>(
                                tradeTypes.Where(e => e.ToLower().StartsWith("call")).SingleOrDefault(), out var callSpreadTradeType);
                            await model.GetIronCondorTradePositionsAsync(
                                orderId: orderId,
                                tradeId: tradeId,
                                valueDate: valueDate,
                                daysToExpiry: daysToExpiry,
                                tradeStatus: tradeStatus,
                                putSpreadTradeType: putSpreadTradeType,
                                callSpreadTradeType: callSpreadTradeType,
                                onViewAction: ironCondorTradePositions => PublishPosition(
                                    key,
                                    ironCondorTradePositions!,
                                    _optionTrade.TradeLimit!,
                                    openingNetSpread,
                                    _fundBalance));
                        }
                    });
                }

            });

        });

    }

    /// <summary>
    /// Loads and displays trade plans for the specified trade history entry.
    /// </summary>
    /// <remarks>This method retrieves trade plans associated with the specified trade history entry and
    /// invokes the <see cref="ShowTradePlans"/> action to display them. If an error occurs during the operation, the
    /// <see cref="ShowErrorMessage"/> method is called with the error details. Before loading new trade plans, the <see
    /// cref="ClearTradePlans"/> action is invoked to clear any existing trade plans.</remarks>
    /// <param name="index">The zero-based index of the trade history entry to load trade plans for. Must be within the bounds of the trade
    /// history collection.</param>
    public Task LoadTradePlans(int index)
    {
        if (index < 0 || index >= _tradeHistory.Count)
            return Task.CompletedTask;
        return _appRoot.Services.TradePlanQueries.ExecuteAsync(async model => {
            var orderId = _tradeHistory[index].OrderId;
            var tradeId = _tradeHistory[index].TradeId;
            var valueDate = _tradeHistory[index].ValueDate;
            model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Trade Plan Action Listener Error"));
            WriteStatusConsole($"Loading trade plans for OrderId: {orderId}, TradeId: {tradeId}, ValueDate: {valueDate}...");
            TradePlans = [];
            await model.GetTradePlansAsync(orderId, tradeId, valueDate, tradePlans => {
                    TradePlans = [.. tradePlans];
                    WriteStatusConsole($"Loaded {tradePlans?.Length ?? 0} trade plans for OrderId: {orderId}, TradeId: {tradeId}, ValueDate: {valueDate}");
            });
        });
    }

    async Task LoadFuturesEodDataHistory()
    {
       //if (_futuresEodHistoryLoaded)
       //     return;
        await _appRoot.Services.MarketDataQueries.ExecuteAsync(async model =>
            await model.GetFuturesContractAsync(
                _optionTrade.UnderlyingContractId,
                futuresContract => _futuresContract = futuresContract));

        if (_futuresContract is null)
            return;

        await _appRoot.Services.FeedQueries.ExecuteAsync(async marketDataFeedQueryModel =>
        {
            var valueDate = _optionTrade?.TradePositions?.LastOrDefault()?.ValueDate
                ?? (_valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System)));
            await marketDataFeedQueryModel.GetFuturesEodDataAsync(
                _futuresContract.ContractId,
                valueDate.AddMonths(-2),
                valueDate,
                futuresEodData =>
                {
                    if (futuresEodData is null || futuresEodData.Length == 0)
                        return;
                    CurrentFuturesEodData = futuresEodData.First();
                    FuturesEodRevision++;
                    _futuresEodData.Clear();
                    _futuresEodData.AddRange(futuresEodData);
                    FuturesEodHistory = [.. _futuresEodData.Skip(1)];
                });
        });
    }

    public async Task LoadFuturesEodData(int index)
    {
        await _appRoot.Services.MarketDataQueries.ExecuteAsync(async marketDataModel =>
            await marketDataModel.GetFuturesContractAsync(
                _optionTrade.UnderlyingContractId,
                futuresContract => _futuresContract = futuresContract));

        if (_futuresContract is null || index < 0 || index >= _tradeHistory.Count)
            return;

        await _appRoot.Services.FeedQueries.ExecuteAsync(async marketDataFeedQueryModel =>
        {
            var valueDate = _tradeHistory[index].ValueDate;
            await marketDataFeedQueryModel.GetFuturesEodDataAsync(
                _futuresContract.ContractId,
                valueDate.AddMonths(-2),
                valueDate,
                futuresEodData =>
                {
                    if (futuresEodData?.Length > 0)
                    {
                        CurrentFuturesEodData = futuresEodData.First();
                        FuturesEodRevision++;
                    }
                });
        });
    }

    Task LoadTradeLimits(int orderId, int tradeId)
        => _appRoot.ExecuteAsync(async cancellationToken => {
            cancellationToken.ThrowIfCancellationRequested();
            var tradeModel = _appRoot.Services.TradeQueries;
            var tradeLimit = default(TradeLimitReadModel);
            await tradeModel.GetTradeLimitsAsync(tradeId, e => tradeLimit = e);
            var fundModel = _appRoot.Services.FundQueries;
            await fundModel.GetFundBalanceAsync(_fundOrder.FundId, fundBalance =>
            {
                _tradeLimits = tradeLimit!;
                _fundBalance = fundBalance;
                TradeLimitSnapshot = new IronCondorTradeLimitSnapshot(orderId, tradeLimit!, fundBalance);
            });
        });

    Task LoadOptionTradeSpreadBarData(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        DateTime startDate,
        DateTime endDate)
        => _appRoot.ExecuteAsync(async cancellationToken => {
            cancellationToken.ThrowIfCancellationRequested();
            var model = _appRoot.Services.TradeQueries;
            await model.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, tradeType, valueDate, startDate, endDate,
                async optionTradeSpreadBarData => {
                    await model.GetIronCondorMDILimitAsync(orderId, tradeId, valueDate, ironCondorMDILimit =>
                    {
                        var optionTradeSpreadBarUIData = optionTradeSpreadBarData
                            .Select(e => new OptionTradeSpreadBarUIViewModel(e, ironCondorMDILimit))
                            .ToArray();
                        SpreadBarData = optionTradeSpreadBarUIData;
                    });
                });
        });

    /// <summary>
    /// return list of option contract id's
    /// </summary>
    /// <returns></returns>
    public ICollection<string> GetOptionLegContractIds()
        => _optionTrade.OptionLegs is not null
        ? [.. _optionTrade.OptionLegs.Select(e => e.ContractId)]
        : [];

    /// <summary>
    /// return list of trades with current trade order
    /// </summary>
    /// <returns></returns>
    public int GetTradeInfoCount() => _tradeInfo?.Count ?? 0;

    /// <summary>
    /// return current trade pnl value
    /// </summary>
    /// <returns></returns>
    public decimal GetTradePnl(TradePositionReadModel pcsIntraDay, TradePositionReadModel ccsIntraDay, int intraDaySign)
    {
        var eodTradePnl = _optionTrade.TradePositions is not null ? _optionTrade.TradePositions.GetEodTradePnl(): 0;
        var pcsTradePnl = pcsIntraDay is null || pcsIntraDay.TradeStatus == TradeStatus.EndOfDay ? 0 :  (intraDaySign * pcsIntraDay.TradePnl) -  pcsIntraDay.Commission;
        var ccsTradePnl = ccsIntraDay is null || ccsIntraDay.TradeStatus == TradeStatus.EndOfDay ? 0 :  (intraDaySign * ccsIntraDay.TradePnl) -  ccsIntraDay.Commission;
        return eodTradePnl + pcsTradePnl + ccsTradePnl;
    }

    public decimal GetEodTradePnl() => _optionTrade.TradePositions is not null ? _optionTrade.TradePositions.GetEodTradePnl() : 0;
    public decimal GetTradePnl() => _tradeHistory is null ? 0 : _tradeHistory.Sum(e => e.TradePnl);

    /// <summary>
    /// reload current iron condor trade when data has been changed within trade
    /// </summary>
    public async Task ReloadIronCondorTrade()
    {
        await _appRoot.Services.MarketDataQueries.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) =>
                PublishError(errorCode, errorMsg, "Unable to connect to IFM servers"));
            await model.GetValueDateAsync(valueDate => _valueDate = valueDate);
        });

        OptionTradeReadModel? trade = null;
        await _appRoot.Services.TradeQueries.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) =>
                PublishError(errorCode, errorMsg, "Reloading Iron Condor Trade Error"));
            await model.GetOptionTradeAsync(
                _fundOrder.OrderId,
                _fundOrderTrade.TradeId,
                optionTrade => trade = optionTrade);
        });
        if (trade is not null)
            await LoadIronCondorTradeDetailsAsync(
                trade,
                _fundOrder.OrderId,
                _fundOrderTrade.TradeId);
    }

    /// <summary>
    /// enable live market data feeds
    /// </summary>
    public Task EnableLiveFeedAsync(CancellationToken cancellationToken = default)
        => InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _liveFeedLifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => _liveFeedLifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisableMarketDataFeedResetListener();
        _resetListenerCancellation.Dispose();
        await _liveFeedLifecycle.DisposeAsync();
    }

    async Task EnableLiveFeedCoreAsync(CancellationToken cancellationToken)
    {
        if (_liveFeedEnabled)
            return;

        await LoadValueDate();
        cancellationToken.ThrowIfCancellationRequested();
        if (!_valueDate.HasValue)
            return;

        await EnableFuturesEodDataListener();
        await EnableFuturesOptionTickDataListener();
        await EnableTradePositionListener();
        await EnableTradePlanListener();
        await EnableOptionTradeSpreadBarDataListener();
        await EnableTradeLiveFeed();
        await UpdateDailyProfitTarget();
        return;

        Task LoadValueDate()
            =>_appRoot.Services.MarketDataQueries.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Load Value Date Error"));
                await model.GetValueDateAsync(valueDate => {
                    _valueDate = valueDate;
                });
            });

        Task DeleteSpreadDistributionJobsInProgress()
            => _appRoot.Services.SpreadDistributionJobs.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Delete Spread Distribution Jobs In Progress Error"));
                var valueDate = _valueDate ?? DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System));
                await model.DeleteSpreadDistributionJobsInProgressAsync(new SpreadDistributionJobEntityId(OrderId, TradeId, valueDate));
            });

        Task EnableTradeLiveFeed()
            => _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Trade Live Feed Error"));
                List<string> optionContractIds = _optionTrade.OptionLegs is not null
                    ? [.. _optionTrade.OptionLegs.OrderByDescending(e => e.ContractId).Select(e => e.ContractId.Trim())]
                    : [];
                foreach (var contractId in optionContractIds)
                {
                    /// get new request id for live feed...
                    var requestId = 0;
                    _liveStreamsIds.Add(new FuturesOptionTickEntityId(contractId, _valueDate!.Value), contractId);
                }
                var baseContract = _baseContracts.Where(e => e.Id.Symbol == _fundOrderTrade.BaseContractSymbol.Trim()).FirstOrDefault();
                await model.StartStreamingFuturesOptionTickDataAsync(_liveStreamsIds, baseContract!, _valueDate!.Value, _optionTrade.MaturityDate, _riskFreeRate,
                    () => { });
                await DeleteSpreadDistributionJobsInProgress();
                IsLiveFeedEnabled = true;
                _liveFeedLifecycle.RunAsync(RunPeriodicAsync);
            });

        async Task EnableFuturesEodDataListener()
        {
            if (_futuresEodChannel is not null)
                return;

            _futuresEodChannel = new LatestValueAsyncChannel<FuturesEodDataV2ReadModel>(
                OnFuturesEodDataUpdateAsync,
                minimumInterval: TimeSpan.FromMilliseconds(50),
                timeProvider: _timeProvider,
                metricsChanged: PublishFuturesEodMetrics);
            PublishFuturesEodMetrics(_futuresEodChannel.Metrics);
            await _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Futures EOD Listener Error"));
                await model.StartFuturesEodDataEventConsumerAsync(
                    _siteId,
                    e => _futuresEodChannel?.TryWrite(e.FuturesEodData));
            });

            async ValueTask OnFuturesEodDataUpdateAsync(
                FuturesEodDataV2ReadModel futuresEodData,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentFuturesEodData = futuresEodData;
                FuturesEodRevision++;
                await GenerateSpreadDistribution();
            }
        }

        async Task EnableFuturesOptionTickDataListener()
        {
            if (_futuresOptionTickChannels is not null)
                return;

            var channels = new KeyedLatestValueAsyncChannel<string, OptionTradeTickPriceDataUpdatedEvent>(
                ProcessFuturesOptionTickAsync,
                minimumInterval: TimeSpan.FromMilliseconds(50),
                timeProvider: _timeProvider,
                metricsChanged: PublishFuturesOptionTickMetrics);
            _futuresOptionTickChannels = channels;
            await _appRoot.Services.FeedCommands.ExecuteAsync(async model =>
            {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Futures Option Tick Data Listener Error"));
                await model.StartFuturesOptionTickDataListenerAsync(e =>
                {
                    if (e?.OptionTickData is not null)
                        channels.TryWrite(e.OptionTickData.ContractId, e);
                    return ValueTask.CompletedTask;
                });
            });

            async ValueTask ProcessFuturesOptionTickAsync(
                string contractId,
                OptionTradeTickPriceDataUpdatedEvent @event,
                CancellationToken channelCancellationToken)
            {
                channelCancellationToken.ThrowIfCancellationRequested();
                await OnFuturesOptionTickDataUpdateAsync(@event);
            }

            async Task OnFuturesOptionTickDataUpdateAsync(OptionTradeTickPriceDataUpdatedEvent e)
            {
                try
                {
                    if (e is null)
                        return;
                    var optionTickData = e.OptionTickData;
                    var optionLeg = _optionTrade.OptionLegs?.Where(o => o.ContractId == optionTickData.ContractId)?.SingleOrDefault();
                    if (optionLeg is null)
                        return;
                    var tradePostionKey = new TradePositionEntityId(
                        OrderId: _optionTrade.OrderId,
                        TradeId: _optionTrade.TradeId,
                        TradeType: GetTradePositionTradeType(optionLeg.OptionLegType),
                        ValueDate: _valueDate!.Value,
                        DaysToExpiry: _optionTrade.MaturityDate.DayNumber - _valueDate.Value.DayNumber,
                        TradeStatus: TradeStatus.IntraDay
                    );

                    // get option trade data key for selected option tick data...
                    var optionLegData = new OptionTradeLegDataReadModel(
                        orderId: tradePostionKey.OrderId,
                        tradeId: tradePostionKey.TradeId,
                        tradeType: tradePostionKey.TradeType,
                        valueDate: tradePostionKey.ValueDate,
                        daysToExpiry: tradePostionKey.DaysToExpiry,
                        tradeStatus: tradePostionKey.TradeStatus,
                        optionLegId: optionLeg.ContractId,
                        bidPrice: Convert.ToDecimal(optionTickData.BidPrice),
                        askPrice: Convert.ToDecimal(optionTickData.AskPrice),
                        impliedVolatility: optionTickData.ImpliedVolatility,
                        delta: optionTickData.Delta,
                        gamma: optionTickData.Gamma,
                        theta: optionTickData.Theta,
                        vega: optionTickData.Vega,
                        rho: optionTickData.Rho,
                        createdOn: DateTime.UtcNow,
                        createdBy: Environment.UserName,
                        updatedOn: DateTime.UtcNow,
                        updatedBy: Environment.UserName
                    ).SetOptionLeg(optionLeg);

                    var tradeModel = _appRoot.Services.TradeCommands;
                    if (!_optionLegDataMap.TryAdd(optionLeg.ContractId, optionLegData))
                    {
                        if (_optionLegDataMap[optionLeg.ContractId].OptionPrice != optionLegData.OptionPrice)
                        {
                            _optionLegDataMap.Remove(optionLeg.ContractId);
                            _optionLegDataMap.Add(optionLeg.ContractId, optionLegData);
                            await tradeModel.ChangeOptionLegDataAsync(
                                orderId: _optionTrade.OrderId,
                                tradeId: _optionTrade.TradeId,
                                key: tradePostionKey,
                                assetPrice: Convert.ToDecimal(optionTickData.UnderlyingPrice),
                                riskFreeRate: _riskFreeRate,
                                optionLegData: optionLegData
                            );
                        }
                    }
                    else
                    {
                        await tradeModel.ChangeOptionLegDataAsync(
                            orderId: _optionTrade.OrderId,
                            tradeId: _optionTrade.TradeId,
                            key: tradePostionKey,
                            assetPrice: Convert.ToDecimal(optionTickData.UnderlyingPrice),
                            riskFreeRate: _riskFreeRate,
                            optionLegData: optionLegData
                        );
                    }
                }
                catch { }
            }
        }

        async Task EnableTradePositionListener()
        {
            if (_tradePositionChannel is not null)
                return;

            _tradePositionChannel = new LatestValueAsyncChannel<TradePositionChangeSourceReadModel>(
                OnTradePositionUpdateAsync,
                minimumInterval: TimeSpan.FromMilliseconds(50),
                timeProvider: _timeProvider,
                metricsChanged: PublishTradePositionMetrics);
            PublishTradePositionMetrics(_tradePositionChannel.Metrics);
            await _appRoot.Services.TradePositionEvents.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Trade Position Listener Error"));
                await model.StartTradePositionListenerAsync(e => {
                    _tradePositionChannel?.TryWrite(new TradePositionChangeSourceReadModel(e.PutTradePosition!, e.CallTradePosition!, e.TradePositionChangeSource, e.OptionLegId));
                });
            });
            return;

            async ValueTask OnTradePositionUpdateAsync(
                TradePositionChangeSourceReadModel e,
                CancellationToken cancellationToken)
            {
                try
                {
                    if (e.PutTradePosition?.OptionLegData is null || e.PutTradePosition?.EntityId.TradeStatus != TradeStatus.IntraDay
                        || e.CallTradePosition?.OptionLegData is null || e.CallTradePosition?.EntityId.TradeStatus != TradeStatus.IntraDay)
                        return;
                    var putCreditSpread = _optionTrade.TradePositions!.Get(e.PutTradePosition.EntityId.FromTradeType(GetTradePositionTradeType(OptionType.Put)));
                    var callCreditSpread = _optionTrade.TradePositions!.Get(e.CallTradePosition.EntityId.FromTradeType(GetTradePositionTradeType(OptionType.Call)));
                    if ((putCreditSpread?.OptionLegData?.Length ?? 0) != 2 || (callCreditSpread?.OptionLegData?.Length ?? 0) != 2)
                    {
                        await ReloadIronCondorTrade();
                        return;
                    }
                    else
                    {
                        switch (e.TradePositionChangeSource)
                        {
                            case TradePositionChangeSourceType.PutCreditSpreadLeg:
                                _optionTrade.TradePositions?.Set(e.PutTradePosition);
                                break;
                            case TradePositionChangeSourceType.CallCreditSpreadLeg:
                                _optionTrade.TradePositions?.Set(e.CallTradePosition);
                                break;
                            case TradePositionChangeSourceType.SpreadDistributionStatistics:
                                _optionTrade.TradePositions?.Set(e.PutTradePosition);
                                _optionTrade.TradePositions?.Set(e.CallTradePosition);
                                break;
                            default:
                                return;
                        }
                        putCreditSpread = e.PutTradePosition;
                        callCreditSpread = e.CallTradePosition;
                        var spreads = (PutCreditSpread: putCreditSpread, CallCreditSpread: callCreditSpread);
                        var netSpreadPrice = Math.Abs(Math.Abs(putCreditSpread?.NetSpread ?? 0m) + Math.Abs(callCreditSpread?.NetSpread ?? 0m));
                        netSpreadPrice = netSpreadPrice < 0.0m ? 0.0m : netSpreadPrice;

                        PublishPosition(
                            e.PutTradePosition.EntityId,
                            spreads,
                            _optionTrade.TradeLimit!,
                            netSpreadPrice,
                            _fundBalance);
                        var netForwardPrice = Math.Abs(putCreditSpread?.ForwardPrice ?? 0m) + Math.Abs(callCreditSpread?.ForwardPrice ?? 0m);
                        netForwardPrice = netForwardPrice < 0.0m ? 0.0m : netForwardPrice;
                        await InsertOptionTradeSpreadData(netForwardPrice, spreads);
                        await LoadCurrentTradeHistory();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                }
            }

        }

        /// <summary>
        /// start trade plan listener
        /// </summary>
        async Task EnableTradePlanListener()
        {
            if (_tradePlanChannel is not null)
                return;

            _tradePlanChannel = new OrderedBatchAsyncChannel<TradePlanReadModel>(
                PublishTradePlanBatchAsync,
                capacity: 256,
                maximumBatchSize: 32,
                readerRetryCount: 3,
                readerRetryDelay: TimeSpan.FromMilliseconds(50),
                timeProvider: _timeProvider,
                metricsChanged: PublishTradePlanMetrics);
            PublishTradePlanMetrics(_tradePlanChannel.Metrics);
            await _appRoot.Services.TradePlanEvents.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Trade Plan Listener Error"));
                await model.StartTradePlanListenerAsync(async e =>
                {
                    var tradePlan = e.TradePlan;
                    if (!_valueDate.HasValue || tradePlan.OrderId != OrderId || tradePlan.TradeId != TradeId || tradePlan.ValueDate != _valueDate.Value)
                        return;
                    var channel = _tradePlanChannel;
                    if (channel is not null)
                        await channel.WriteAsync(tradePlan);
                });
            });

            ValueTask PublishTradePlanBatchAsync(
                IReadOnlyList<TradePlanReadModel> batch,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TradePlans = [.. TradePlans.Concat(batch).TakeLast(500)];
                return ValueTask.CompletedTask;
            }
        }

        ///
        async Task EnableOptionTradeSpreadBarDataListener()
        {
            if (!_valueDate.HasValue || _spreadBarChannel is not null)
                return;

            var channel = new LatestValueAsyncChannel<OptionTradeSpreadBarDataInsertedCompleteEvent>(
                ProcessSpreadBarRefreshAsync,
                minimumInterval: TimeSpan.FromMilliseconds(100),
                timeProvider: _timeProvider,
                metricsChanged: PublishSpreadBarMetrics);
            _spreadBarChannel = channel;
            PublishSpreadBarMetrics(channel.Metrics);
            await _appRoot.Services.SpreadBarEvents.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Option Trade Spread Bar Data Listener Error"));
                await model.StartOptionTradeSpreadBarDataListenerAsync(@event =>
                {
                    var spreadBar = @event.OptionTradeSpreadBarData;
                    if (spreadBar.OrderId == OrderId
                        && spreadBar.TradeId == TradeId
                        && spreadBar.ValueDate == _valueDate.Value)
                        channel.TryWrite(@event);
                    return ValueTask.CompletedTask;
                });
            });

            async ValueTask ProcessSpreadBarRefreshAsync(
                OptionTradeSpreadBarDataInsertedCompleteEvent @event,
                CancellationToken channelCancellationToken)
            {
                channelCancellationToken.ThrowIfCancellationRequested();
                var spreadBar = @event.OptionTradeSpreadBarData;
                await LoadOptionTradeSpreadBarData(
                    orderId: spreadBar.OrderId,
                    tradeId: spreadBar.TradeId,
                    tradeType: spreadBar.TradeType,
                    valueDate: spreadBar.ValueDate,
                    startDate: EasternTime.GetNow(TimeProvider.System).AddHours(-6),
                    endDate: EasternTime.GetNow(TimeProvider.System));
            }
        }

        Task UpdateDailyProfitTarget()
            => _appRoot.Services.MarketDataQueries.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Trade Days Error"));
                var tradingDays = 0;
                var maxTradingDays = 0;
                await model.GetTradingDaysAsync(
                    _optionTrade.TradeDate,
                    _valueDate!.Value,
                    MarketType.Futures,
                    CurrencyType.USD,
                    value => tradingDays = value);
                await model.GetTradingDaysAsync(
                    _optionTrade.TradeDate,
                    _optionTrade.MaturityDate,
                    MarketType.Futures,
                    CurrencyType.USD,
                    value => maxTradingDays = value);
                await _appRoot.Services.TradeCommands.ExecuteAsync(async tradeModel =>
                {
                    tradeModel.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Updating Trade Limit Daily Profit Target Error"));
                    await tradeModel.UpdateTradeLimitDailyProfitTargetAsync(
                        _fundOrder.OrderId,
                        _fundOrderTrade.TradeId,
                        tradingDays,
                        maxTradingDays);
                });
            });

        Task LoadCurrentTradeHistory()
             => _appRoot.Services.TradeQueries.ExecuteAsync(async model => {
                 model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Load Current Trade History Error"));
                 await model.GetTradeHistoryAsync(_optionTrade.OrderId, tradeHistory => {
                     _tradeHistory = new (tradeHistory);
                     TradeHistory = [.. tradeHistory];
                 });
             });

        Task GenerateSpreadDistribution(double lossProbabilityFactor = 0)
            => _appRoot.Services.SpreadDistributionJobs.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => WriteStatusConsole(errorCode, errorMsg));
                await model.IsSpreadDistributionJobInProgressAsync(OrderId, TradeId, async jobInProgress => {
                    if (!jobInProgress)
                    {
                        await model.SubmitSpreadDistributionJobAsync(new SpreadDistributionJobReadModel
                        (
                            orderId: _optionTrade.OrderId,
                            tradeId: _optionTrade.TradeId,
                            tradeType: _optionTrade.TradeType,
                            tradeStatus: TradeStatus.IntraDay,
                            valueDate: _valueDate!.Value,
                            daysToExpiry: _optionTrade.MaturityDate.DayNumber - _valueDate!.Value.DayNumber,
                            jobSubmitted: DateTime.UtcNow,
                            jobStatus: SpreadDistributionJobStatus.InProgress,
                            jobCompleted: null,
                            jobFailed: null,
                            inProgress: true,
                            lossProbabilityFactor: lossProbabilityFactor
                        ));
                    }
                });
         });

        TradeType GetTradePositionTradeType(OptionType optionType)
             => _optionTrade.TradeType switch
             {
                 TradeType.ShortIronCondor => optionType == OptionType.Put ? TradeType.PutCreditSpread : TradeType.CallCreditSpread,
                 TradeType.LongIronCondor => optionType == OptionType.Put ? TradeType.PutDebitSpread : TradeType.CallDebitSpread,
                 _ => throw new NotImplementedException()
             };

    }

    /// <summary>
    /// disable live market data feeds
    /// </summary>
    public Task DisableLiveFeedAsync(CancellationToken cancellationToken = default)
        => StopAsync(cancellationToken);

    async Task DisableLiveFeedCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsLiveFeedEnabled = false;
        await DisableOptionTradeSpreadBarDataListener();
        await DisableTradePlanListener();
        await DisableFuturesEodDataListener();
        await DisableFuturesOptionTickDataListener();
        await DisableTradePositionListenerAsync();
        await DisableTradeLiveFeed();
        return;

        Task DisableTradeLiveFeed()
            => _appRoot.Services.FeedCommands.ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Disable Trade Live Feed Error"));
                if (_optionTrade is not null)
                {
                    foreach (var e in _liveStreamsIds)
                    {
                        var streamId = e.Key;
                        var contractId = e.Value;
                        await model.StopStreamingFuturesOptionTickDataAsync(streamId, contractId);
                    }
                    _liveStreamsIds = new();
                }
                IsLiveFeedEnabled = false;
            });

        async Task DisableFuturesEodDataListener()
        {
            var channel = Interlocked.Exchange(ref _futuresEodChannel, null);
            try
            {
                await _appRoot.Services.FeedCommands
                    .ExecuteAsync(async model => await model.StopFuturesEodDataEventConsumerAsync(_siteId));
            }
            finally
            {
                if (channel is not null)
                    await channel.StopAsync();
            }
        }

        async Task DisableFuturesOptionTickDataListener()
        {
            var channels = Interlocked.Exchange(ref _futuresOptionTickChannels, null);
            try
            {
                await _appRoot.Services.FeedCommands
                    .ExecuteAsync(async model => await model.StopFuturesOptionTickDataListenerAsync());
            }
            finally
            {
                if (channels is not null)
                    await channels.StopAsync();
            }
        }

        async ValueTask DisableTradePositionListenerAsync()
        {
            var channel = Interlocked.Exchange(ref _tradePositionChannel, null);
            if (channel is null)
                return;

            try
            {
                await _appRoot.Services.TradePositionEvents.StopTradePositionListenerAsync();
            }
            finally
            {
                await channel.StopAsync();
            }
        }

        async Task DisableTradePlanListener()
        {
            var channel = Interlocked.Exchange(ref _tradePlanChannel, null);
            if (channel is null)
                return;
            try
            {
                await _appRoot.Services.TradePlanEvents
                    .ExecuteAsync(async model => await model.StopTradePlanListenerAsync());
            }
            finally
            {
                await channel.StopAsync();
            }
        }

        async Task DisableOptionTradeSpreadBarDataListener()
        {
            var channel = Interlocked.Exchange(ref _spreadBarChannel, null);
            try
            {
                await _appRoot.Services.SpreadBarEvents
                    .ExecuteAsync(async model => await model.StopOptionTradeSpreadBarDataListenerAsync());
            }
            finally
            {
                if (channel is not null)
                    await channel.StopAsync();
            }
        }
    }

    public Task InsertOptionTradeSpreadData(decimal netForwardPrice, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) e)
       => _appRoot.Services.TradeCommands
            .ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Insert Option Trade Spread Data Error"));
                var optionTradeSpreadData = GetOptionTradeSpreadData(netForwardPrice, e);
                await model.InsertOptionTradeSpreadDataAsync(optionTradeSpreadData);
            });

    OptionTradeSpreadsDataModel GetOptionTradeSpreadData(decimal netForwardPrice, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) e)
            => new OptionTradeSpreadsDataModel(
                sequenceId: 0,
                orderId: _optionTrade.OrderId,
                tradeId: _optionTrade.TradeId,
                tradeType: _optionTrade.TradeType,
                valueDate: _valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System)),
                lossLimit: _tradeLimits.MaxLossLimit,
                winLimit: _tradeLimits.MaxProfitLimit,
                forwardSpread: netForwardPrice,
                netSpread: Math.Abs((e.PutCreditSpread?.NetSpread ?? 0m) + (e.CallCreditSpread?.NetSpread ?? 0m)),
                createdOn: DateTime.UtcNow,
                createdBy: string.Empty);

    /// <summary>
    /// check every minute to snapshot trade data
    /// </summary>
    async Task SnapshotTickAsync()
    {
        DateOnly? valueDate = null;
        await _appRoot.Services.MarketDataQueries.ExecuteAsync(async model =>
            await model.GetValueDateAsync(value => valueDate = value));
        if (!valueDate.HasValue)
            return;

        if (!IsLiveFeedEnabled)
            return;

        await _appRoot.Services.TradeCommands
            .ExecuteAsync(async model => await model.SnapshotOptionTradeAsync(_optionTrade.OrderId, _optionTrade.TradeId));
        await WriteStatusConsole($"SnapshotOptionTrade executed for {_optionTrade.OrderId}:{_optionTrade.TradeId}");
    }

    /// <summary>
    /// check every minute to insert option trade spread bar data
    /// </summary>
    async Task SpreadBarDataTickAsync()
    {
        var valueDate = _valueDate ?? DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System));
        OptionTradeSpreadsDataModel? spreadData = null;
        await _appRoot.Services.TradeQueries.ExecuteAsync(async model =>
            await model.GetOptionTradeSpreadDataAsync(
                _optionTrade.OrderId,
                _optionTrade.TradeId,
                _optionTrade.TradeType,
                valueDate,
                value => spreadData = value));
        if (spreadData is null)
            return;

        var optionTradeSpreadBarData = new OptionTradeSpreadBarsDataModel(
            orderId: spreadData.OrderId,
            tradeId: spreadData.TradeId,
            tradeType: spreadData.TradeType,
            valueDate: valueDate,
            barDate: DateTime.UtcNow,
            lossLimit: spreadData.LossLimit,
            winLimit: spreadData.WinLimit,
            forwardSpread: spreadData.ForwardSpread,
            netSpread: spreadData.NetSpread);
        await _appRoot.Services.TradeCommands
            .ExecuteAsync(async model => await model.InsertOptionTradeSpreadBarDataAsync(optionTradeSpreadBarData));
    }

    async Task RunPeriodicAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SnapshotTickAsync();
            await SpreadBarDataTickAsync();
            await Task.Delay(TimeSpan.FromSeconds(15), _timeProvider, cancellationToken);
        }
    }

    Task WriteStatusConsole(int errorCode, string errorMessage)
        => _appRoot.Services.StatusConsole.ExecuteAsync(async model =>
            await model.WriteConsoleAsync(LogSourceType.Trade, errorCode, errorMessage));

    Task WriteStatusConsole(string statusMessage)
        => _appRoot.Services.StatusConsole.ExecuteAsync(async model =>
            await model.WriteConsoleAsync(LogSourceType.Trade, statusMessage));

    void PublishPosition(
        TradePositionEntityId key,
        (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) positions,
        TradeLimitReadModel tradeLimit,
        decimal openingNetSpread,
        decimal fundBalance)
    {
        PositionSnapshot = new IronCondorPositionSnapshot(
            key,
            positions.PutCreditSpread,
            positions.CallCreditSpread,
            tradeLimit,
            openingNetSpread,
            fundBalance);
        PositionRevision++;
    }

    void PublishFuturesEodMetrics(LatestValueChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
            LiveStreamMetrics = LiveStreamMetrics with { FuturesEod = metrics };
    }

    void PublishTradePositionMetrics(LatestValueChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
            LiveStreamMetrics = LiveStreamMetrics with { TradePosition = metrics };
    }

    void PublishTradePlanMetrics(OrderedBatchChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
            LiveStreamMetrics = LiveStreamMetrics with { TradePlan = metrics };
    }

    void PublishFuturesOptionTickMetrics(string contractId, LatestValueChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
        {
            _futuresOptionTickMetrics[contractId] = metrics;
            LiveStreamMetrics = LiveStreamMetrics with
            {
                FuturesOptionTicks = new Dictionary<string, LatestValueChannelMetrics>(
                    _futuresOptionTickMetrics)
            };
        }
    }

    void PublishSpreadBarMetrics(LatestValueChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
            LiveStreamMetrics = LiveStreamMetrics with { SpreadBars = metrics };
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

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);
}
