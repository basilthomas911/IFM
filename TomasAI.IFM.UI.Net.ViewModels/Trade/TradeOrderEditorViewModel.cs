using TomasAI.IFM.UI.Net.Models.Portfolio;
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
    string Status);

/// <summary>
/// Coordinates the main trade-order workspace through observable state and correlated NATS terminal events.
/// </summary>
public sealed class TradeOrderEditorViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly IAppRoot _appRoot;
    readonly TimeProvider _timeProvider;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly DateOnly? _valueDate;
    readonly IReadOnlyList<FuturesContractV3ReadModel> _baseContracts;    readonly IReferenceDataService _referenceDataService;    IReadOnlyList<PortfolioFundEditorModel> _funds = [];
    IReadOnlyList<PortfolioFundOrderEditorModel> _fundOrders = [];
    IReadOnlyList<PortfolioFundOrderTradeEditorModel> _fundOrderTrades = [];
    IReadOnlyList<PortfolioReadModel> _portfolios = [];
    IReadOnlyList<FundMandateReadModel> _portfolioFunds = [];
    IReadOnlyList<FundOrderProjectionReadModel> _canonicalOrders = [];
    int _portfolioSelectedIndex = -1;
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
        LoadOperation = new AsyncOperation(LoadCoreAsync, () => !IsCommandRunning);
        LoadOperation.PropertyChanged += OperationPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(StartListenersCoreAsync, StopListenersCoreAsync);
    }

    /// <summary>Gets all loaded funds.</summary>
    public IReadOnlyList<PortfolioFundEditorModel> Funds
    {
        get => _funds;
        private set => SetProperty(ref _funds, value);
    }

    /// <summary>Gets orders for the selected fund and date range.</summary>
    public IReadOnlyList<PortfolioFundOrderEditorModel> FundOrders
    {
        get => _fundOrders;
        private set => SetProperty(ref _fundOrders, value);
    }

    /// <summary>Gets trades for the selected fund order.</summary>
    public IReadOnlyList<PortfolioFundOrderTradeEditorModel> FundOrderTrades
    {
        get => _fundOrderTrades;
        private set => SetProperty(ref _fundOrderTrades, value);
    }

    public IReadOnlyList<PortfolioReadModel> Portfolios { get => _portfolios; private set => SetProperty(ref _portfolios, value); }
    public IReadOnlyList<FundMandateReadModel> PortfolioFunds { get => _portfolioFunds; private set => SetProperty(ref _portfolioFunds, value); }
    public IReadOnlyList<FundOrderProjectionReadModel> CanonicalOrders { get => _canonicalOrders; private set => SetProperty(ref _canonicalOrders, value); }
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
    public PortfolioFundEditorModel? SelectedFund => GetAt(Funds, FundSelectedIndex);

    /// <summary>Gets the selected fund order.</summary>
    public PortfolioFundOrderEditorModel? SelectedFundOrder => GetAt(FundOrders, FundOrderSelectedIndex);

    /// <summary>Gets the selected fund-order trade.</summary>
    public PortfolioFundOrderTradeEditorModel? SelectedFundOrderTrade => GetAt(FundOrderTrades, FundOrderTradeSelectedIndex);

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
    /// <summary>Gets whether a canonical Portfolio mutation is running.</summary>
    public bool IsCommandRunning => false;

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

    public bool CanCreateOrder => !IsBusy && SelectedFund is not null;
    public bool CanLoadOrder => !IsBusy && SelectedFundOrder is not null && SelectedFundOrderTrade is not null;
    public bool CanDeleteOrder => !IsBusy
        && SelectedFundOrder is { } order && PortfolioFundOrderEditorPolicy.CanDeleteOrder(order);
    public bool CanCompleteOrder => !IsBusy
        && SelectedFundOrder is { } order && PortfolioFundOrderEditorPolicy.CanCloseOrder(order);
    public bool CanAddTrade => !IsBusy
        && SelectedFundOrder is { } order && PortfolioFundOrderEditorPolicy.CanAddTrade(order);
    public bool CanRemoveTrade => !IsBusy
        && SelectedFundOrder is { } order
        && SelectedFundOrderTrade is { } trade
        && PortfolioFundOrderEditorPolicy.CanRemoveTrade(order, trade);
    public bool CanChangeTradeState => HasMutableOpenOrder && SelectedFundOrderTrade is not null;
    public bool CanEndOfDay => HasMutableOpenOrder && SelectedFundOrderTrade is not null;
    public bool CanSubmitOrder => HasMutableOpenOrder
        && SelectedFundOrderTrade?.TradeState == TradeState.NewTrade
        && CanSubmitOrderAction(OrderActionType);
    public bool CanUseLiveFeed => CanSubmitOrder;

    bool HasMutableOpenOrder => !IsBusy
        && SelectedFundOrder?.OrderStatus == PortfolioOrderEditorStatus.Open;

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
    public PortfolioFundEditorModel? GetFund(int index) => GetAt(Funds, index);

    /// <summary>Safely gets a fund identifier by index.</summary>
    public int GetFundId(int index) => GetFund(index)?.FundId ?? 0;

    /// <summary>Safely gets a visible fund order by index.</summary>
    public PortfolioFundOrderEditorModel? GetFundOrder(int index) => GetAt(FundOrders, index);

    /// <summary>Safely gets a fund order from an explicitly filtered range.</summary>
    public PortfolioFundOrderEditorModel? GetFundOrder(int fundId, DateTime startDate, DateTime endDate, int index)
        => GetAt(Funds.FirstOrDefault(fund => fund.FundId == fundId)?.Orders
            .Where(order => order.OrderDate >= EasternTime.ToUtc(startDate)
                            && order.OrderDate <= EasternTime.ToUtc(endDate))
            .ToArray() ?? [], index);

    /// <summary>Safely gets a selected-order trade by index.</summary>
    public PortfolioFundOrderTradeEditorModel? GetFundOrderTrade(int index) => GetAt(FundOrderTrades, index);

    /// <summary>Gets the single opening trade, when present.</summary>
    public PortfolioFundOrderTradeEditorModel? GetOpeningFundOrderTrade()
        => FundOrderTrades.SingleOrDefault(trade => trade.PrimaryTrade);

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
        OnPropertyChanged(nameof(FundSelectedIndex));
        OnPropertyChanged(nameof(SelectedFund));
        RebuildOrders();

        return true;
    }

    /// <summary>Selects a Portfolio and loads its canonical Portfolio Funds.</summary>
    public async Task SelectPortfolioAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= Portfolios.Count) index = -1;
        if (_portfolioSelectedIndex == index) return;
        var generation = Interlocked.Increment(ref _scopeGeneration);
        _portfolioSelectedIndex = index;
        Funds = []; FundOrders = []; FundOrderTrades = []; PortfolioFunds = []; CanonicalOrders = [];
        _fundSelectedIndex = _fundOrderSelectedIndex = _fundOrderTradeSelectedIndex = -1;
        OnPropertyChanged(nameof(PortfolioSelectedIndex)); OnPropertyChanged(nameof(SelectedPortfolio));
        if (SelectedPortfolio is not null)
        {
            await LoadPortfolioScopeAsync(SelectedPortfolio.PortfolioId, generation, cancellationToken);
        }
    }

    /// <summary>Loads canonical Portfolio Fund orders for the selected scope.</summary>
    public async Task LoadCanonicalOrdersAsync(CancellationToken cancellationToken = default)
    {

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
        FundOrders = CanonicalOrders.Select(ToEditorOrder).ToArray();
        _fundOrderSelectedIndex = FundOrders.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(FundOrderSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrder));
        FundOrderTrades = []; _fundOrderTradeSelectedIndex = -1;
        NotifyCapabilitiesChanged();
    }

    /// <summary>Creates an empty operator-authored order in the selected canonical Portfolio Fund.</summary>
    /// <param name="draft">The order values collected by the editor.</param>
    /// <param name="cancellationToken">A token that cancels command publication or projection reload.</param>
    /// <returns>The committed canonical order composition.</returns>
    public async Task<FundCompositionReservationResult> CreateManualOrderAsync(PortfolioFundOrderEditorModel draft, CancellationToken cancellationToken = default)
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

    /// <summary>Adds an operator-authored trade to a canonical Portfolio fund order.</summary>
    /// <param name="order">The selected canonical Portfolio order.</param>
    /// <param name="trade">The trade values collected by the editor.</param>
    /// <param name="cancellationToken">A token that cancels command publication or projection reload.</param>
    /// <returns>The committed canonical composition.</returns>
    public async Task<FundCompositionReservationResult> AddManualTradeAsync(
        FundOrderProjectionReadModel order,
        PortfolioFundOrderTradeEditorModel trade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(trade);
        var now = DateTime.UtcNow;
        var request = new AddManualFundOrderTradeRequest
        {
            PortfolioId = order.PortfolioId,
            FundId = order.FundId,
            OrderId = order.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            TradeId = trade.TradeId,
            TradeType = trade.TradeType.ToString(),
            TradeDate = trade.TradeDate,
            MaturityDate = trade.MaturityDate,
            TradeState = trade.TradeState.ToString(),
            TradeAction = trade.TradeAction.ToString(),
            Reference = trade.Reference,
            PrimaryTrade = trade.PrimaryTrade,
            BaseContractSymbol = trade.BaseContractSymbol,
            RequestedAtUtc = now,
        };
        var result = await _appRoot.Services.PortfolioFundCommands
            .AddManualTradeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new UiServiceOperationException(
                result.ErrorCode,
                result.ErrorMessage ?? "Unable to add the trade to the manual Portfolio order.");
        LastStatusMessage = $"Trade {trade.TradeId} added to Portfolio order {order.OrderId}.";
        await LoadCanonicalOrdersAsync(cancellationToken).ConfigureAwait(false);
        return result.Value;
    }
    /// <summary>Gets the canonical Portfolio trades attached to an order.</summary>
    /// <param name="orderId">The canonical order identifier.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The projected trades attached to the order.</returns>
    public async Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetCanonicalTradesAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var result = await _appRoot.Services.PortfolioQueries.GetOrderTradesAsync(orderId, 200, cancellationToken: cancellationToken);
        return result.Success && result.Value is not null ? result.Value.Items : [];
    }
    /// <summary>Removes an economically inactive trade from a canonical manual Portfolio order.</summary>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="tradeId">The canonical trade identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The committed canonical composition.</returns>
    public async Task<FundCompositionReservationResult> RemoveManualTradeAsync(
        FundOrderProjectionReadModel order,
        int tradeId,
        CancellationToken cancellationToken = default)
    {
        var request = new ManualFundOrderTradeMutationRequest
        {
            PortfolioId = order.PortfolioId,
            FundId = order.FundId,
            OrderId = order.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            TradeId = tradeId,
            RequestedAtUtc = DateTime.UtcNow,
        };
        return await CompleteManualMutationAsync(
            _appRoot.Services.PortfolioFundCommands.RemoveManualTradeAsync(request, cancellationToken),
            $"Trade {tradeId} removed from Portfolio order {order.OrderId}.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Changes a canonical manual Portfolio order trade lifecycle state.</summary>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="tradeId">The canonical trade identifier.</param>
    /// <param name="tradeState">The target lifecycle state.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The committed canonical composition.</returns>
    public async Task<FundCompositionReservationResult> ChangeManualTradeStateAsync(
        FundOrderProjectionReadModel order,
        int tradeId,
        TradeState tradeState,
        CancellationToken cancellationToken = default)
    {
        var request = new ManualFundOrderTradeMutationRequest
        {
            PortfolioId = order.PortfolioId,
            FundId = order.FundId,
            OrderId = order.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            TradeId = tradeId,
            TradeState = tradeState.ToString(),
            RequestedAtUtc = DateTime.UtcNow,
        };
        return await CompleteManualMutationAsync(
            _appRoot.Services.PortfolioFundCommands.ChangeManualTradeStateAsync(request, cancellationToken),
            $"Trade {tradeId} state changed to {tradeState}.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes a canonical manual Portfolio order after its closing trade completes.</summary>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="reason">The operator close reason.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The committed canonical composition.</returns>
    public async Task<FundCompositionReservationResult> CloseManualOrderAsync(
        FundOrderProjectionReadModel order,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var request = new ManualFundOrderMutationRequest
        {
            PortfolioId = order.PortfolioId,
            FundId = order.FundId,
            OrderId = order.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            Reason = reason,
            RequestedAtUtc = DateTime.UtcNow,
        };
        return await CompleteManualMutationAsync(
            _appRoot.Services.PortfolioFundCommands.CloseManualOrderAsync(request, cancellationToken),
            $"Portfolio order {order.OrderId} closed.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes an empty draft canonical manual Portfolio order.</summary>
    /// <param name="order">The selected canonical order.</param>
    /// <param name="reason">The operator deletion reason.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The accepted deletion command identifier.</returns>
    public async Task<Guid> DeleteManualOrderAsync(
        FundOrderProjectionReadModel order,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        var request = new ManualFundOrderMutationRequest
        {
            PortfolioId = order.PortfolioId,
            FundId = order.FundId,
            OrderId = order.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            Reason = reason,
            RequestedAtUtc = DateTime.UtcNow,
        };
        var result = await _appRoot.Services.PortfolioFundCommands
            .DeleteManualOrderAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value == Guid.Empty)
            throw new UiServiceOperationException(
                result.ErrorCode,
                result.ErrorMessage ?? "Unable to delete the manual Portfolio order.");
        LastStatusMessage = $"Portfolio order {order.OrderId} deleted.";
        await LoadCanonicalOrdersAsync(cancellationToken).ConfigureAwait(false);
        return result.Value;
    }
    async Task<FundCompositionReservationResult> CompleteManualMutationAsync(
        Task<ServiceResult<FundCompositionReservationResult>> operation,
        string status,
        CancellationToken cancellationToken)
    {
        var result = await operation.ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new UiServiceOperationException(
                result.ErrorCode,
                result.ErrorMessage ?? "Unable to mutate the manual Portfolio order.");
        LastStatusMessage = status;
        await LoadCanonicalOrdersAsync(cancellationToken).ConfigureAwait(false);
        return result.Value;
    }


    /// <summary>Loads and selects trades for a canonical Portfolio order.</summary>
    /// <param name="index">The canonical order index.</param>
    /// <param name="cancellationToken">A token that cancels the Portfolio query.</param>
    public async Task SelectCanonicalOrderAsync(int index, CancellationToken cancellationToken = default)
    {
        SelectFundOrder(index);
        var order = SelectedFundOrder;
        if (order is null) { FundOrderTrades = []; return; }
        var result = await _appRoot.Services.PortfolioQueries.GetOrderTradesAsync(order.OrderId, 200, cancellationToken: cancellationToken);
        var trades = result.Success && result.Value is not null ? result.Value.Items.Select(ToEditorTrade).ToArray() : [];
        var replacement = ToEditorOrder(CanonicalOrders[index]);
        foreach (var trade in trades) replacement.Add(trade);
        FundOrders = FundOrders.Select((value, position) => position == index ? replacement : value).ToArray();
        FundOrderTrades = trades;
        _fundOrderTradeSelectedIndex = trades.Length > 0 ? 0 : -1;
        OnPropertyChanged(nameof(SelectedFundOrder));
        OnPropertyChanged(nameof(FundOrderTradeSelectedIndex));
        OnPropertyChanged(nameof(SelectedFundOrderTrade));
        NotifyCapabilitiesChanged();
    }

    static PortfolioFundOrderEditorModel ToEditorOrder(FundOrderProjectionReadModel order)
        => new(order.FundId, order.OrderId, order.CreatedOnUtc,
            Enum.TryParse<PortfolioOrderEditorStatus>(order.Status, true, out var status) ? status : PortfolioOrderEditorStatus.Open,
            order.UnderlyingRoot, order.RequestedTradeDate, order.RequestedMaturityDate ?? order.RequestedTradeDate,
            string.IsNullOrWhiteSpace(order.OperatorReference) ? order.WorkflowId.ToString("N") : order.OperatorReference,
            order.CreatedOnUtc, order.CreatedBy, null, string.Empty);

    static PortfolioFundOrderTradeEditorModel ToEditorTrade(FundOrderTradeProjectionReadModel trade)
        => new(trade.FundId, trade.OrderId, trade.TradeId,
            Enum.TryParse<TradeType>(trade.TradeType, true, out var type) ? type : TradeType.Unknown,
            trade.RequestedTradeDate, trade.RequestedMaturityDate ?? trade.RequestedTradeDate,
            Enum.TryParse<TradeState>(trade.TradeState, true, out var state) ? state : TradeState.NewTrade,
            Enum.TryParse<TradeAction>(trade.TradeAction, true, out var action) ? action : TradeAction.Buy,
            trade.InstructionReference, trade.PrimaryTrade, trade.BaseContractSymbol, trade.CreatedOnUtc, trade.CreatedBy, null, string.Empty);

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

    /// <summary>
    /// Resolves fill evidence before permitting removal of a cancelled trade. A failed or unavailable
    /// lookup leaves the evidence unknown and therefore keeps removal disabled.
    /// </summary>
    public async Task RefreshSelectedTradeFillEvidenceAsync()
    {
        var selected = SelectedFundOrderTrade;
        if (selected is null || selected.TradeState != TradeState.OrderCancelled
            || selected.HasFillEvidence.HasValue)
            return;

        bool? hasFillEvidence = null;
        await _appRoot.Services.TradeQueries.GetOptionTradeAsync(
            selected.OrderId,
            selected.TradeId,
            trade =>
            {
                hasFillEvidence = trade.TradeFills?.Any(fill => fill.FillQuantity != 0) == true;
            });

        if (!hasFillEvidence.HasValue || SelectedFundOrderTrade?.Id != selected.Id)
            return;

        FundOrderTrades = FundOrderTrades
            .Select(trade => trade.Id == selected.Id
                ? trade with { HasFillEvidence = hasFillEvidence.Value }
                : trade)
            .ToArray();
        OnPropertyChanged(nameof(SelectedFundOrderTrade));
        NotifyCapabilitiesChanged();
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

    /// <summary>Adds live market data for a trade.</summary>
    public Task AddTradeLiveFeed(TradeEntityId tradeId)
        => ExecuteFeedCommandAsync(
            model => model.AddTradeLiveFeedAsync(tradeId, RequiredValueDate()),
            "Add Trade Live Feed Error");

    public Task RemoveTradeLiveFeed(TradeEntityId tradeId)
        => ExecuteFeedCommandAsync(
            model => model.RemoveTradeLiveFeedAsync(tradeId, RequiredValueDate()),
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

    /// <summary>Starts the canonical Portfolio order listener lifecycle.</summary>
    /// <returns>A task that completes when listener initialization finishes.</returns>
    public Task StartFundOrderListener() => InitializeAsync(CancellationToken.None);
    /// <summary>Stops the canonical Portfolio order listener lifecycle.</summary>
    /// <returns>A task that completes when listener shutdown finishes.</returns>
    public Task StopFundOrderListener() => StopAsync(CancellationToken.None);
    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
    /// <inheritdoc />
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

    Task StartListenersCoreAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }

    Task StopListenersCoreAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {

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

    async Task LoadPortfolioScopeAsync(int portfolioId, long generation, CancellationToken cancellationToken)
    {
        var selectedFundId = SelectedFund?.FundId;
        var fundResult = await _appRoot.Services.PortfolioQueries.GetFundsAsync(portfolioId, null, 200, cancellationToken: cancellationToken);
        if (generation != Volatile.Read(ref _scopeGeneration) || SelectedPortfolio?.PortfolioId != portfolioId) return;
        PortfolioFunds = fundResult.Success && fundResult.Value is not null ? fundResult.Value.Items : [];
        Funds = PortfolioFunds
            .Where(x => x.OperatingState == FundOperatingState.Active)
            .Select(x => new PortfolioFundEditorModel(x.FundId, x.Name, x.Objective, 0m, true, x.CreatedOnUtc, x.CreatedBy))
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

    static PortfolioFundEditorModel CloneFund(PortfolioFundEditorModel fund)
        => new(
            fund.FundId,
            fund.Name,
            fund.Description,
            fund.Balance,
            fund.IsProduction,
            fund.CreatedOn,
            fund.CreatedBy);

    static PortfolioFundOrderEditorModel CloneOrder(PortfolioFundOrderEditorModel order)
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
