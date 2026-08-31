using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

/// <summary>Provides per-contract diagnostics for replaceable option-tick order-entry state.</summary>
public sealed record IronCondorTradeOrderLiveStreamMetricsSnapshot(
    IReadOnlyDictionary<string, LatestValueChannelMetrics> FuturesOptionTicks);

/// <summary>
/// Represents a view model for managing and executing iron condor trade orders.
/// </summary>
/// <remarks>This class provides properties and methods to handle various aspects of iron condor trades, including
/// setting trade dates, calculating trade values, managing live feed data, and more. It is designed to work with a
/// trading application infrastructure, utilizing models and actions to perform operations related to option trading
/// strategies.</remarks>
public sealed class IronCondorTradeOrderViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _riskMarginLifecycle;
    readonly AsyncLifecycleCoordinator _liveFeedLifecycle;
    readonly TimeProvider _timeProvider;
    readonly IReferenceDataService _referenceDataService;
    readonly bool _historicalReadOnly;
    readonly OptionTradeReadModel? _historicalTrade;
    readonly decimal _historicalFundBalance;
    readonly object _liveStreamMetricsGate = new();
    readonly Dictionary<string, LatestValueChannelMetrics> _futuresOptionTickMetrics = [];
    IAppRoot _appRoot;
    OptionTradeReadModel _ironCondorTrade = null!;
    OptionTradeReadModel _parentTrade = null!;
    DateOnly _valueDate;
    int _fundId;
    FuturesContractV2ReadModel _baseContract;
    FundOrderReadModel _fundOrder;
    FundOrderTradeReadModel _fundOrderTrade;
    OrderActionType _orderActionType;
    DefaultFuturesContractDefinitionsUiModel _defaultFuturesContractDefinitions = null!;
    double _riskFreeRate;
    int _putSpreadStrikeWidth;
    int _callSpreadStrikeWidth;
    Dictionary<(OptionLegAction OptionLegAction, OptionType OptionType), OptionTradeLegReadModel> _optionLegMap = [];
    Dictionary<(DateOnly ValueDate, TradeType TradeType, TradeStatus TradeStatus, OptionLegAction OptionLegAction, OptionType OptionType), OptionTradeLegDataReadModel> _optionLegDataMap = [];
    Dictionary<(DateOnly ValueDate, TradeType TradeType, TradeStatus TradeStatus), TradePositionReadModel> _tradePositionMap = [];
    Dictionary<(OptionLegAction OptionLegAction, OptionType OptionType), decimal> _optionPriceMap = [];
    RiskPositionType _riskPositionType;
    Guid _liveFeedQuoteId;
    decimal _fundBalance;
    decimal _orderPrice;
    decimal _assetPrice;
    decimal? _fundMaxProfit;
    object[] _strikePrices = [];
    int _selectedStrikePriceIndex = -1;
    FuturesEodDataV2ReadModel? _liveFeedValues;
    PresentationError? _lastError;
    bool _isLoaded;
    bool _isLoading;
    long _liveFeedRevision;
    long _errorSequence;
    string _localSymbol = string.Empty;
    KeyedLatestValueAsyncChannel<string, FuturesOptionTickDataV2ReadModel>? _futuresOptionTickChannels;
    IronCondorTradeOrderLiveStreamMetricsSnapshot _liveStreamMetrics = new(
        new Dictionary<string, LatestValueChannelMetrics>());

    /// <summary>
    /// Initializes a new instance of the <see cref="IronCondorTradeOrderViewModel"/> class with the specified
    /// parameters.
    /// </summary>
    /// <param name="appRoot">The application root interface providing access to application-wide services and resources.</param>
    /// <param name="valueDate">The date representing the value date for the trade order.</param>
    /// <param name="fundId">The identifier of the fund associated with the trade order.</param>
    /// <param name="baseContract">The base futures contract view model related to the trade order.</param>
    /// <param name="fundOrder">The fund order view model that this trade order is part of.</param>
    /// <param name="fundOrderTrade">The fund order trade view model specifying the trade details.</param>
    /// <param name="orderActionType">The type of action to be performed on the order, such as buy or sell.</param>
    /// <exception cref="InvalidOperationException">Thrown if the <paramref name="fundOrderTrade"/> has a trade type that is not supported by this view model.</exception>
    public IronCondorTradeOrderViewModel(
        IAppRoot appRoot,
        DateOnly valueDate,
        int fundId,
        FuturesContractV2ReadModel baseContract,
        FundOrderReadModel fundOrder,
        FundOrderTradeReadModel fundOrderTrade,
        OrderActionType orderActionType,
        IReferenceDataService referenceDataService,
        TimeProvider? timeProvider = null,
        bool historicalReadOnly = false,
        OptionTradeReadModel? historicalTrade = null,
        decimal historicalFundBalance = 0m)
    {
        switch(fundOrderTrade.TradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                break;
            default:
                throw new InvalidOperationException($"IronCondorTradeOrderViewModel.Constructor: invalid trade type '{fundOrderTrade.TradeType}'");
        }
        if (historicalReadOnly && historicalTrade is null)
            throw new ArgumentNullException(
                nameof(historicalTrade),
                "A hydrated TradeDb option trade is required for historical read-only display.");
        if (historicalTrade is not null
            && (historicalTrade.OrderId != fundOrderTrade.OrderId
                || historicalTrade.TradeId != fundOrderTrade.TradeId))
            throw new ArgumentException(
                $"Historical TradeDb identity {historicalTrade.OrderId}:{historicalTrade.TradeId} does not match composition {fundOrderTrade.OrderId}:{fundOrderTrade.TradeId}.",
                nameof(historicalTrade));
        _appRoot = appRoot;
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        _valueDate = valueDate;
        _fundId = fundId;
        _baseContract = baseContract;
        _fundOrder = fundOrder;
        _fundOrderTrade = fundOrderTrade;
        _orderActionType = orderActionType;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _historicalReadOnly = historicalReadOnly;
        _historicalTrade = historicalTrade;
        _historicalFundBalance = historicalFundBalance;
        _putSpreadStrikeWidth = 30; // change to get strike width from reference data...
        _callSpreadStrikeWidth = 15; // change to get strike width from reference data...
        _liveFeedQuoteId = Guid.Empty;
        _riskMarginLifecycle = new AsyncLifecycleCoordinator(StartFundRiskMarginConsumerCoreAsync, StopFundRiskMarginConsumerCoreAsync);
        _liveFeedLifecycle = new AsyncLifecycleCoordinator(StartLiveFeedCoreAsync, StopLiveFeedCoreAsync);
    }

    public IAppRoot AppRoot => _appRoot;
    public bool IsHistoricalReadOnly => _historicalReadOnly;
    public DateOnly ValueDate => _valueDate;
    public DateOnly TradeDate => _ironCondorTrade.TradeDate;
    public DateOnly MaturityDate => _ironCondorTrade.MaturityDate;
    public TradeType TradeType => _ironCondorTrade?.TradeType ?? TradeType.Unknown;
    public int FundId => _fundId;
    public FuturesContractV2ReadModel BaseContract => _baseContract;
    public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade;
    public DefaultFuturesContractDefinitionsUiModel DefaultFuturesContractDefinitions => _defaultFuturesContractDefinitions;
    public double RiskFreeRate => _riskFreeRate;
    public RiskPositionType RiskPositionType
    {
        get => _riskPositionType;
        private set => SetProperty(ref _riskPositionType, value);
    }
    public OptionTradeReadModel IronCondorTrade => _ironCondorTrade;
    public OptionTradeLegReadModel[] OptionLegs => _ironCondorTrade?.OptionLegs ?? [];
    public OptionTradeLegDataReadModel[] OptionLegData => _ironCondorTrade?.TradePositions?.SelectMany(e => e.OptionLegData)?.ToArray() ?? [];
    public TradePositionReadModel[] TradePosition => _ironCondorTrade?.TradePositions ?? [];
    public OptionTradeReadModel ParentTrade => _parentTrade;
    public OrderActionType OrderActionType => _orderActionType;
    public decimal FundBalance
    {
        get => _fundBalance;
        set => SetProperty(ref _fundBalance, value);
    }
    public decimal OrderPrice
    {
        get => _orderPrice;
        set => SetProperty(ref _orderPrice, value);
    }
    public int OrderQuantity => _ironCondorTrade?.OptionLegs?.Sum(e => e.Quantity) / _ironCondorTrade?.OptionLegs?.Length ?? 0;
    public int ParentTradeOrderQuantity => _parentTrade?.OptionLegs?.Sum(e => e.Quantity) / _ironCondorTrade?.OptionLegs?.Length ?? 0;

    public decimal OrderAmount => _ironCondorTrade?.TradePositions?.Get(this.PutSpreadTradeType, this.TradeStatus)?.TradeValue
        + _ironCondorTrade?.TradePositions?.Get(this.CallSpreadTradeType, this.TradeStatus)?.TradeValue ?? 0m;

    public decimal TradeCommission => OrderQuantity * 1.42m * (_ironCondorTrade?.OptionLegs?.Length ?? 0) * -1.0m;
    public decimal TotalAmount => OrderAmount + TradeCommission;
    public int DaysToExpiry => _ironCondorTrade?.TradePositions?.Get(this.PutSpreadTradeType, this.TradeStatus)?.EntityId.DaysToExpiry ?? 0;

    public object[] StrikePrices
    {
        get => _strikePrices;
        private set => SetProperty(ref _strikePrices, value);
    }
    public int SelectedStrikePriceIndex
    {
        get => _selectedStrikePriceIndex;
        private set => SetProperty(ref _selectedStrikePriceIndex, value);
    }
    public decimal AssetPrice
    {
        get => _assetPrice;
        private set => SetProperty(ref _assetPrice, value);
    }
    public FuturesEodDataV2ReadModel? LiveFeedValues
    {
        get => _liveFeedValues;
        private set => SetProperty(ref _liveFeedValues, value);
    }
    public long LiveFeedRevision
    {
        get => _liveFeedRevision;
        private set => SetProperty(ref _liveFeedRevision, value);
    }
    public IronCondorTradeOrderLiveStreamMetricsSnapshot LiveStreamMetrics
    {
        get => _liveStreamMetrics;
        private set => SetProperty(ref _liveStreamMetrics, value);
    }
    public decimal? FundMaxProfit
    {
        get => _fundMaxProfit;
        private set => SetProperty(ref _fundMaxProfit, value);
    }
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public TradeType PutSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
    public TradeType ParentPutSpreadTradeType => _parentTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
    public TradeType CallSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
    public TradeType ParentCallSpreadTradeType => _parentTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
    public TradeStatus TradeStatus => _orderActionType == OrderActionType.Open ? TradeStatus.Open : TradeStatus.Close;
    public OptionLegAction ShortOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction LongOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

    public OptionLegAction OptionLeg1Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction OptionLeg2Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
    public OptionLegAction OptionLeg3Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction OptionLeg4Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

    public OptionLegAction ParentOptionLeg1Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction ParentOptionLeg2Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
    public OptionLegAction ParentOptionLeg3Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
    public OptionLegAction ParentOptionLeg4Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

    public int NearestPutStrike { get; set; }
    public int NearestCallStrike { get; set; }
    public double OTMPutProbability { get; set; }
    public double OTMCallProbability { get; set; }
    public string LocalSymbol
    {
        get => _localSymbol;
        set => SetProperty(ref _localSymbol, value ?? string.Empty);
    }

    /// <summary>
    /// Loads the iron condor trade orders by initializing necessary data and executing required queries.
    /// </summary>
    /// <remarks>This method retrieves default futures contract definitions and loads additional data required
    /// for processing iron condor trades. It handles errors by displaying an error message if the loading of default
    /// futures contract definitions fails.</remarks>
    public async Task LoadIronCondorTradeOrders()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        IsLoaded = false;
        try
        {
            if (_historicalReadOnly)
            {
                LoadHistoricalTradeOrderData();
                IsLoaded = true;
                PublishLoadedState();
                return;
            }
            _defaultFuturesContractDefinitions =
                (await _referenceDataService.GetDefaultFuturesContractDefinitionsAsync())
                .RequireValue();
            await LoadForwardDelta();
            await LoadRiskFreeRate();
            await LoadIronCondorTradeOrderData();
            IsLoaded = true;
            PublishLoadedState();
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Loading Iron Condor Trade Order Error");
            throw;
        }
        catch (UiOperationException exception)
        {
            PublishError(
                exception.ErrorCode,
                exception.Message,
                "Loading Iron Condor Trade Order Error");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    void LoadHistoricalTradeOrderData()
    {
        _ironCondorTrade = _historicalTrade!;
        FundBalance = _historicalFundBalance;
        StrikePrices = _ironCondorTrade.OptionLegs?
            .Select(leg => (object)leg.StrikePrice)
            .Distinct()
            .OrderBy(strike => strike)
            .ToArray() ?? [];
        SelectedStrikePriceIndex = StrikePrices.Length == 0 ? -1 : StrikePrices.Length / 2;
        MapHistoricalOptionLeg();
        MapOptionLegData();
        AssetPrice = GetTradePosition(PutSpreadTradeType, TradeStatus)?.AssetPrice
            ?? GetTradePosition(CallSpreadTradeType, TradeStatus)?.AssetPrice
            ?? 0m;
    }

    void PublishLoadedState()
    {
        OnPropertyChanged(nameof(TradeDate));
        OnPropertyChanged(nameof(MaturityDate));
        OnPropertyChanged(nameof(IronCondorTrade));
        OnPropertyChanged(nameof(OptionLegs));
        OnPropertyChanged(nameof(OptionLegData));
        OnPropertyChanged(nameof(TradePosition));
        OnPropertyChanged(nameof(OrderActionType));
    }

    void MapHistoricalOptionLeg()
    {
        _optionLegMap = [];
        foreach (var optionLeg in _ironCondorTrade.OptionLegs ?? [])
            _optionLegMap.TryAdd(
                (optionLeg.OptionLegAction, optionLeg.OptionLegType),
                optionLeg);
    }

    /// <summary>
    /// Initiates the process to set the maximum profit for a fund.
    /// </summary>
    /// <remarks>This method triggers the execution of a series of asynchronous operations to update the
    /// fund's maximum profit. It handles errors by displaying an error message if any issues occur during the
    /// process.</remarks>
    public async Task SetFundMaxProfit()
    {
        ThrowIfHistoricalReadOnly();
        try
        {
            await _riskMarginLifecycle.InitializeAsync(CancellationToken.None);
            await _appRoot.Services.FundCommands.ExecuteObservableAsync(async model =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                await model.GenerateFundRiskMarginAsync(
                    _fundOrder,
                    Domain.MarketData.Analytics.Shared.TimeFrameType.FifteenSeconds);
            });
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Setting Fund Max Profit Error");
            throw;
        }
    }

    Task StartFundRiskMarginConsumerCoreAsync(CancellationToken cancellationToken)
        => _appRoot.Services.FundCommands.ExecuteAsync(async model =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await model.StartFundRiskMarginEventConsumerAsync(
                HandleFundMaxProfitChanged,
                HandleFundMaxProfitFailed);
        });

    void HandleFundMaxProfitChanged(FundMaxProfitGeneratedCompleteEvent @event)
    {
        FundMaxProfit = @event.FundMaxProfit.FundMaxProfit;
        _ = StopFundRiskMarginEventConsumer();
    }

    void HandleFundMaxProfitFailed(FundMaxProfitGeneratedFailEvent @event)
    {
        PublishError(@event.ErrorCode, @event.ErrorMessage, "Setting Risk Margin Failed");
        _ = StopFundRiskMarginEventConsumer();
    }

    /// <summary>
    /// Stops the consumer responsible for processing fund risk margin events.
    /// </summary>
    /// <remarks>This method halts the operation of the fund risk margin event consumer, preventing it from
    /// processing further events. Ensure that stopping the consumer is appropriate for the application's current state,
    /// as it will cease handling incoming events.</remarks>
    public Task StopFundRiskMarginEventConsumer()
         => _riskMarginLifecycle.StopAsync(CancellationToken.None);

    Task StopFundRiskMarginConsumerCoreAsync(CancellationToken cancellationToken)
         => _appRoot.Services.FundCommands.ExecuteAsync(async model => {
             cancellationToken.ThrowIfCancellationRequested();
             await model.StopFundRiskMarginEventConsumerAsync();
         });

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _liveFeedLifecycle.StopAsync(cancellationToken);
        await _riskMarginLifecycle.StopAsync(cancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        await _liveFeedLifecycle.DisposeAsync();
        await _riskMarginLifecycle.DisposeAsync();
    }

    /// <summary>
    /// Retrieves the intraday profit and loss (PnL) for the current trade.
    /// </summary>
    /// <remarks>This method queries the trade history for the specified order and calculates the sum of PnL
    /// for trades with a status of <see cref="TradeStatus.IntraDay"/>.</remarks>
    public async Task<decimal> GetIntraDayPnl()
    {
        try
        {
            List<TradeHistoryReadModel> history = [];
            await _appRoot.Services.TradeQueries.ExecuteObservableAsync(
                async model => history = await model.GetTradeHistoryAsync(_ironCondorTrade.OrderId));
            return history.Where(entry => entry.TradeStatus == TradeStatus.IntraDay).Sum(entry => entry.TradePnl);
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Loading Intraday P&L Error");
            throw;
        }
    }

    /// <summary>
    /// Calculates the commission for a given trade position.
    /// </summary>
    /// <param name="spd">The trade position view model containing option leg data.</param>
    /// <returns>The calculated commission as a decimal value.</returns>
    public decimal Commission(TradePositionReadModel spd)
    {
        var optionLegCount = spd.OptionLegData.Length;
        var optionQuantity = spd.OptionLegData.Sum(e => e.OptionLeg?.Quantity ?? 0) / optionLegCount;
        return optionQuantity * 1.42m * optionLegCount;
    }

    /// <summary>
    /// Sets the trade date for the current iron condor trade.
    /// </summary>
    /// <param name="tradeDate">The date to be set as the trade date.</param>
    public void SetTradeDate(DateOnly tradeDate)
    {
        _ironCondorTrade = _ironCondorTrade with { TradeDate = tradeDate };
        OnPropertyChanged(nameof(TradeDate));
        OnPropertyChanged(nameof(IronCondorTrade));
    }

    /// <summary>
    /// Sets the maturity date for the iron condor trade.
    /// </summary>
    /// <param name="maturityDate">The new maturity date to be set for the trade.</param>
    public void SetMaturityDate(DateOnly maturityDate)
    {
        _ironCondorTrade = _ironCondorTrade with { MaturityDate = maturityDate };
        OnPropertyChanged(nameof(MaturityDate));
        OnPropertyChanged(nameof(DaysToExpiry));
        OnPropertyChanged(nameof(IronCondorTrade));
    }

    /// <summary>
    /// Updates the days to expiry for trade positions within the iron condor trade.
    /// </summary>
    /// <remarks>This method calculates the number of days to expiry for both put and call spread trade
    /// positions based on the maturity date of the iron condor trade and the current value date. It then updates the
    /// trade positions with the calculated days to expiry.</remarks>
    public void SetDaysToExpiry()
    {
        var tradePosition = _ironCondorTrade?.TradePositions?.Get(PutSpreadTradeType, TradeStatus, ValueDate);
        if (tradePosition is not null)
        {
            tradePosition = tradePosition with { DaysToExpiry = _ironCondorTrade?.MaturityDate.DayNumber ?? 0 - this.ValueDate.DayNumber };
            _ironCondorTrade?.TradePositions?.Set(tradePosition);
        }
        tradePosition = _ironCondorTrade?.TradePositions?.Get(CallSpreadTradeType, TradeStatus, ValueDate);
        if (tradePosition is not null)
        {
            tradePosition = tradePosition with { DaysToExpiry = (_ironCondorTrade?.MaturityDate.DayNumber ?? 0 - this.ValueDate.DayNumber) };
            _ironCondorTrade?.TradePositions?.Set(tradePosition);
        }
    }

    public void SetTradeStatus(OrderActionType orderActionType)
    {
        var tradeStatus = orderActionType switch
        {
            OrderActionType.Open => TradeStatus.Open,
            OrderActionType.Close => TradeStatus.Close,
            _ => throw new NotImplementedException()
        };
        var oldTradePosition = _ironCondorTrade?.TradePositions?.Get(PutSpreadTradeType, TradeStatus, ValueDate);
        if (oldTradePosition is not null)
        {
            var newTradePosition = oldTradePosition with { TradeStatus = tradeStatus };
            foreach (var old in newTradePosition.OptionLegData)
                newTradePosition.OptionLegData.Set(old.OptionLegId, old with { TradeStatus = tradeStatus });
            _ironCondorTrade?.TradePositions?.Set(oldTradePosition, newTradePosition);
        }
        oldTradePosition = _ironCondorTrade?.TradePositions?.Get(CallSpreadTradeType, TradeStatus, ValueDate);
        if (oldTradePosition is not null)
        {
            var newTradePosition = oldTradePosition with { TradeStatus = tradeStatus };
            foreach (var old in newTradePosition.OptionLegData)
                newTradePosition.OptionLegData.Set(old.OptionLegId, old with { TradeStatus = tradeStatus });
            _ironCondorTrade?.TradePositions?.Set(oldTradePosition, newTradePosition);
        }
        _orderActionType = orderActionType;
        MapOptionLegData();
    }

    /// <summary>
    /// Sets the trade limits for maximum loss, minimum profit target, and daily profit target.
    /// </summary>
    /// <remarks>This method updates the trade limits for the current trading strategy. Ensure that all
    /// parameters are positive values to avoid invalid configurations.</remarks>
    /// <param name="maxLoss">The maximum allowable loss for a trade. Must be a positive decimal value.</param>
    /// <param name="minProfitTarget">The minimum profit target for a trade. Must be a positive decimal value.</param>
    /// <param name="dailyProfitTarget">The daily profit target for trades. Must be a positive decimal value.</param>
    public void SetTradeLimit(decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget)
        => _ironCondorTrade = _ironCondorTrade.SetTradeLimit(maxLoss, minProfitTarget, dailyProfitTarget);

    /// <summary>
    /// Sets the strike price for the put spread.
    /// </summary>
    /// <param name="strikePrice">The desired strike price for the put spread.</param>
    public int SetPutSpreadStrike(int strikePrice)
        => strikePrice - _putSpreadStrikeWidth;

    /// <summary>
    /// Sets the call spread strike price by adding a predefined width to the specified strike price.
    /// </summary>
    /// <param name="strikePrice">The base strike price to which the call spread width is added.</param>
    /// <returns>The calculated call spread strike price.</returns>
    public int SetCallSpreadStrike(int strikePrice)
        => strikePrice + _callSpreadStrikeWidth;

    /// <summary>
    /// Sets the action type for the order.
    /// </summary>
    /// <param name="orderActionType">The type of action to be set for the order. This value determines the operation to be performed on the order.</param>
    public void SetOrderAction(OrderActionType orderActionType)
    {
        if (_orderActionType == orderActionType)
            return;
        _orderActionType = orderActionType;
        OnPropertyChanged(nameof(OrderActionType));
        OnPropertyChanged(nameof(TradeStatus));
    }

    /// <summary>
    /// Determines the appropriate order action based on the trade type of the iron condor trade.
    /// </summary>
    /// <returns><see cref="OrderAction.Sell"/> if the trade type is <see cref="TradeType.ShortIronCondor"/>; <see
    /// cref="OrderAction.Buy"/> if the trade type is <see cref="TradeType.LongIronCondor"/>.</returns>
    /// <exception cref="NotImplementedException">Thrown if the trade type is not recognized.</exception>
    public OrderAction GetOrderAction()
        => _ironCondorTrade.TradeType switch
        {
            TradeType.ShortIronCondor => OrderAction.Sell,
            TradeType.LongIronCondor => OrderAction.Buy,
            _ => throw new NotImplementedException()
        };

    Task LoadForwardDelta()
        => _appRoot.Services.FeedQueries.ExecuteObservableAsync(
            model => model.GetFuturesRiskPositionTypeAsync(
                _valueDate,
                _fundOrderTrade.TradeType,
                value => RiskPositionType = value.RiskPositionType));

    Task LoadRiskFreeRate()
        => _appRoot.Services.MarketDataQueries.ExecuteObservableAsync(
            model => model.GetRiskFreeRateAsync(value => _riskFreeRate = value));

    async Task LoadIronCondorTradeOrderData()
    {
        var strikePrices = await LoadStrikePrices();

        await _appRoot.Services.TradeQueries.ExecuteObservableAsync(async tradeModel =>
        {
            var orderId = _fundOrderTrade.OrderId;
            var tradeId = _fundOrderTrade.TradeId;
            OptionTradeReadModel? optionTrade = null;
            await tradeModel.GetOptionTradeAsync(orderId, tradeId, value => optionTrade = value);

            _ironCondorTrade = optionTrade ?? CreateIronCondorTrade(TradeStatus);
            MapOptionLeg();
            MapOptionLegData();
            MapOptionPriceFromTradeReference(_fundOrderTrade.TradeAction, _fundOrderTrade.Reference);

            await _appRoot.Services.FundQueries.ExecuteObservableAsync(async fundModel =>
            {
                await fundModel.GetFundBalanceAsync(_fundId, fundBalance =>
                {
                    FundBalance = fundBalance;
                    StrikePrices = strikePrices;
                    SelectedStrikePriceIndex = strikePrices.Length / 2;
                });
            });

            await _appRoot.Services.FeedQueries.ExecuteObservableAsync(async marketDataFeedModel =>
            {
                await marketDataFeedModel.GetFuturesEodDataAsync(_baseContract.ContractId, _valueDate, futuresEodData =>
                {
                    if (futuresEodData is not null)
                    {
                        LoadBaseValues(futuresEodData);
                        AssetPrice = Convert.ToDecimal(futuresEodData.ClosePrice);
                    }
                });
            });
        });

        void MapOptionLeg()
        {
            _optionLegMap = [];
            var optionLeg = _ironCondorTrade?.OptionLegs?.Get(OptionLegAction.Long, OptionType.Put);
            if (optionLeg is not null)
                _optionLegMap.Add((OptionLegAction.Long, OptionType.Put), optionLeg);
            optionLeg = _ironCondorTrade?.OptionLegs?.Get(OptionLegAction.Short, OptionType.Put);
            if (optionLeg is not null)
                _optionLegMap.Add((OptionLegAction.Short, OptionType.Put), optionLeg);
            optionLeg = _ironCondorTrade?.OptionLegs?.Get(OptionLegAction.Long, OptionType.Call);
            if (optionLeg is not null)
                _optionLegMap.Add((OptionLegAction.Long, OptionType.Call), optionLeg);
            optionLeg = _ironCondorTrade?.OptionLegs?.Get(OptionLegAction.Short, OptionType.Call);
            if (optionLeg is not null)
                _optionLegMap.Add((OptionLegAction.Short, OptionType.Call), optionLeg);
        }

    }

    void MapOptionLegData()
    {
        _optionLegDataMap = [];
        _tradePositionMap = [];
        if (_ironCondorTrade?.TradePositions is not null)
            foreach (var e in _ironCondorTrade?.TradePositions!)
            {
                if (!_tradePositionMap.ContainsKey((e.EntityId.ValueDate, e.EntityId.TradeType, e.EntityId.TradeStatus)))
                    _tradePositionMap.Add((e.EntityId.ValueDate, e.EntityId.TradeType, e.EntityId.TradeStatus), e);
                foreach (var o in e.OptionLegData)
                    if (!_optionLegDataMap.ContainsKey((e.EntityId.ValueDate, e.EntityId.TradeType, e.EntityId.TradeStatus, o.OptionLeg!.OptionLegAction, o.OptionLeg.OptionLegType)))
                        _optionLegDataMap.Add(
                            key: (e.EntityId.ValueDate, e.EntityId.TradeType, e.EntityId.TradeStatus, o.OptionLeg.OptionLegAction, o.OptionLeg.OptionLegType),
                            value: o);
            }
    }

    /// <summary>
    /// Removes a trade from a fund order.
    /// </summary>
    /// <remarks>This method executes asynchronously and removes the specified trade from the associated fund
    /// order. Ensure that the <paramref name="fundOrderTradeId"/> is valid and corresponds to an existing
    /// trade.</remarks>
    /// <param name="fundOrderTradeId">The identifier of the trade to be removed from the fund order. Cannot be null.</param>
    public async Task RemoveTradeFromFundOrder(FundOrderTradeId fundOrderTradeId)
    {
        ThrowIfHistoricalReadOnly();
        try
        {
            await _appRoot.Services.FundCommands.ExecuteObservableAsync(
                async model => _ = await model.RemoveTradeFromFundOrderAsync(fundOrderTradeId));
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Removing Trade From Fund Order Error");
            throw;
        }
    }

    /// <summary>
    /// Submits a trade order for execution and updates the trade state.
    /// </summary>
    /// <remarks>This method updates the trade state to indicate that the order has been placed and handles
    /// the addition of manual trade fills if required. It also ensures that any new futures option contracts are added
    /// before placing the order.</remarks>
    /// <param name="tradeOrder">The trade order to be submitted, containing details such as fund ID and order quantity.</param>
    /// <returns>The placed-order command correlation identifier.</returns>
    public async Task<Guid> SubmitOrder(TradeOrderReadModel tradeOrder)
    {
        ThrowIfHistoricalReadOnly();
        // get manual trade fills if not sending to broker for trade fills...
        _ironCondorTrade = _ironCondorTrade with
        {
            TradeState = TradeState.OrderPlaced,
            IsPrimaryTrade = _fundOrderTrade.PrimaryTrade
        };

        // check if trade fills need to be added manually...
        AddManualTradeFills();
        //AddTradeTicketLegs();

        var commandId = Guid.Empty;
        try
        {
            // populate options contracts if they do not exist...
            await AddNewFuturesOptionContracts();

            // place order...
            await _appRoot.Services.TradeCommands.ExecuteObservableAsync(
                async model => commandId = await model.PlaceOrderAsync(tradeOrder, _ironCondorTrade));
            return commandId;
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Submit Order Error");
            throw;
        }

        void AddManualTradeFills()
        {
            if (tradeOrder.TradeFillType == TradeFillType.Manual)
            {
                _ironCondorTrade = _ironCondorTrade
                    .AddTradeFills( GetManualTradeFills(tradeOrder.FundId, tradeOrder.OrderQuantity) )
                    with { TradeState = tradeOrder.OrderActionType == OrderActionType.Open
                        ? TradeState.TradeToOpen
                        : TradeState.TradeToClose };
            }
        }

        Task AddNewFuturesOptionContracts()
        {
            List<FuturesOptionContractReadModel> contracts = [];
            string[] contractIds = [.. _ironCondorTrade!.OptionLegs!.Select(e => e.ContractId)];
            foreach (var contractId in contractIds.Select(e => new FuturesOptionContractIdReadModel(e)))
            {
                var futuresOptionContract = GetFuturesOptionContract(contractId.StrikePrice, contractId.OptionType, contractId.ContractMonth);
                contracts.Add(futuresOptionContract);
            }
            return _appRoot
                .Services.MarketDataCommands
                .ExecuteObservableAsync(
                    model => model.AddFuturesOptionContractsAsync(_valueDate.Year, [.. contracts], () => { }));
        }

    }

    public async Task TurnLiveFeedOn()
    {
        ThrowIfHistoricalReadOnly();
        try
        {
            await _liveFeedLifecycle.InitializeAsync(CancellationToken.None);
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Start Streaming Option Tick Data Error");
            throw;
        }
    }

    async Task StartLiveFeedCoreAsync(CancellationToken cancellationToken)
    {
        if (_liveFeedQuoteId  != Guid.Empty) return;

        // save futures option contracts...
        _liveFeedQuoteId = Guid.NewGuid();

        var contracts = new List<FuturesOptionContractReadModel>();
        foreach (var contractId in _optionLegMap.Values.Select(e => new FuturesOptionContractIdReadModel(e.ContractId)))
            contracts.Add(GetFuturesOptionContract(contractId.StrikePrice, contractId.OptionType, contractId.ContractMonth));

        var newContracts = contracts.ToArray();
        await _appRoot
            .Services.MarketDataCommands
            .ExecuteObservableAsync(model => model.AddFuturesOptionContractsAsync(
                _valueDate.Year,
                newContracts,
                () => { }));
        cancellationToken.ThrowIfCancellationRequested();
        await StartStreamingFuturesOptionTickData(newContracts);

        async Task StartStreamingFuturesOptionTickData(
            FuturesOptionContractReadModel[] futuresOptionContracts)
        {
            var channels = new KeyedLatestValueAsyncChannel<string, FuturesOptionTickDataV2ReadModel>(
                ProcessFuturesOptionTickAsync,
                minimumInterval: TimeSpan.FromMilliseconds(50),
                timeProvider: _timeProvider,
                metricsChanged: PublishFuturesOptionTickMetrics);
            _futuresOptionTickChannels = channels;
            var feedIds = futuresOptionContracts.ToDictionary(
                contract => new FuturesOptionTickEntityId(
                    contract.ContractId,
                    _valueDate),
                contract => contract.ContractId);
            await _appRoot.Services.FeedCommands.ExecuteObservableAsync(async model =>
            {
                await model.StartFuturesOptionTickDataListenerAsync(
                    e =>
                    {
                        if (e?.OptionTickData is not null)
                            channels.TryWrite(e.OptionTickData.ContractId, e.OptionTickData);
                        return ValueTask.CompletedTask;
                    });
                await model.StartStreamingFuturesOptionTickDataAsync(
                    feedIds,
                    _baseContract,
                    _valueDate,
                    _ironCondorTrade.MaturityDate,
                    _riskFreeRate,
                    () => { });
            });

            async ValueTask ProcessFuturesOptionTickAsync(
                string contractId,
                FuturesOptionTickDataV2ReadModel tick,
                CancellationToken channelCancellationToken)
            {
                channelCancellationToken.ThrowIfCancellationRequested();
                await SetLiveFeedTickData(tick);
            }
        }

         async Task SetLiveFeedTickData(FuturesOptionTickDataV2ReadModel futuresOptionTickData)
         {
             try
             {
                 await _appRoot.Services.FeedQueries.ExecuteObservableAsync(
                     model => model.GetFuturesEodDataAsync(
                         _baseContract.ContractId,
                         _valueDate,
                         futuresEodData => SetLiveFeedTickDataValues(futuresEodData, futuresOptionTickData)));
             }
             catch (UiServiceOperationException exception)
             {
                 PublishError(exception, "Set Live Feed Tick Data Error");
                 throw;
             }
         }
    }

    void SetLiveFeedTickDataValues(
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesOptionTickDataV2ReadModel futuresOptionTickData)
    {
        var assetPrice = futuresEodData.ClosePrice;
        var daysToExpiry = _ironCondorTrade.MaturityDate.DayNumber - _ironCondorTrade.TradeDate.DayNumber;
        var timeValue = daysToExpiry / 365.0;
        var pcs = GetTradePosition(this.PutSpreadTradeType, this.TradeStatus);
        pcs = pcs! with { AssetPrice = Convert.ToDecimal(assetPrice) };
        _ironCondorTrade?.TradePositions?.Set(pcs);

        var ccs = GetTradePosition(this.CallSpreadTradeType, this.TradeStatus);
        ccs = ccs! with { AssetPrice = Convert.ToDecimal(assetPrice) };
        _ironCondorTrade?.TradePositions?.Set(ccs);

        _ = futuresOptionTickData switch
        {
            _ when IsShortPutOptionQuote() => SetShortPutOptionLegData(),
            _ when IsLongPutOptionQuote() => SetLongPutOptionLegData(),
            _ when IsShortCallOptionQuote() => SetShortCallOptionLegData(),
            _ when IsLongCallOptionQuote() => SetLongCallOptionLegData(),
            _ => false
        };
        AssetPrice = Convert.ToDecimal(futuresEodData.ClosePrice);
        LiveFeedValues = futuresEodData;
        LiveFeedRevision++;
        return;

        bool IsShortPutOptionQuote()
        {
            var shortPutOptionContract = GetFuturesOptionContract(
                    strikePrice: Convert.ToDouble(GetOptionLeg(ShortOptionLegAction, OptionType.Put).StrikePrice),
                    futuresOptionType: OptionType.Put,
                    contractMonth: _ironCondorTrade!.MaturityDate);
            return futuresOptionTickData.ContractId == shortPutOptionContract.ContractId;
        }

        bool SetShortPutOptionLegData()
        {
            var putOptionLegData = GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put)
                with
            {
                BidPrice = Convert.ToDecimal(futuresOptionTickData.BidPrice),
                AskPrice = Convert.ToDecimal(futuresOptionTickData.AskPrice)
            };
            OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);
            UpdatePutCreditSpreadLiveFeedValues();
            return true;
        }

        bool IsLongPutOptionQuote()
        {
            var longPutOptionContract = GetFuturesOptionContract(
                strikePrice: Convert.ToDouble(GetOptionLeg(LongOptionLegAction, OptionType.Put).StrikePrice),
                futuresOptionType: OptionType.Put,
                contractMonth: _ironCondorTrade!.MaturityDate);
            return futuresOptionTickData.ContractId == longPutOptionContract.ContractId;
        }

        bool SetLongPutOptionLegData()
        {
            var putOptionLegData = GetOptionLegData(PutSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Put)
                     with
            {
                BidPrice = Convert.ToDecimal(futuresOptionTickData.BidPrice),
                AskPrice = Convert.ToDecimal(futuresOptionTickData.AskPrice)
            };
            OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);
            UpdatePutCreditSpreadLiveFeedValues();
            return true;
        }

        bool IsShortCallOptionQuote()
        {
            var shortCallOptionContract = GetFuturesOptionContract(
                            strikePrice: Convert.ToDouble(_ironCondorTrade?.OptionLegs?.Get(ShortOptionLegAction, OptionType.Call)?.StrikePrice ?? 0),
                            futuresOptionType: OptionType.Call,
                            contractMonth: _ironCondorTrade!.MaturityDate);
            return futuresOptionTickData.ContractId == shortCallOptionContract.ContractId;
        }

        bool SetShortCallOptionLegData()
        {
            var callOptionLegData = GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call)
                         with
            {
                BidPrice = Convert.ToDecimal(futuresOptionTickData.BidPrice),
                AskPrice = Convert.ToDecimal(futuresOptionTickData.AskPrice)
            };
            OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);
            UpdateCallCreditSpreadLiveFeedValues();
            return true;
        }

        bool IsLongCallOptionQuote()
        {
            var longCallOptiononContract = GetFuturesOptionContract(
                           strikePrice: Convert.ToDouble(_ironCondorTrade?.OptionLegs?.Get(LongOptionLegAction, OptionType.Call)?.StrikePrice ?? 0),
                           futuresOptionType: OptionType.Call,
                           contractMonth: _ironCondorTrade!.MaturityDate);
            return futuresOptionTickData.ContractId == longCallOptiononContract.ContractId;
        }

        bool SetLongCallOptionLegData()
        {
            var callOptionLegData = GetOptionLegData(CallSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Call)
                with
            {
                BidPrice = Convert.ToDecimal(futuresOptionTickData.BidPrice),
                AskPrice = Convert.ToDecimal(futuresOptionTickData.AskPrice)
            };
            OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);
            UpdateCallCreditSpreadLiveFeedValues();
            return true;
        }
    }

    public async Task TurnLiveFeedOff()
    {
        try
        {
            await _liveFeedLifecycle.StopAsync(CancellationToken.None);
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Stop Streaming Option Tick Data Error");
            throw;
        }
    }

    async Task StopLiveFeedCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channels = Interlocked.Exchange(ref _futuresOptionTickChannels, null);
        try
        {
            if (_liveFeedQuoteId == Guid.Empty)
                return;

            await _appRoot.Services.FeedCommands.ExecuteObservableAsync(async model =>
            {
                await model.StopFuturesOptionTickDataListenerAsync();
                foreach (var contractId in _optionLegMap.Values
                             .Select(optionLeg => optionLeg.ContractId)
                             .Distinct())
                {
                    await model.StopStreamingFuturesOptionTickDataAsync(
                        new FuturesOptionTickEntityId(contractId, _valueDate),
                        contractId);
                }
                _liveFeedQuoteId = Guid.Empty;
            });
        }
        finally
        {
            if (channels is not null)
                await channels.StopAsync();
        }
    }

    public void CalculateTradeValues()
    {
        // update put spread trade values...
        var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
        pcs = pcs! with {
            Commission = Commission(pcs),
            TradePnl = Commission(pcs) * -1,
            RiskFreeRate = _riskFreeRate
        };
        foreach (var e in pcs.OptionLegData)
        {
            var og = _appRoot.Services.OptionPricing.CalculateGreeks(
                _valueDate,
                _ironCondorTrade.MaturityDate,
                OptionTypeName.Put,
                Convert.ToDouble(pcs.AssetPrice),
                Convert.ToDouble(e.OptionLeg?.StrikePrice ?? 0),
                Convert.ToDouble(e.OptionPrice),
                _riskFreeRate);
            if (!og.Success) continue;
            pcs.OptionLegData.Set(e.OptionLegId, e with
            {
                ImpliedVolatility = og.ImpliedVolatility,
                Delta = og.Delta,
                Gamma = og.Gamma,
                Theta = og.Theta,
                Vega = og.Vega,
                Rho = og.Rho
            });
        }
        var optionLegData = GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put);
        if ((optionLegData?.Delta ?? 0.0) != 0.0)
            pcs = pcs with { OTMProbability = 1 - Math.Abs(optionLegData?.Delta ?? 0) };
        _ironCondorTrade?.TradePositions?.Set(pcs);

        // update call spread values...
        var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
        ccs = ccs! with
        {
            Commission = Commission(ccs),
            TradePnl = Commission(pcs) * -1,
            RiskFreeRate = _riskFreeRate
        };
        foreach (var e in ccs.OptionLegData)
        {
            var og = _appRoot.Services.OptionPricing.CalculateGreeks(
                _valueDate,
                _ironCondorTrade.MaturityDate,
                OptionTypeName.Call,
                Convert.ToDouble(ccs.AssetPrice),
                Convert.ToDouble(e.OptionLeg?.StrikePrice ?? 0),
                Convert.ToDouble(e.OptionPrice),
                _riskFreeRate);
            if (!og.Success)
                continue;
            ccs.OptionLegData.Set(e.OptionLegId, e with {
                ImpliedVolatility = og.ImpliedVolatility,
                Delta = og.Delta,
                Gamma = og.Gamma,
                Theta = og.Theta,
                Vega = og.Vega,
                Rho = og.Rho });
        }
        optionLegData = GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call);
        if ((optionLegData?.Delta ?? 0.0) != 0.0)
            ccs = ccs with { OTMProbability = (1 - Math.Abs(optionLegData?.Delta ?? 0)) };
        _ironCondorTrade?.TradePositions?.Set(ccs);
    }

    public void UpdateOptionLegMap()
    {
        foreach (var e in _ironCondorTrade.OptionLegs!)
            SetOptionLeg(e.OptionLegAction, e.OptionLegType, e);
    }

    public void UpdatePutCreditSpreadLiveFeedValues()
    {
        var tradeType = PutSpreadTradeType;
        var pcs = GetTradePosition(tradeType, TradeStatus);
        if (pcs is null)
            return;
        pcs = pcs with
        {
            NetSpread = GetNetSpreadValue(
              shortBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).BidPrice),
              shortAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).AskPrice),
              longBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Put).BidPrice),
              longAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Put).AskPrice))
        };
        var tradeTypeLimit = _ironCondorTrade.TradeTypeLimits?.Get(tradeType);
        if (tradeTypeLimit is not null)
        {
            var maxLossLimit = pcs.NetSpread * 2;
            tradeTypeLimit = tradeTypeLimit with
            {
                MaxLossLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit : maxLossLimit / 8.0m,
                MinProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0m : maxLossLimit,
                MaxProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0m : maxLossLimit,
            };
            _ironCondorTrade?.TradeTypeLimits?.Set(tradeType, tradeTypeLimit);
        }
        var optionLeg = GetOptionLeg(ShortOptionLegAction, OptionType.Put);
        pcs = pcs with { TradeValue = optionLeg.Quantity != 0 ? (pcs.NetSpread * optionLeg.Quantity * 50) : 0 };
        _ironCondorTrade?.TradePositions?.Set(pcs);
        SetTradePositionMap(tradeType, TradeStatus, pcs);
        UpdateTradeLimitValues();
        CalculateTradeValues();
    }

    public void UpdateCallCreditSpreadLiveFeedValues()
    {
        var tradeType = CallSpreadTradeType;
        var ccs = GetTradePosition(tradeType, TradeStatus);
        if (ccs == null) return;
        ccs = ccs with
        {
            NetSpread = GetNetSpreadValue(
              shortBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).BidPrice),
              shortAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).AskPrice),
              longBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Call).BidPrice),
              longAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Call).AskPrice))
        };
        var tradeTypeLimit = _ironCondorTrade?.TradeTypeLimits?.Get(tradeType);
        if (tradeTypeLimit is not null)
        {
            var maxLossLimit = ccs.NetSpread * 2;
            tradeTypeLimit = tradeTypeLimit with
            {
                MaxLossLimit = _ironCondorTrade!.TradeType == TradeType.ShortIronCondor ? maxLossLimit : maxLossLimit / 8.0m,
                MinProfitLimit = _ironCondorTrade!.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0m : maxLossLimit,
                MaxProfitLimit = _ironCondorTrade!.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0m : maxLossLimit,
            };
            _ironCondorTrade?.TradeTypeLimits?.Set(tradeType, tradeTypeLimit);
        }
        var optionLeg = GetOptionLeg(ShortOptionLegAction, OptionType.Call);
        ccs = ccs with { TradeValue = optionLeg.Quantity != 0 ? (ccs.NetSpread * optionLeg.Quantity * 50) : 0 };
        _ironCondorTrade?.TradePositions?.Set(ccs);
        SetTradePositionMap(tradeType, TradeStatus, ccs);
        UpdateTradeLimitValues();
        CalculateTradeValues();
    }

    public void UpdateTradeLimitValues()
    {
        var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
        if (pcs is null) return;
        var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
        if (ccs is null) return;
        _ironCondorTrade.SetTradeLimit(
            riskMargin: _ironCondorTrade?.TradeLimit?.RiskMargin ?? 0m,
            maxProfit: pcs.TradeValue + ccs.TradeValue,
            maxLoss: FundBalance * 0.02m  * -1m,
            maxReturn: (_ironCondorTrade?.TradeLimit?.RiskMargin ?? 0m) == 0m ? 0.0m : (pcs.TradeValue + ccs.TradeValue) / (_ironCondorTrade?.TradeLimit?.RiskMargin ?? 1m),
            maxLossLimit: (_ironCondorTrade?.TradeTypeLimits?.Get(PutSpreadTradeType)?.MaxLossLimit  ?? 0m) + (_ironCondorTrade?.TradeTypeLimits?.Get(CallSpreadTradeType)?.MaxLossLimit ?? 0m),
            minProfitLimit: (_ironCondorTrade?.TradeTypeLimits?.Get(PutSpreadTradeType)?.MinProfitLimit ?? 0m) + (_ironCondorTrade?.TradeTypeLimits?.Get(CallSpreadTradeType)?.MinProfitLimit ?? 0m),
            maxProfitLimit: (_ironCondorTrade?.TradeTypeLimits?.Get(PutSpreadTradeType)?.MaxProfitLimit ?? 0m) + (_ironCondorTrade?.TradeTypeLimits?.Get(CallSpreadTradeType)?.MaxProfitLimit ?? 0m),
            minProfitTarget: (0.50m * _ironCondorTrade?.TradeLimit?.MaxProfit ?? 0m) + (2 * TradeCommission),
            dailyProfitTarget: DaysToExpiry <= 0 ? 0m : ((_ironCondorTrade?.TradeLimit?.MaxProfit ?? 0m) / DaysToExpiry) + (2 * TradeCommission));
    }

    public async Task<object[]> LoadStrikePrices()
    {
        var definition = (await _referenceDataService.GetFuturesOptionStrikePriceDefinitionsAsync())
            .RequireValue();

        object[] strikePrices = [];
        await _appRoot.Services.MarketDataQueries.ExecuteObservableAsync(async marketDataModel =>
        {
            await marketDataModel.GetCurrentFuturesEodDataAsync(_valueDate, futuresEodData =>
            {
                if (futuresEodData is null)
                    return;

                var strikePrice = (int)((int)futuresEodData.ClosePrice / 5) * 5;
                var minStrike = strikePrice - 1000;
                var maxStrike = strikePrice + 1000;
                strikePrices = GetStrikePriceRange(minStrike, maxStrike, definition.Increment).ToArray();
            });
        });
        return strikePrices;

        IEnumerable<object> GetStrikePriceRange(int minStrikePrice, int maxStrikePrice, int increment)
        {
            for (var strikePrice = minStrikePrice; strikePrice < maxStrikePrice; strikePrice += increment)
                yield return strikePrice;
        }
    }

    /// <summary>
    /// update order action if short iron condor and we are closing a trade
    /// </summary>
    /// <param name="orderActionType"></param>
    public async Task<bool> UpdateOrderActionAsync(OrderActionType orderActionType)
    {
        ThrowIfHistoricalReadOnly();
        SetOrderAction(orderActionType);
        FundReadModel[] funds = [];
        FundOrderReadModel[] fundOrders = [];
        FundOrderTradeReadModel[] fundOrderTrades = [];
        try
        {
            await _appRoot.Services.FundQueries.ExecuteObservableAsync(async model =>
            {
                await model.GetFundsAsync(values => funds = values);
                fundOrders = await model.GetFundOrdersAsync();
                fundOrderTrades = await model.GetFundOrderTradesAsync();
            });

            var fund = funds.FirstOrDefault(value => value.FundId == FundId);
            var fundOrder = fund is null
                ? null
                : fundOrders.FirstOrDefault(value => value.OrderId == FundOrderTrade.OrderId);
            var parent = fundOrder is null
                ? null
                : fundOrderTrades.FirstOrDefault(value =>
                    value.OrderId == fundOrder.OrderId
                    && value.TradeState == TradeState.TradeToOpen);
            if (parent is null)
                return false;

            await _appRoot.Services.TradeQueries.ExecuteObservableAsync(
                model => model.GetOptionTradeAsync(
                    parent.OrderId,
                    parent.TradeId,
                    optionTrade => _parentTrade = optionTrade));
            OnPropertyChanged(nameof(ParentTrade));
            OnPropertyChanged(nameof(ParentTradeOrderQuantity));
            return true;
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Loading Iron Condor Parent Trade Error");
            throw;
        }
    }

    void ThrowIfHistoricalReadOnly()
    {
        if (_historicalReadOnly)
            throw new InvalidOperationException("Historical trade orders are read-only.");
    }

    public OptionTradeLegReadModel GetOptionLeg(OptionLegAction optionLegAction, OptionType optionType)
        => _optionLegMap!.ContainsKey((optionLegAction, optionType)) ? _optionLegMap[(optionLegAction, optionType)] : default!;

    public void SetOptionLeg(OptionLegAction optionLegAction, OptionType optionType, OptionTradeLegReadModel optionLeg)
    {
        if (_optionLegMap.ContainsKey((optionLegAction, optionType)))
        {
            _optionLegMap.Remove((optionLegAction, optionType));
            _optionLegMap.Add((optionLegAction, optionType), optionLeg);
        }
    }

    public OptionTradeLegReadModel GetParentOptionLeg(OptionLegAction optionLegAction, OptionType optionType)
        => _parentTrade?.OptionLegs?.Get(optionLegAction, optionType)!;

    public OptionTradeLegDataReadModel GetParentOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType)
    {
        var optionLegData = default(OptionTradeLegDataReadModel);
        var tradePosition = _parentTrade?.TradePositions?.Get(tradeType, tradeStatus);
        if (tradePosition is not null)
            optionLegData = tradePosition.OptionLegData.Get(optionLegAction, optionType);
        return optionLegData!;
    }

    public OptionTradeLegDataReadModel GetOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType)
        => _optionLegDataMap.ContainsKey((_valueDate, tradeType, tradeStatus, optionLegAction, optionType))
            ? _optionLegDataMap[(_ironCondorTrade.TradeDate, tradeType, tradeStatus, optionLegAction, optionType)]
            : default!;

    public void SetOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType, OptionTradeLegDataReadModel optionLegData)
    {
        if (_optionLegDataMap.ContainsKey((_valueDate, tradeType, tradeStatus, optionLegAction, optionType)))
        {
            _optionLegDataMap.Remove((_valueDate, tradeType, tradeStatus, optionLegAction, optionType));
            _optionLegDataMap.Add((_valueDate, tradeType, tradeStatus, optionLegAction, optionType), optionLegData);
        }
        var tradePosition = _ironCondorTrade?.TradePositions?.Get(tradeType, tradeStatus);
        if (tradePosition != null)
        {
            tradePosition.OptionLegData.Set(optionLegData.OptionLegId, optionLegData);
            _ironCondorTrade!.TradePositions!.Set(tradePosition);
        }
    }

    public void SetTradePositionMap(TradeType tradeType, TradeStatus tradeStatus, TradePositionReadModel tradePosition)
    {
        if (_tradePositionMap.ContainsKey((_valueDate, tradeType, tradeStatus)))
        {
            _tradePositionMap.Remove((_valueDate, tradeType, tradeStatus));
            _tradePositionMap.Add((_valueDate, tradeType, tradeStatus), tradePosition);
        }
    }

    public TradePositionReadModel? GetTradePosition(TradeType tradeType, TradeStatus tradeStatus)
        => _tradePositionMap.ContainsKey((_valueDate, tradeType, tradeStatus)) ? _tradePositionMap[(_valueDate, tradeType, tradeStatus)] : null;

    public bool StrikePriceMapped(OptionLegAction optionLegAction, OptionType optionType)
    {
        if (_optionPriceMap == null || _optionPriceMap.Count == 0) return false;
        return _optionPriceMap.ContainsKey((optionLegAction, optionType));
    }

    public decimal GetStrikePrice(OptionLegAction optionLegAction, OptionType optionType)
    {
        if (_optionPriceMap == null || _optionPriceMap.Count == 0 || !_optionPriceMap.ContainsKey((optionLegAction, optionType)))
            return _optionLegMap[(optionLegAction, optionType)]?.StrikePrice ?? 0m;
        return _optionPriceMap[(optionLegAction, optionType)];
    }

    private void LoadBaseValues(FuturesEodDataV2ReadModel futuresEodData)
    {
        var assetPrice = futuresEodData.ClosePrice;
        var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
        if (pcs is not null)
        {
            pcs = pcs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade?.TradePositions?.Set(pcs);

        }
        var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
        if (ccs is not null)
        {
            ccs = ccs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade?.TradePositions?.Set(ccs);
        }
    }

    private decimal GetNetSpreadValue(double shortBidPrice, double shortAskPrice, double longBidPrice, double longAskPrice)
        => Convert.ToDecimal(((shortBidPrice + shortAskPrice) / 2) - ((longBidPrice + longAskPrice) / 2));

    private OptionTradeReadModel CreateIronCondorTrade(TradeStatus tradeStatus)
    {
        var daysToExpiry = _fundOrderTrade.MaturityDate.DayNumber - _fundOrderTrade.TradeDate.DayNumber;
        var optionLegs = GetOptionLegs();
        var ironCondorTrade = new OptionTradeReadModel (
            orderId: _fundOrderTrade.OrderId,
            tradeId: _fundOrderTrade.TradeId,
            tradeStrategy: string.Empty,
            tradeDate: _fundOrderTrade.TradeDate,
            maturityDate: _fundOrderTrade.MaturityDate,
            tradeType: _fundOrderTrade.TradeType,
            tradeState: _fundOrderTrade.TradeState,
            tradeAction: _fundOrderTrade.TradeAction,
            underlyingContractId: _baseContract.ContractId,
            underlyingAssetType: AssetType.Futures,
            isPrimaryTrade: true,
            isHedgeTrade: false,
            createdOn: DateTime.UtcNow,
            createdBy: string.Empty,
            updatedOn: DateTime.UtcNow,
            updatedBy: string.Empty
        );
        return ironCondorTrade
            .AddOptionLegs(optionLegs)
            .AddTradePosition(new TradePositionReadModel[] {
                CreateTradePosition(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId, this.PutSpreadTradeType),
                CreateTradePosition(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId, this.CallSpreadTradeType)
            }).SetTradeLimit(TradeLimitReadModel.Default(tradeId: _fundOrderTrade.TradeId, tradeType: _fundOrderTrade.TradeType))
                .AddTradeTypeLimits(new TradeTypeLimitReadModel[] {
                new TradeTypeLimitReadModel(tradeId: _fundOrderTrade.TradeId, tradeType: this.PutSpreadTradeType, maxLossLimit: 0.0m, minProfitLimit: 0.0m, maxProfitLimit: 0.0m),
                new TradeTypeLimitReadModel(tradeId: _fundOrderTrade.TradeId, tradeType: this.CallSpreadTradeType, maxLossLimit: 0.0m, minProfitLimit: 0.0m, maxProfitLimit: 0.0m)
            });

        OptionTradeLegReadModel[] GetOptionLegs()
        {
            var contractIds = _fundOrderTrade.GetContractIds();
            return [
                OptionTradeLegReadModel.Default(
                orderId: _fundOrderTrade.OrderId,
                tradeId: _fundOrderTrade.TradeId,
                contractId: contractIds.ElementAtOrDefault(0) ?? string.Empty,
                optionType: OptionType.Put,
                optionLegAction: this.ShortOptionLegAction
            ),
                OptionTradeLegReadModel.Default(
                orderId: _fundOrderTrade.OrderId,
                tradeId: _fundOrderTrade.TradeId,
                contractId: contractIds.ElementAtOrDefault(1) ?? string.Empty,
                optionType: OptionType.Put,
                optionLegAction: this.LongOptionLegAction
            ),
                OptionTradeLegReadModel.Default(
                orderId: _fundOrderTrade.OrderId,
                tradeId: _fundOrderTrade.TradeId,
                contractId: contractIds.ElementAtOrDefault(2) ?? string.Empty,
                optionType: OptionType.Call,
                optionLegAction: this.ShortOptionLegAction
            ),
                OptionTradeLegReadModel.Default(
                orderId: _fundOrderTrade.OrderId,
                tradeId: _fundOrderTrade.TradeId,
                contractId: contractIds.ElementAtOrDefault(3) ?? string.Empty,
                optionType: OptionType.Call,
                optionLegAction: this.LongOptionLegAction
            )];
        }

        TradePositionReadModel CreateTradePosition(int orderId, int tradeId, TradeType tradeType)
        {
            var optionType = GetOptionType(tradeType);
            return TradePositionReadModel.Default(orderId, tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus)
                .AddOptionLegData([
                    OptionTradeLegDataReadModel.Default(orderId, tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus).SetOptionLeg(optionLegs.Get(this.ShortOptionLegAction, optionType)),
                    OptionTradeLegDataReadModel.Default(orderId, tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus).SetOptionLeg(optionLegs.Get(this.LongOptionLegAction, optionType))
                ]);
        }

        OptionType GetOptionType(TradeType tradeType)
            => tradeType switch {
                TradeType.CallCreditSpread => OptionType.Call,
                TradeType.CallDebitSpread => OptionType.Call,
                TradeType.PutCreditSpread => OptionType.Put,
                TradeType.PutDebitSpread => OptionType.Put,
                _ => throw new NotImplementedException()
            };
    }

    FuturesOptionContractReadModel GetFuturesOptionContract(double strikePrice, OptionType futuresOptionType, DateOnly contractMonth)
    {
        return new FuturesOptionContractReadModel(
            contractId: $"{new FuturesOptionContractIdReadModel(_defaultFuturesContractDefinitions.Symbol, contractMonth, futuresOptionType, strikePrice)}",
            symbol: _defaultFuturesContractDefinitions.Symbol,
            localSymbol: GetLocalSymbol(_defaultFuturesContractDefinitions.Symbol, contractMonth, futuresOptionType, strikePrice),
            securityType: _defaultFuturesContractDefinitions.OptionSecurityType,
            currency: _defaultFuturesContractDefinitions.Currency,
            exchange: _defaultFuturesContractDefinitions.Exchange,
            multiplier: _defaultFuturesContractDefinitions.Multiplier,
            contractMonth: contractMonth,
            optionType: $"{futuresOptionType}".ToUpper(),
            strikePrice: strikePrice,
            description: GetDescription()
        );

        string GetDescription()
        {
            var asset = _defaultFuturesContractDefinitions.Symbol;
            var year = contractMonth.Year;
            var month = $"{contractMonth:MMM}";
            var day = contractMonth.Day;
            var optionType = $"{futuresOptionType}".ToUpper();
            var exchange = _defaultFuturesContractDefinitions.Exchange;
            return $"{asset} {year} {month} {day} {optionType} {strikePrice:F0} @ {exchange}";
        }

        string GetLocalSymbol(string symbol, DateOnly valueDate, OptionType futuresOptionType, double strikePrice)
        {
            if (!string.IsNullOrWhiteSpace(LocalSymbol)) return LocalSymbol;
            return $"{FuturesOptionContractReadModel.GetLocalSymbol(symbol, valueDate)} {(futuresOptionType == OptionType.Put ? "P" : "C")}{Convert.ToInt32(strikePrice)}";
        }
    }

    /// <summary>
    /// map trade action from trade reference string if populated
    /// </summary>
    /// <param name="reference"></param>
    void MapOptionPriceFromTradeReference(TradeAction tradeAction, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;

        // parse reference into 4 trade action tokens...
        var tokens = reference.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3) return;

        // 2nd token must start with "P"...
        if (!tokens[0].ToLower().StartsWith("p")) return;

        // 3rd token must be "x"...
        if (tokens[1].ToLower() != "x") return;

        // 4th token must start with "C"...
        if (!tokens[2].ToLower().StartsWith("c")) return;

        // parse put leg prices..
        var putPriceTokens = tokens[0].Substring(1).Split([":"], StringSplitOptions.RemoveEmptyEntries);
        if (putPriceTokens.Length != 2) return;

        // parse call leg prices..
        var callPriceTokens = tokens[2].Substring(1).Split([ ":"], StringSplitOptions.RemoveEmptyEntries);
        if (callPriceTokens.Length != 2) return;

        // map trade strike prices...
        _optionPriceMap = [];
        switch (tradeAction)
        {
            case TradeAction.Sell:
                _optionPriceMap.Add((OptionLegAction.Short, OptionType.Put), decimal.Parse(putPriceTokens[0]));
                _optionPriceMap.Add((OptionLegAction.Long, OptionType.Put), decimal.Parse(putPriceTokens[1]));
                _optionPriceMap.Add((OptionLegAction.Short, OptionType.Call), decimal.Parse(callPriceTokens[0]));
                _optionPriceMap.Add((OptionLegAction.Long, OptionType.Call), decimal.Parse(callPriceTokens[1]));
                break;
            case TradeAction.Buy:
                _optionPriceMap.Add((OptionLegAction.Long, OptionType.Put), decimal.Parse(putPriceTokens[0]));
                _optionPriceMap.Add((OptionLegAction.Short, OptionType.Put), decimal.Parse(putPriceTokens[1]));
                _optionPriceMap.Add((OptionLegAction.Long, OptionType.Call), decimal.Parse(callPriceTokens[0]));
                _optionPriceMap.Add((OptionLegAction.Short, OptionType.Call), decimal.Parse(callPriceTokens[1]));
                break;
        }

    }

    TradeFillReadModel[] GetManualTradeFills(int fundId, int fillQuantity)
    {
        var orderId = _ironCondorTrade.OrderId;
        var tradeId = _ironCondorTrade.TradeId;
        var fillDate = DateTime.UtcNow;
        var tradeFills = new List<TradeFillReadModel>
        {
            new TradeFillReadModel(orderId, tradeId, fillDate, fillQuantity, fillDate, Environment.UserName)
                .AddTradeFillData([
                    new TradeFillDataReadModel(orderId, tradeId,
                        contractId: GetOptionLeg(ShortOptionLegAction, OptionType.Put).ContractId,
                        fillDate: fillDate,
                        bidPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).BidPrice,
                        askPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).AskPrice,
                        commission: fillQuantity * 1.42m,
                        optionLegAction: this.ShortOptionLegAction,
                        createdOn: fillDate,
                        createdBy: Environment.UserName
                    ),
                    new TradeFillDataReadModel(orderId, tradeId,
                        contractId: GetOptionLeg(LongOptionLegAction, OptionType.Put).ContractId,
                        fillDate: fillDate,
                        bidPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Put).BidPrice,
                        askPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Put).AskPrice,
                        commission: fillQuantity * 1.42m,
                        optionLegAction: LongOptionLegAction,
                        createdOn: fillDate,
                        createdBy: Environment.UserName
                    ),
                    new TradeFillDataReadModel( orderId,tradeId,
                        contractId: GetOptionLeg(ShortOptionLegAction, OptionType.Call).ContractId,
                        fillDate: fillDate,
                        bidPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).BidPrice,
                        askPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).AskPrice,
                        commission: fillQuantity * 1.42m,
                        optionLegAction: ShortOptionLegAction,
                        createdOn: fillDate,
                        createdBy: Environment.UserName
                    ),
                    new TradeFillDataReadModel( orderId,tradeId,
                        contractId: GetOptionLeg(LongOptionLegAction, OptionType.Call).ContractId,
                        fillDate: fillDate,
                        bidPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Call).BidPrice,
                        askPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Call).AskPrice,
                        commission: fillQuantity * 1.42m,
                        optionLegAction: LongOptionLegAction,
                        createdOn: fillDate,
                        createdBy: Environment.UserName
                    )
                ])
        };
        return [.. tradeFills];
    }

    void PublishError(UiServiceOperationException exception, string caption)
        => PublishError(exception.ErrorCode, exception.Message, caption);

    void PublishFuturesOptionTickMetrics(string contractId, LatestValueChannelMetrics metrics)
    {
        lock (_liveStreamMetricsGate)
        {
            _futuresOptionTickMetrics[contractId] = metrics;
            LiveStreamMetrics = new IronCondorTradeOrderLiveStreamMetricsSnapshot(
                new Dictionary<string, LatestValueChannelMetrics>(_futuresOptionTickMetrics));
        }
    }

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);

}
