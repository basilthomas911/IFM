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
using TomasAI.IFM.Shared.EventQueue;
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
    LatestValueAsyncChannel<TradePositionChangeSourceReadModel>? _tradePositionChannel;
    ConcurrentStack<IronCondorSpreadPathDataModel> _spreadPathQueue;
    ConcurrentEventQueue<TradePlanReadModel> _tradePlanConsoleQueue = null!;
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
        _siteId = _appRoot.GetModel<EventModel>().SiteId;
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
        await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
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
        => _appRoot.GetModel<TradeCommandModel>().ExecuteAsync(async model => {
            model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Delete Option Trade Spread Bar Data Error"));
            var optionTradeId = new OptionTradeEntityId(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId);
            await model.DeleteOptionTradeSpreadBarDataAsync(optionTradeId, _fundOrderTrade.TradeType, _valueDate.HasValue? _valueDate.Value: DateOnly.FromDateTime(DateTime.Now.Date));
        });

    /// <summary>
    /// disable market data feed listener
    /// </summary>
    public async Task DisableMarketDataFeedResetListener()
    {
        if (!_resetListenerEnabled)
            return;
        _resetListenerCancellation.Cancel();
        await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
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
        await _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model =>
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
            => _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Trade Info Error"));
                await model.GetTradeInfoAsync(_fundOrderTrades, tradeInfo => {
                    _tradeInfo = [.. tradeInfo];
                    TradeInfo = [.. tradeInfo];
                });
            });

        // load iron condor trade positions from storage
        Task LoadTradePositions()
            => _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Trade Positions Error"));
                await model.GetTradePositionsAsync(orderId, tradeId, tradePositions => {
                    _tradePositions = [.. tradePositions];
                });
            });

        // load risk free rate from market data query model
        Task LoadRiskFreeRate()
            => _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMsg) => PublishError(errorCode, errorMsg, "Loading Iron Condor Risk Free Rate Error"));
                await model.GetRiskFreeRateAsync(riskFreeRate => _riskFreeRate = riskFreeRate);
            });

        // load option trade spread bar data by position value date
        Task LoadOptionTradeSpreadBarDataByPositionValueDate()
        {
            var positionValueDate = (_optionTrade?.TradePositions?.LastOrDefault()?.ValueDate ??
                (_valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(DateTime.Now.Date))).ToDateTime(TimeOnly.MinValue);
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
           => _appRoot.GetModel<TradePlanQueryModel>().ExecuteAsync(async model => {
               var valueDate = _valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(DateTime.Now.Date);
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
      => _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model => {
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
        await _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model =>
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
        return _appRoot.GetModel<TradePlanQueryModel>().ExecuteAsync(async model => {
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
        await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model =>
            await model.GetFuturesContractAsync(
                _optionTrade.UnderlyingContractId,
                futuresContract => _futuresContract = futuresContract));

        if (_futuresContract is null)
            return;

        await _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async marketDataFeedQueryModel =>
        {
            var valueDate = _optionTrade?.TradePositions?.LastOrDefault()?.ValueDate
                ?? (_valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(DateTime.Now.Date));
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
        await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async marketDataModel =>
            await marketDataModel.GetFuturesContractAsync(
                _optionTrade.UnderlyingContractId,
                futuresContract => _futuresContract = futuresContract));

        if (_futuresContract is null || index < 0 || index >= _tradeHistory.Count)
            return;

        await _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async marketDataFeedQueryModel =>
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
            var tradeModel = _appRoot.GetModel<TradeQueryModel>();
            var tradeLimit = default(TradeLimitReadModel);
            await tradeModel.GetTradeLimitsAsync(tradeId, e => tradeLimit = e);
            var fundModel = _appRoot.GetModel<FundQueryModel>();
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
            var model = _appRoot.GetModel<TradeQueryModel>();
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
        await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) =>
                PublishError(errorCode, errorMsg, "Unable to connect to IFM servers"));
            await model.GetValueDateAsync(valueDate => _valueDate = valueDate);
        });

        OptionTradeReadModel? trade = null;
        await _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model =>
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
            =>_appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Load Value Date Error"));
                await model.GetValueDateAsync(valueDate => {
                    _valueDate = valueDate;
                });
            });

        Task DeleteSpreadDistributionJobsInProgress()
            => _appRoot.GetModel<SpreadDistributionJobModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Delete Spread Distribution Jobs In Progress Error"));
                var valueDate = _valueDate ?? DateOnly.FromDateTime(DateTime.Now);
                await model.DeleteSpreadDistributionJobsInProgressAsync(new SpreadDistributionJobEntityId(OrderId, TradeId, valueDate));
            });

        Task EnableTradeLiveFeed()
            => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
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

        Task EnableFuturesEodDataListener()
            => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Futures EOD Listener Error"));
                await model.StartFuturesEodDataEventConsumerAsync(_siteId, e =>
                {
                    CurrentFuturesEodData = e.FuturesEodData;
                    FuturesEodRevision++;
                    GenerateSpreadDistribution();
                });
            });

        async Task EnableFuturesOptionTickDataListener()
        {
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
            {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Enable Futures Option Tick Data Listener Error"));
                await model.StartFuturesOptionTickDataListenerAsync(async e => await model.ExecuteAsync(async () => await OnFuturesOptionTickDataUpdateAsync(e)));
            });

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
                        createdOn: DateTime.Now,
                        createdBy: Environment.UserName,
                        updatedOn: DateTime.Now,
                        updatedBy: Environment.UserName
                    ).SetOptionLeg(optionLeg);

                    var tradeModel = _appRoot.GetModel<TradeCommandModel>();
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
                minimumInterval: TimeSpan.FromMilliseconds(50));
            await _appRoot.GetModel<TradePositionFeedEventModel>().ExecuteAsync(async model => {
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
        Task EnableTradePlanListener()
            => _appRoot.GetModel<TradePlanEventModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Trade Plan Listener Error"));
                await model.StartTradePlanListenerAsync(o =>
                    TradePlans = [.. TradePlans.TakeLast(499), o.TradePlan]);
            });

        ///
        async Task EnableOptionTradeSpreadBarDataListener()
        {
            if (!_valueDate.HasValue) return;
            await _appRoot.GetModel<OptionTradeSpreadBarDataEventModel>().ExecuteAsync(async model => {
                model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Option Trade Spread Bar Data Listener Error"));
                await model.StartOptionTradeSpreadBarDataListenerAsync(o =>
                    LoadOptionTradeSpreadBarData(
                        orderId: o.OptionTradeSpreadBarData.OrderId,
                        tradeId: o.OptionTradeSpreadBarData.TradeId,
                        tradeType: o.OptionTradeSpreadBarData.TradeType,
                        valueDate: o.OptionTradeSpreadBarData.ValueDate,
                        startDate: DateTime.Now.AddHours(-6),
                        endDate: DateTime.Now)
                );
            });

        }

        Task UpdateDailyProfitTarget()
            => _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model => {
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
                await _appRoot.GetModel<TradeCommandModel>().ExecuteAsync(async tradeModel =>
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
             => _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model => {
                 model.OnError((errorCode, errorMessage) => PublishError(errorCode, errorMessage, "Load Current Trade History Error"));
                 await model.GetTradeHistoryAsync(_optionTrade.OrderId, tradeHistory => {
                     _tradeHistory = new (tradeHistory);
                     TradeHistory = [.. tradeHistory];
                 });
             });

        Task GenerateSpreadDistribution(double lossProbabilityFactor = 0)
            => _appRoot.GetModel<SpreadDistributionJobModel>().ExecuteAsync(async model => {
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
                            jobSubmitted: DateTime.Now,
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
            => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
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

        Task DisableFuturesEodDataListener()
            => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => await model.StopFuturesEodDataEventConsumerAsync(_siteId));

        Task DisableFuturesOptionTickDataListener()
            => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => await model.StopFuturesOptionTickDataListenerAsync());

        async ValueTask DisableTradePositionListenerAsync()
        {
            var channel = Interlocked.Exchange(ref _tradePositionChannel, null);
            if (channel is null)
                return;

            await _appRoot.GetModel<TradePositionFeedEventModel>().StopTradePositionListenerAsync();
            await channel.StopAsync();
        }

        async Task DisableTradePlanListener()
        {
            if (_tradePlanConsoleQueue == null) return;
            _tradePlanConsoleQueue?.Stop();
            await _appRoot.GetModel<TradePlanEventModel>().ExecuteAsync(async model => await model.StopTradePlanListenerAsync());
            _tradePlanConsoleQueue = null!;
        }

        Task DisableOptionTradeSpreadBarDataListener()
            => _appRoot.GetModel<OptionTradeSpreadBarDataEventModel>()
                .ExecuteAsync(async model => await model.StopOptionTradeSpreadBarDataListenerAsync());
    }

    public Task InsertOptionTradeSpreadData(decimal netForwardPrice, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) e)
       => _appRoot.GetModel<TradeCommandModel>()
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
                valueDate: _valueDate.HasValue ? _valueDate.Value : DateOnly.FromDateTime(DateTime.Now),
                lossLimit: _tradeLimits.MaxLossLimit,
                winLimit: _tradeLimits.MaxProfitLimit,
                forwardSpread: netForwardPrice,
                netSpread: Math.Abs((e.PutCreditSpread?.NetSpread ?? 0m) + (e.CallCreditSpread?.NetSpread ?? 0m)),
                createdOn: DateTime.Now,
                createdBy: string.Empty);

    /// <summary>
    /// check every minute to snapshot trade data
    /// </summary>
    async Task SnapshotTickAsync()
    {
        DateOnly? valueDate = null;
        await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model =>
            await model.GetValueDateAsync(value => valueDate = value));
        if (!valueDate.HasValue)
            return;

        if (!IsLiveFeedEnabled)
            return;

        await _appRoot.GetModel<TradeCommandModel>()
            .ExecuteAsync(async model => await model.SnapshotOptionTradeAsync(_optionTrade.OrderId, _optionTrade.TradeId));
        await WriteStatusConsole($"SnapshotOptionTrade executed for {_optionTrade.OrderId}:{_optionTrade.TradeId}");
    }

    /// <summary>
    /// check every minute to insert option trade spread bar data
    /// </summary>
    async Task SpreadBarDataTickAsync()
    {
        var valueDate = _valueDate ?? DateOnly.FromDateTime(DateTime.Now);
        OptionTradeSpreadsDataModel? spreadData = null;
        await _appRoot.GetModel<TradeQueryModel>().ExecuteAsync(async model =>
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
            barDate: DateTime.Now,
            lossLimit: spreadData.LossLimit,
            winLimit: spreadData.WinLimit,
            forwardSpread: spreadData.ForwardSpread,
            netSpread: spreadData.NetSpread);
        await _appRoot.GetModel<TradeCommandModel>()
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
        => _appRoot.GetModel<StatusConsoleModel>().ExecuteAsync(async model =>
            await model.WriteConsoleAsync(LogSourceType.Trade, errorCode, errorMessage));

    Task WriteStatusConsole(string statusMessage)
        => _appRoot.GetModel<StatusConsoleModel>().ExecuteAsync(async model =>
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

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);
}
