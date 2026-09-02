using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Identifies a correlated terminal change published by the trade-order editor.</summary>
public enum TradeOrderEditorChangeKind
{
    OrderAdded,
    OrderRemoved,
    OrderClosed,
    TradeAdded,
    TradeRemoved,
    TradeStateChanged
}

/// <summary>Describes the latest correlated domain change observed by the editor.</summary>
public sealed record TradeOrderEditorChange(
    long Sequence,
    TradeOrderEditorChangeKind Kind,
    IEvent Event);

/// <summary>
/// Coordinates the main trade-order workspace through observable state and correlated NATS terminal events.
/// </summary>
public sealed class TradeOrderEditorViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly IAppRoot _appRoot;
    readonly TimeProvider _timeProvider;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly DateOnly? _valueDate;
    readonly IReadOnlyList<FuturesContractV3ReadModel> _baseContracts;
    readonly FundQueryService _fundQueryModel;
    readonly FundCommandService _fundCommandModel;
    readonly IReferenceDataService _referenceDataService;
    readonly FundOrderEventService _fundOrderEventModel;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    IReadOnlyList<FundReadModel> _funds = [];
    IReadOnlyList<FundOrderReadModel> _fundOrders = [];
    IReadOnlyList<FundOrderTradeReadModel> _fundOrderTrades = [];
    IReadOnlyList<PortfolioReadModel> _portfolios = [];
    IReadOnlyList<FundMandateReadModel> _portfolioFunds = [];
    IReadOnlyList<FundOrderProjectionReadModel> _canonicalOrders = [];
    IReadOnlyList<LegacyPortfolioScopeReadModel> _legacyScopes = [];
    IReadOnlyList<LegacyFundHistoryReadModel> _legacyCatalog = [];
    IReadOnlyList<LegacyFundOrderHistoryReadModel> _legacyOrders = [];
    IReadOnlyList<LegacyFundTradeHistoryReadModel> _legacyTrades = [];
    bool _isLegacyHistoryMode;
    int _portfolioSelectedIndex = -1;
    TaskCompletionSource<IEvent>? _terminalCompletion;
    Guid _commandId;
    int _isSubmittingCommand;
    int _fundSelectedIndex = -1;
    int _fundOrderSelectedIndex = -1;
    int _fundOrderTradeSelectedIndex = -1;
    DateTime _fromDate = DateTime.MinValue;
    DateTime _toDate = DateTime.MaxValue;
    OrderActionType _orderActionType;
    PresentationError? _lastError;
    TradeOrderEditorChange? _lastChange;
    string _lastStatusMessage = string.Empty;
    long _errorSequence;
    long _changeSequence;
    long _scopeGeneration;

    /// <summary>Creates the main editor for one trading date and its available futures contracts.</summary>
    public TradeOrderEditorViewModel(
        IAppRoot appRoot,
        DateOnly? valueDate,
        ICollection<FuturesContractV3ReadModel> baseContracts,
        IReferenceDataService referenceDataService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentNullException.ThrowIfNull(baseContracts);
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        _appRoot = appRoot;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _valueDate = valueDate;
        _baseContracts = baseContracts.ToArray();
        _fundQueryModel = appRoot.Services.FundQueries;
        _fundCommandModel = appRoot.Services.FundCommands;
        _fundOrderEventModel = appRoot.Services.FundOrderEvents;
        LoadOperation = new AsyncOperation(LoadCoreAsync, () => !IsCommandRunning);
        LoadOperation.PropertyChanged += OperationPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(StartListenersCoreAsync, StopListenersCoreAsync);
    }

    /// <summary>Gets all loaded funds.</summary>
    public IReadOnlyList<FundReadModel> Funds
    {
        get => _funds;
        private set => SetProperty(ref _funds, value);
    }

    /// <summary>Gets orders for the selected fund and date range.</summary>
    public IReadOnlyList<FundOrderReadModel> FundOrders
    {
        get => _fundOrders;
        private set => SetProperty(ref _fundOrders, value);
    }

    /// <summary>Gets trades for the selected fund order.</summary>
    public IReadOnlyList<FundOrderTradeReadModel> FundOrderTrades
    {
        get => _fundOrderTrades;
        private set => SetProperty(ref _fundOrderTrades, value);
    }

    public IReadOnlyList<PortfolioReadModel> Portfolios { get => _portfolios; private set => SetProperty(ref _portfolios, value); }
    public IReadOnlyList<FundMandateReadModel> PortfolioFunds { get => _portfolioFunds; private set => SetProperty(ref _portfolioFunds, value); }
    public IReadOnlyList<FundOrderProjectionReadModel> CanonicalOrders { get => _canonicalOrders; private set => SetProperty(ref _canonicalOrders, value); }
    public IReadOnlyList<LegacyFundOrderHistoryReadModel> LegacyOrders { get => _legacyOrders; private set => SetProperty(ref _legacyOrders, value); }
    public IReadOnlyList<LegacyFundTradeHistoryReadModel> LegacyTrades { get => _legacyTrades; private set => SetProperty(ref _legacyTrades, value); }
    public bool IsLegacyHistoryMode => _isLegacyHistoryMode;
    public int PortfolioSelectedIndex => _portfolioSelectedIndex;
    public PortfolioReadModel? SelectedPortfolio => GetAt(Portfolios, PortfolioSelectedIndex);

    /// <summary>Gets the editor trading date.</summary>
    public DateOnly? ValueDate => _valueDate;

    /// <summary>Gets available futures contracts.</summary>
    public IReadOnlyList<FuturesContractV3ReadModel> BaseContracts => _baseContracts;

    /// <summary>Gets the selected fund index, or -1 when no fund is selected.</summary>
    public int FundSelectedIndex => _fundSelectedIndex;

    /// <summary>Gets the selected order index, or -1 when no order is selected.</summary>
    public int FundOrderSelectedIndex => _fundOrderSelectedIndex;

    /// <summary>Gets the selected trade index, or -1 when no trade is selected.</summary>
    public int FundOrderTradeSelectedIndex => _fundOrderTradeSelectedIndex;

    /// <summary>Gets the selected fund.</summary>
    public FundReadModel? SelectedFund => GetAt(Funds, FundSelectedIndex);

    /// <summary>Gets the selected fund order.</summary>
    public FundOrderReadModel? SelectedFundOrder => GetAt(FundOrders, FundOrderSelectedIndex);

    /// <summary>Gets the selected fund-order trade.</summary>
    public FundOrderTradeReadModel? SelectedFundOrderTrade => GetAt(FundOrderTrades, FundOrderTradeSelectedIndex);

    /// <summary>Gets or sets the order action used by the embedded strategy editor.</summary>
    public OrderActionType OrderActionType
    {
        get => _orderActionType;
        set
        {
            if (SetProperty(ref _orderActionType, value))
            {
                OnPropertyChanged(nameof(CanSubmitOrder));
                OnPropertyChanged(nameof(CanUseLiveFeed));
            }
        }
    }

    /// <summary>Gets the active correlation identifier.</summary>
    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }

    /// <summary>Gets whether a fund mutation is awaiting its terminal event.</summary>
    public bool IsCommandRunning => CommandId != Guid.Empty || Volatile.Read(ref _isSubmittingCommand) == 1;

    /// <summary>Gets whether the editor is loading or awaiting a mutation.</summary>
    public bool IsBusy => LoadOperation.IsRunning || IsCommandRunning;

    /// <summary>Gets the latest coded presentation failure.</summary>
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets the latest correlated domain change.</summary>
    public TradeOrderEditorChange? LastChange
    {
        get => _lastChange;
        private set => SetProperty(ref _lastChange, value);
    }

    /// <summary>Gets the latest successful mutation status.</summary>
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    /// <summary>Gets the single-flight fund loading operation.</summary>
    public IAsyncOperation LoadOperation { get; }

    public bool CanCreateOrder => !IsLegacyHistoryMode && !IsBusy && SelectedFund is not null;
    public bool CanLoadOrder => !IsLegacyHistoryMode && !IsBusy && SelectedFundOrder is not null && SelectedFundOrderTrade is not null;
    public bool CanDeleteOrder => !IsLegacyHistoryMode && !IsBusy && SelectedFundOrder is not null;
    public bool CanCompleteOrder => !IsLegacyHistoryMode && !IsBusy
        && SelectedFundOrder?.OrderStatus == TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open;
    public bool CanAddTrade => CanCompleteOrder;
    public bool CanRemoveTrade => CanCompleteOrder && SelectedFundOrderTrade is not null;
    public bool CanChangeTradeState => CanCompleteOrder && SelectedFundOrderTrade is not null;
    public bool CanEndOfDay => CanCompleteOrder && SelectedFundOrderTrade is not null;
    public bool CanSubmitOrder => CanCompleteOrder
        && SelectedFundOrderTrade?.TradeState == TradeState.NewTrade
        && CanSubmitOrderAction(OrderActionType);
    public bool CanUseLiveFeed => CanSubmitOrder;

    /// <summary>
    /// Gets whether the requested order action is permitted now. Closing positions is always
    /// permitted; opening is limited to the weekday 03:00â€“16:00 Eastern entry window.
    /// </summary>
    public bool CanSubmitOrderAction(OrderActionType orderActionType)
        => orderActionType == OrderActionType.Close
            || PositionEntryWindow.IsOpen(_timeProvider.GetUtcNow());

    /// <summary>Validates the time-sensitive entry policy without using exceptions for normal control flow.</summary>
    public bool ValidateOrderSubmission(OrderActionType orderActionType)
    {
        if (CanSubmitOrderAction(orderActionType))
            return true;

        LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            0,
            "New positions can only be opened between 03:00 and 16:00 Eastern, Monday through Friday. "
                + "Existing positions may still be closed.",
            "Position Entry Closed");
        return false;
    }

    /// <summary>Safely gets a fund by index.</summary>
    public FundReadModel? GetFund(int index) => GetAt(Funds, index);

    /// <summary>Safely gets a fund identifier by index.</summary>
    public int GetFundId(int index) => GetFund(index)?.FundId ?? 0;

    /// <summary>Safely gets a visible fund order by index.</summary>
    public FundOrderReadModel? GetFundOrder(int index) => GetAt(FundOrders, index);

    /// <summary>Safely gets a fund order from an explicitly filtered range.</summary>
    public FundOrderReadModel? GetFundOrder(int fundId, DateTime startDate, DateTime endDate, int index)
        => GetAt(Funds.FirstOrDefault(fund => fund.FundId == fundId)?.Orders
            .Where(order => order.OrderDate >= EasternTime.ToUtc(startDate)
                            && order.OrderDate <= EasternTime.ToUtc(endDate))
            .ToArray() ?? [], index);

    /// <summary>Safely gets a selected-order trade by index.</summary>
    public FundOrderTradeReadModel? GetFundOrderTrade(int index) => GetAt(FundOrderTrades, index);

    /// <summary>Gets the single opening trade, when present.</summary>
    public FundOrderTradeReadModel? GetOpeningFundOrderTrade()
        => FundOrderTrades.SingleOrDefault(trade => trade.TradeState == TradeState.TradeToOpen);

    /// <summary>Selects a fund and rebuilds its visible order list.</summary>
    public bool SelectFund(int index)
    {
        if (index < 0 || index >= Funds.Count)
            index = -1;
        if (_fundSelectedIndex == index)
            return false;
        _fundSelectedIndex = index;
        Interlocked.Increment(ref _scopeGeneration);
        CanonicalOrders = [];
        LegacyOrders = [];
        LegacyTrades = [];
        OnPropertyChanged(nameof(FundSelectedIndex));
        OnPropertyChanged(nameof(SelectedFund));
        if (IsLegacyHistoryMode)
        {
            FundOrders = [];
            FundOrderTrades = [];
            _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        }
        else
            RebuildOrders();
        return true;
    }

    public async Task SetLegacyHistoryModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_isLegacyHistoryMode == enabled) return;
        _isLegacyHistoryMode = enabled;
        Interlocked.Increment(ref _scopeGeneration);
        Portfolios = []; PortfolioFunds = []; Funds = []; FundOrders = []; FundOrderTrades = []; CanonicalOrders = [];
        LegacyOrders = []; LegacyTrades = [];
        _portfolioSelectedIndex = _fundSelectedIndex = _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        OnPropertyChanged(nameof(IsLegacyHistoryMode));
        NotifyCapabilitiesChanged();
        await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SelectPortfolioAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= Portfolios.Count) index = -1;
        if (_portfolioSelectedIndex == index) return;
        var generation = Interlocked.Increment(ref _scopeGeneration);
        _portfolioSelectedIndex = index;
        Funds = []; FundOrders = []; FundOrderTrades = []; PortfolioFunds = []; CanonicalOrders = []; LegacyOrders = []; LegacyTrades = [];
        _fundSelectedIndex = _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        OnPropertyChanged(nameof(PortfolioSelectedIndex)); OnPropertyChanged(nameof(SelectedPortfolio));
        if (SelectedPortfolio is not null)
        {
            if (IsLegacyHistoryMode) await LoadLegacyPortfolioScopeAsync(SelectedPortfolio.PortfolioId, generation, cancellationToken);
            else await LoadPortfolioScopeAsync(SelectedPortfolio.PortfolioId, generation, cancellationToken);
        }
    }

    public async Task LoadCanonicalOrdersAsync(CancellationToken cancellationToken = default)
    {
        if (IsLegacyHistoryMode)
        {
            await LoadLegacyOrdersAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        if (SelectedPortfolio is null || SelectedFund is null) { CanonicalOrders = []; return; }
        var generation = Volatile.Read(ref _scopeGeneration);
        var portfolioId = SelectedPortfolio.PortfolioId;
        var fundId = SelectedFund.FundId;
        var rows = new List<FundOrderProjectionReadModel>();
        var month = DateOnly.FromDateTime(_fromDate == DateTime.MinValue ? DateTime.UtcNow : _fromDate);
        var end = DateOnly.FromDateTime(_toDate == DateTime.MaxValue ? DateTime.UtcNow : _toDate);
        month = new DateOnly(month.Year, month.Month, 1); end = new DateOnly(end.Year, end.Month, 1);
        for (var current = month; current <= end; current = current.AddMonths(1))
        {
            var result = await _appRoot.Services.PortfolioQueries.GetOrdersAsync(portfolioId, fundId, current, 200, cancellationToken: cancellationToken);
            if (result.Success && result.Value is not null) rows.AddRange(result.Value.Items);
        }
        if (generation != Volatile.Read(ref _scopeGeneration) || SelectedPortfolio?.PortfolioId != portfolioId || SelectedFund?.FundId != fundId)
            return;
        CanonicalOrders = rows.OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.OrderId).ToArray();
    }

    public async Task<FundCompositionReservationResult> CreateManualOrderAsync(FundOrderReadModel draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var portfolio = SelectedPortfolio ?? throw new InvalidOperationException("Select a Portfolio before creating an order.");
        var mandate = PortfolioFunds.SingleOrDefault(x => x.FundId == draft.FundId)
            ?? throw new InvalidOperationException("The selected Fund is not part of the selected Portfolio.");
        var now = DateTime.UtcNow;
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioVersion = portfolio.PortfolioVersion,
            FundId = mandate.FundId,
            FundMandateVersion = mandate.FundMandateVersion,
            UnderlyingRoot = draft.BaseContractId,
            RequestedTradeDate = draft.TradeDate,
            RequestedMaturityDate = draft.MaturityDate,
            Reference = draft.Reference ?? string.Empty,
            IdempotencyKey = Guid.NewGuid(),
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1),
        };
        var result = await _appRoot.Services.PortfolioFundCommands.CreateManualOrderAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new UiServiceOperationException(result.ErrorCode, result.ErrorMessage ?? "Unable to create the manual Portfolio order.");
        LastStatusMessage = $"Manual Portfolio order {result.Value.Order.OrderId} created.";
        await LoadCanonicalOrdersAsync(cancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    public async Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetCanonicalTradesAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var result = await _appRoot.Services.PortfolioQueries.GetOrderTradesAsync(orderId, 200, cancellationToken: cancellationToken);
        return result.Success && result.Value is not null ? result.Value.Items : [];
    }

    public async Task<IReadOnlyList<LegacyFundTradeHistoryReadModel>> GetLegacyTradesAsync(int orderId, CancellationToken cancellationToken = default)
    {
        if (!IsLegacyHistoryMode || SelectedFund is null) { LegacyTrades = []; return LegacyTrades; }
        var generation = Volatile.Read(ref _scopeGeneration);
        var legacyFundId = SelectedFund.FundId;
        var result = await _appRoot.Services.PortfolioQueries.GetLegacyFundOrderTradesAsync(legacyFundId, orderId, cancellationToken);
        if (generation != Volatile.Read(ref _scopeGeneration) || SelectedFund?.FundId != legacyFundId) return LegacyTrades;
        LegacyTrades = result.Success && result.Value is not null ? result.Value : [];
        return LegacyTrades;
    }

    /// <summary>Selects an order and rebuilds its trade list.</summary>
    public bool SelectFundOrder(int index)
    {
        if (index < 0 || index >= FundOrders.Count)
            index = -1;
        if (_fundOrderSelectedIndex == index)
            return false;
        _fundOrderSelectedIndex = index;
        OnPropertyChanged(nameof(FundOrderSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrder));
        RebuildTrades();
        return true;
    }

    /// <summary>Selects a trade by safe list index.</summary>
    public bool SelectFundOrderTrade(int index)
    {
        if (index < 0 || index >= FundOrderTrades.Count)
            index = -1;
        if (_fundOrderTradeSelectedIndex == index)
            return false;
        _fundOrderTradeSelectedIndex = index;
        OnPropertyChanged(nameof(FundOrderTradeSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrderTrade));
        NotifyCapabilitiesChanged();
        return true;
    }

    /// <summary>Updates the visible order date range.</summary>
    public void SetOrderDateRange(DateTime fromDate, DateTime toDate)
    {
        _fromDate = EasternTime.ToUtc(fromDate);
        _toDate = EasternTime.ToUtc(toDate);
        RebuildOrders();
    }

    /// <summary>Selects a fund by domain identifier after the next load.</summary>
    public void SetSelectedFundIndex(int fundId)
        => SelectFund(Funds.ToList().FindIndex(fund => fund.FundId == fundId));

    /// <summary>Tracks a command submitted by an embedded strategy editor.</summary>
    public void SetCommandId(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A non-empty command identifier is required.", nameof(commandId));
        IEvent? earlyEvent;
        lock (_correlationGate)
        {
            _commandId = commandId;
            _earlyTerminalEvents.Remove(commandId, out earlyEvent);
            _earlyTerminalEvents.Clear();
        }
        NotifyCommandChanged();
        if (earlyEvent is not null)
            _lifecycle.RunAsync(_ => HandleEventAsync(earlyEvent).AsTask());
    }

    public Task AddOrderToFund(FundOrderReadModel fundOrder)
        => ExecuteMutationAsync(model => model.AddOrderToFundAsync(fundOrder), CancellationToken.None);

    public Task RemoveOrderFromFund(FundOrderId fundOrderId)
        => ExecuteMutationAsync(model => model.RemoveOrderFromFundAsync(fundOrderId), CancellationToken.None);

    public Task CloseFundOrder(FundOrderId fundOrderId)
        => ExecuteMutationAsync(model => model.CloseFundOrderAsync(fundOrderId), CancellationToken.None);

    public Task AddTradeToFundOrder(FundOrderTradeReadModel fundOrderTrade)
        => ExecuteMutationAsync(model => model.AddTradeToFundOrderAsync(fundOrderTrade), CancellationToken.None);

    public Task RemoveTradeFromFundOrder(FundOrderTradeId fundOrderTradeId)
        => ExecuteMutationAsync(model => model.RemoveTradeFromFundOrderAsync(fundOrderTradeId), CancellationToken.None);

    public Task ChangeFundOrderTradeState(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
        => ExecuteMutationAsync(model => model.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState), CancellationToken.None);

    public Task AddTradeLiveFeed(int orderId, int tradeId)
        => ExecuteFeedCommandAsync(
            model => model.AddTradeLiveFeedAsync(orderId, tradeId, RequiredValueDate()),
            "Add Trade Live Feed Error");

    public Task RemoveTradeLiveFeed(int orderId, int tradeId)
        => ExecuteFeedCommandAsync(
            model => model.RemoveTradeLiveFeedAsync(orderId, tradeId, RequiredValueDate()),
            "Remove Trade Live Feed Error");

    public Task RemoveTradeLiveFeeds(int orderId)
        => ExecuteFeedCommandAsync(
            model => model.RemoveTradeLiveFeedsAsync(orderId),
            "Remove Trade Live Feeds Error");

    /// <summary>Loads funds through the observable single-flight operation.</summary>
    public Task LoadFunds() => LoadOperation.ExecuteAsync();

    /// <summary>Gets a newly allocated trade identifier.</summary>
    public async Task<int> GetNewTradeIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _referenceDataService.GetNextTradeIdAsync(cancellationToken)).RequireValue();
        }
        catch (UiOperationException exception)
        {
            LastError = new PresentationError(
                Interlocked.Increment(ref _errorSequence),
                exception.ErrorCode,
                exception.Message,
                "New Trade Id Error");
            throw;
        }
    }

    /// <summary>Gets Symbol lookup values used by the trade-entry view.</summary>
    public async Task<IReadOnlyList<LookupTypeUiModel>> GetSymbolsAsync(
        CancellationToken cancellationToken = default)
        => (await _referenceDataService.GetLookupTypesAsync("Symbol", cancellationToken)).RequireValue();

    public Task StartFundOrderListener() => InitializeAsync(CancellationToken.None);
    public Task StopFundOrderListener() => StopAsync(CancellationToken.None);
    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        LoadOperation.PropertyChanged -= OperationPropertyChanged;
        await _lifecycle.DisposeAsync();
        try
        {
            await ((IAsyncDisposable)LoadOperation).DisposeAsync();
        }
        catch (Exception exception) when (ReferenceEquals(LoadOperation.LastFailure, exception))
        {
        }
    }

    async Task StartListenersCoreAsync(CancellationToken cancellationToken)
    {
        await _fundOrderEventModel.ExecuteObservableAsync(
            async model => await model.StartFundOrderListenerAsync(HandleEventAsync),
            cancellationToken);
        try
        {
            await _fundCommandModel.ExecuteObservableAsync(
                async model => await model.StartFundOrderTradeStateEventConsumerAsync(
                    HandleEventAsync,
                    HandleEventAsync),
                cancellationToken);
        }
        catch
        {
            await _fundOrderEventModel.StopFundOrderListenerAsync();
            throw;
        }
    }

    async Task StopListenersCoreAsync(CancellationToken cancellationToken)
    {
        await _fundCommandModel.ExecuteObservableAsync(
            async model => await model.StopFundOrderTradeStateEventConsumerAsync(),
            cancellationToken);
        await _fundOrderEventModel.ExecuteObservableAsync(
            async model => await model.StopFundOrderListenerAsync(),
            cancellationToken);
        CancelCorrelation();
    }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsLegacyHistoryMode)
            {
                await LoadLegacyCoreAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            var generation = Interlocked.Increment(ref _scopeGeneration);
            var selectedPortfolioId = SelectedPortfolio?.PortfolioId;
            var portfolioResult = await _appRoot.Services.PortfolioQueries.GetPortfoliosAsync(PortfolioOperatingState.Active, 200, cancellationToken: cancellationToken);
            if (generation != Volatile.Read(ref _scopeGeneration)) return;
            Portfolios = portfolioResult.Success && portfolioResult.Value is not null ? portfolioResult.Value.Items : [];
            _portfolioSelectedIndex = selectedPortfolioId is null ? (Portfolios.Count > 0 ? 0 : -1) : Portfolios.ToList().FindIndex(x => x.PortfolioId == selectedPortfolioId);
            if (_portfolioSelectedIndex < 0 && Portfolios.Count > 0) _portfolioSelectedIndex = 0;
            OnPropertyChanged(nameof(PortfolioSelectedIndex)); OnPropertyChanged(nameof(SelectedPortfolio));
            if (SelectedPortfolio is not null)
                await LoadPortfolioScopeAsync(SelectedPortfolio.PortfolioId, generation, cancellationToken);
            else
                PortfolioFunds = [];
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Loading Funds Error");
            throw;
        }
    }

    async Task LoadLegacyCoreAsync(CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref _scopeGeneration);
        var selectedPortfolioId = SelectedPortfolio?.PortfolioId;
        var scopesTask = _appRoot.Services.PortfolioQueries.GetLegacyPortfolioScopesAsync(cancellationToken);
        var catalogTask = _appRoot.Services.PortfolioQueries.GetLegacyFundCatalogAsync(cancellationToken);
        await Task.WhenAll(scopesTask, catalogTask).ConfigureAwait(false);
        if (generation != Volatile.Read(ref _scopeGeneration)) return;
        var scopes = await scopesTask;
        var catalog = await catalogTask;
        _legacyScopes = scopes.Success && scopes.Value is not null ? scopes.Value : [];
        _legacyCatalog = catalog.Success && catalog.Value is not null ? catalog.Value : [];
        Portfolios = _legacyScopes.Select(x => x.Portfolio).ToArray();
        _portfolioSelectedIndex = selectedPortfolioId is null ? (Portfolios.Count > 0 ? 0 : -1) : Portfolios.ToList().FindIndex(x => x.PortfolioId == selectedPortfolioId);
        if (_portfolioSelectedIndex < 0 && Portfolios.Count > 0) _portfolioSelectedIndex = 0;
        OnPropertyChanged(nameof(PortfolioSelectedIndex)); OnPropertyChanged(nameof(SelectedPortfolio));
        if (SelectedPortfolio is not null)
            await LoadLegacyPortfolioScopeAsync(SelectedPortfolio.PortfolioId, generation, cancellationToken).ConfigureAwait(false);
    }

    async Task LoadLegacyPortfolioScopeAsync(int portfolioId, long generation, CancellationToken cancellationToken)
    {
        var scope = _legacyScopes.SingleOrDefault(x => x.Portfolio.PortfolioId == portfolioId);
        if (scope is null || generation != Volatile.Read(ref _scopeGeneration)) return;
        PortfolioFunds = scope.Funds;
        var mappedIds = scope.Funds.Select(x => x.HistoricalSourceFundId).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        var catalog = _legacyCatalog.Where(x => mappedIds.Contains(x.Fund.FundId)).ToList();
        if (_legacyScopes.FirstOrDefault()?.Portfolio.PortfolioId == portfolioId)
            catalog.AddRange(_legacyCatalog.Where(x => x.IsUnassigned));
        Funds = catalog.OrderBy(x => x.IsUnassigned).ThenBy(x => x.Fund.FundId).Select(x => x.Fund).ToArray();
        _fundSelectedIndex = Funds.Count > 0 ? 0 : -1;
        FundOrders = []; FundOrderTrades = []; CanonicalOrders = []; LegacyOrders = []; LegacyTrades = [];
        _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        OnPropertyChanged(nameof(FundSelectedIndex)); OnPropertyChanged(nameof(SelectedFund));
        await LoadLegacyOrdersAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task LoadLegacyOrdersAsync(CancellationToken cancellationToken)
    {
        if (!IsLegacyHistoryMode || SelectedFund is null) { LegacyOrders = []; return; }
        var generation = Volatile.Read(ref _scopeGeneration);
        var fundId = SelectedFund.FundId;
        var from = DateOnly.FromDateTime(_fromDate == DateTime.MinValue ? new DateTime(1900, 1, 1) : _fromDate);
        var to = DateOnly.FromDateTime(_toDate == DateTime.MaxValue ? DateTime.UtcNow.AddYears(1) : _toDate);
        var result = await _appRoot.Services.PortfolioQueries.GetLegacyFundOrdersAsync(fundId, from, to, 1000, cancellationToken);
        if (generation != Volatile.Read(ref _scopeGeneration) || SelectedFund?.FundId != fundId) return;
        LegacyOrders = result.Success && result.Value is not null ? result.Value : [];
        LegacyTrades = [];
    }

    async Task LoadPortfolioScopeAsync(int portfolioId, long generation, CancellationToken cancellationToken)
    {
        var selectedFundId = SelectedFund?.FundId;
        var fundResult = await _appRoot.Services.PortfolioQueries.GetFundsAsync(portfolioId, null, 200, cancellationToken: cancellationToken);
        if (generation != Volatile.Read(ref _scopeGeneration) || SelectedPortfolio?.PortfolioId != portfolioId) return;
        PortfolioFunds = fundResult.Success && fundResult.Value is not null ? fundResult.Value.Items : [];
        Funds = PortfolioFunds
            .Where(x => x.OperatingState == FundOperatingState.Active)
            .Select(x => new FundReadModel(x.FundId, x.Name, x.Objective, 0m, true, x.CreatedOnUtc, x.CreatedBy))
            .ToArray();
        _fundSelectedIndex = selectedFundId is null ? (Funds.Count > 0 ? 0 : -1) : Funds.ToList().FindIndex(x => x.FundId == selectedFundId);
        if (_fundSelectedIndex < 0 && Funds.Count > 0) _fundSelectedIndex = 0;
        FundOrders = [];
        FundOrderTrades = [];
        _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        OnPropertyChanged(nameof(FundSelectedIndex));
        OnPropertyChanged(nameof(SelectedFund));
        await LoadCanonicalOrdersAsync(cancellationToken);
    }

    async Task ExecuteMutationAsync(
        Func<FundCommandService, Task<Guid>> submit,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _isSubmittingCommand, 1, 0) != 0 || CommandId != Guid.Empty)
            throw new InvalidOperationException("A trade-order command is already in progress.");
        NotifyCommandChanged();
        try
        {
            Guid commandId = Guid.Empty;
            await _fundCommandModel.ExecuteObservableAsync(
                async model => commandId = await submit(model),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException("The fund command returned an empty correlation identifier.");
            var terminalEvent = await AwaitTerminalEventAsync(commandId, cancellationToken);
            if (terminalEvent is IErrorEvent error)
                throw new UiServiceOperationException(error.ErrorCode, error.ErrorMessage);
            await PublishChangeAsync(terminalEvent, cancellationToken);
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Trade Order Command Error");
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _isSubmittingCommand, 0);
            ClearCorrelation();
        }
    }

    async Task<IEvent> AwaitTerminalEventAsync(Guid commandId, CancellationToken cancellationToken)
    {
        IEvent? earlyEvent;
        TaskCompletionSource<IEvent> completion;
        lock (_correlationGate)
        {
            _commandId = commandId;
            completion = new TaskCompletionSource<IEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminalCompletion = completion;
            _earlyTerminalEvents.Remove(commandId, out earlyEvent);
            _earlyTerminalEvents.Clear();
        }
        NotifyCommandChanged();
        if (earlyEvent is not null)
            completion.TrySetResult(earlyEvent);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    async ValueTask HandleEventAsync(IEvent @event)
    {
        TaskCompletionSource<IEvent>? completion;
        var processExternal = false;
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (_earlyTerminalEvents.Count >= 32)
                    _earlyTerminalEvents.Remove(_earlyTerminalEvents.Keys.First());
                _earlyTerminalEvents[@event.CommandId] = @event;
                return;
            }
            if (_commandId != @event.CommandId)
                return;
            completion = _terminalCompletion;
            processExternal = completion is null;
        }
        if (completion is not null)
        {
            completion.TrySetResult(@event);
            return;
        }
        if (!processExternal)
            return;
        try
        {
            if (@event is IErrorEvent error)
                throw new UiServiceOperationException(error.ErrorCode, error.ErrorMessage);
            await PublishChangeAsync(@event, CancellationToken.None);
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, "Trade Order Command Error");
        }
        catch (Exception exception)
        {
            LastError = new PresentationError(
                Interlocked.Increment(ref _errorSequence),
                0,
                exception.Message,
                "Trade Order Command Error");
        }
        finally
        {
            ClearCorrelation();
        }
    }

    async Task PublishChangeAsync(IEvent @event, CancellationToken cancellationToken)
    {
        var (kind, status) = @event switch
        {
            OrderAddedToFundCompleteEvent complete => (TradeOrderEditorChangeKind.OrderAdded, $"Order created: {complete.FundOrder.OrderId} {complete.FundOrder.Reference}"),
            OrderRemovedFromFundCompleteEvent complete => (TradeOrderEditorChangeKind.OrderRemoved, $"Order removed: {complete.FundOrderId.OrderId}"),
            FundOrderClosedCompleteEvent complete => (TradeOrderEditorChangeKind.OrderClosed, $"Order closed: {complete.FundOrderId}"),
            TradeAddedToFundOrderCompleteEvent complete => (TradeOrderEditorChangeKind.TradeAdded, $"Trade added: {complete.FundOrderTrade.TradeId} {complete.FundOrderTrade.Reference}"),
            TradeRemovedFromFundOrderCompleteEvent complete => (TradeOrderEditorChangeKind.TradeRemoved, $"Trade removed: {complete.FundOrderTradeId.TradeId}"),
            FundOrderTradeStateChangedCompleteEvent complete => (TradeOrderEditorChangeKind.TradeStateChanged, $"Trade state changed: {complete.FundOrderTradeId.OrderId}:{complete.FundOrderTradeId.TradeId} {complete.TradeState}"),
            _ => throw new InvalidOperationException($"Unsupported trade-order terminal event '{@event.GetType().Name}'.")
        };
        LastStatusMessage = status;
        LastChange = new TradeOrderEditorChange(Interlocked.Increment(ref _changeSequence), kind, @event);
        await _appRoot.Services.StatusConsole.ExecuteObservableAsync(
            async model => await model.WriteConsoleAsync(LogSourceType.TradeOrder, status),
            cancellationToken);
        await LoadCoreAsync(cancellationToken);
    }

    async Task ExecuteFeedCommandAsync(Func<MarketDataFeedCommandService, Task> command, string caption)
    {
        try
        {
            await _appRoot.Services.FeedCommands.ExecuteObservableAsync(
                async model => await command(model));
        }
        catch (UiServiceOperationException exception)
        {
            PublishError(exception, caption);
            throw;
        }
    }

    DateOnly RequiredValueDate()
        => ValueDate ?? throw new InvalidOperationException("A value date is required for live-feed commands.");

    void RebuildOrders(int? selectedOrderId = null, int? selectedTradeId = null)
    {
        selectedOrderId ??= SelectedFundOrder?.OrderId;
        selectedTradeId ??= SelectedFundOrderTrade?.TradeId;
        FundOrders = SelectedFund?.Orders
            .Where(order => order.OrderDate >= _fromDate && order.OrderDate <= _toDate)
            .ToArray() ?? [];
        _fundOrderSelectedIndex = selectedOrderId is null
            ? (FundOrders.Count > 0 ? 0 : -1)
            : FundOrders.ToList().FindIndex(order => order.OrderId == selectedOrderId);
        if (_fundOrderSelectedIndex < 0 && FundOrders.Count > 0)
            _fundOrderSelectedIndex = 0;
        OnPropertyChanged(nameof(FundOrderSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrder));
        RebuildTrades(selectedTradeId);
    }

    void RebuildTrades(int? selectedTradeId = null)
    {
        selectedTradeId ??= SelectedFundOrderTrade?.TradeId;
        FundOrderTrades = SelectedFundOrder?.Trades ?? [];
        _fundOrderTradeSelectedIndex = selectedTradeId is null
            ? (FundOrderTrades.Count > 0 ? 0 : -1)
            : FundOrderTrades.ToList().FindIndex(trade => trade.TradeId == selectedTradeId);
        if (_fundOrderTradeSelectedIndex < 0 && FundOrderTrades.Count > 0)
            _fundOrderTradeSelectedIndex = 0;
        OnPropertyChanged(nameof(FundOrderTradeSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrderTrade));
        NotifyCapabilitiesChanged();
    }

    void ClearCorrelation()
    {
        lock (_correlationGate)
        {
            _commandId = Guid.Empty;
            _terminalCompletion = null;
            _earlyTerminalEvents.Clear();
        }
        NotifyCommandChanged();
    }

    void CancelCorrelation()
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_correlationGate)
        {
            completion = _terminalCompletion;
            _commandId = Guid.Empty;
            _terminalCompletion = null;
            _earlyTerminalEvents.Clear();
        }
        completion?.TrySetCanceled();
        NotifyCommandChanged();
    }

    void NotifyCommandChanged()
    {
        OnPropertyChanged(nameof(CommandId));
        OnPropertyChanged(nameof(IsCommandRunning));
        OnPropertyChanged(nameof(IsBusy));
        LoadOperation.NotifyCanExecuteChanged();
        NotifyCapabilitiesChanged();
    }

    void NotifyCapabilitiesChanged()
    {
        OnPropertyChanged(nameof(CanCreateOrder));
        OnPropertyChanged(nameof(CanLoadOrder));
        OnPropertyChanged(nameof(CanDeleteOrder));
        OnPropertyChanged(nameof(CanCompleteOrder));
        OnPropertyChanged(nameof(CanAddTrade));
        OnPropertyChanged(nameof(CanRemoveTrade));
        OnPropertyChanged(nameof(CanChangeTradeState));
        OnPropertyChanged(nameof(CanEndOfDay));
        OnPropertyChanged(nameof(CanSubmitOrder));
        OnPropertyChanged(nameof(CanUseLiveFeed));
    }

    void OperationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not (nameof(IAsyncOperation.IsRunning) or nameof(IAsyncOperation.CanExecute)))
            return;
        OnPropertyChanged(nameof(IsBusy));
        NotifyCapabilitiesChanged();
    }

    void PublishError(UiServiceOperationException exception, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            exception.ErrorCode,
            exception.Message,
            caption);

    static T? GetAt<T>(IReadOnlyList<T> values, int index) where T : class
        => index >= 0 && index < values.Count ? values[index] : null;

    static FundReadModel CloneFund(FundReadModel fund)
        => new(
            fund.FundId,
            fund.Name,
            fund.Description,
            fund.Balance,
            fund.IsProduction,
            fund.CreatedOn,
            fund.CreatedBy);

    static FundOrderReadModel CloneOrder(FundOrderReadModel order)
        => new(
            order.FundId,
            order.OrderId,
            order.OrderDate,
            order.OrderStatus,
            order.BaseContractId,
            order.TradeDate,
            order.MaturityDate,
            order.Reference,
            order.CreatedOn,
            order.CreatedBy,
            order.UpdatedOn,
            order.UpdatedBy);
}
