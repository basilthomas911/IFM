using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

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
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly DateOnly? _valueDate;
    readonly IReadOnlyList<FuturesContractV2ReadModel> _baseContracts;
    readonly FundQueryModel _fundQueryModel;
    readonly FundCommandModel _fundCommandModel;
    readonly ReferenceQueryModel _referenceQueryModel;
    readonly FundOrderEventModel _fundOrderEventModel;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    IReadOnlyList<FundReadModel> _funds = [];
    IReadOnlyList<FundOrderReadModel> _fundOrders = [];
    IReadOnlyList<FundOrderTradeReadModel> _fundOrderTrades = [];
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

    /// <summary>Creates the main editor for one trading date and its available futures contracts.</summary>
    public TradeOrderEditorViewModel(
        IAppRoot appRoot,
        DateOnly? valueDate,
        ICollection<FuturesContractV2ReadModel> baseContracts)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentNullException.ThrowIfNull(baseContracts);
        _appRoot = appRoot;
        _valueDate = valueDate;
        _baseContracts = baseContracts.ToArray();
        _fundQueryModel = appRoot.GetModel<FundQueryModel>();
        _fundCommandModel = appRoot.GetModel<FundCommandModel>();
        _referenceQueryModel = appRoot.GetModel<ReferenceQueryModel>();
        _fundOrderEventModel = appRoot.GetModel<FundOrderEventModel>();
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

    /// <summary>Gets the editor trading date.</summary>
    public DateOnly? ValueDate => _valueDate;

    /// <summary>Gets available futures contracts.</summary>
    public IReadOnlyList<FuturesContractV2ReadModel> BaseContracts => _baseContracts;

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
        set => SetProperty(ref _orderActionType, value);
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

    public bool CanCreateOrder => !IsBusy && SelectedFund is not null;
    public bool CanLoadOrder => !IsBusy && SelectedFundOrder is not null && SelectedFundOrderTrade is not null;
    public bool CanDeleteOrder => !IsBusy && SelectedFundOrder is not null;
    public bool CanCompleteOrder => !IsBusy
        && SelectedFundOrder?.OrderStatus == TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open;
    public bool CanAddTrade => CanCompleteOrder;
    public bool CanRemoveTrade => CanCompleteOrder && SelectedFundOrderTrade is not null;
    public bool CanEndOfDay => CanCompleteOrder && SelectedFundOrderTrade is not null;
    public bool CanSubmitOrder => CanCompleteOrder && SelectedFundOrderTrade?.TradeState == TradeState.NewTrade;
    public bool CanUseLiveFeed => CanSubmitOrder;

    /// <summary>Safely gets a fund by index.</summary>
    public FundReadModel? GetFund(int index) => GetAt(Funds, index);

    /// <summary>Safely gets a fund identifier by index.</summary>
    public int GetFundId(int index) => GetFund(index)?.FundId ?? 0;

    /// <summary>Safely gets a visible fund order by index.</summary>
    public FundOrderReadModel? GetFundOrder(int index) => GetAt(FundOrders, index);

    /// <summary>Safely gets a fund order from an explicitly filtered range.</summary>
    public FundOrderReadModel? GetFundOrder(int fundId, DateTime startDate, DateTime endDate, int index)
        => GetAt(Funds.FirstOrDefault(fund => fund.FundId == fundId)?.Orders
            .Where(order => order.OrderDate >= startDate && order.OrderDate <= endDate)
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
        OnPropertyChanged(nameof(FundSelectedIndex));
        OnPropertyChanged(nameof(SelectedFund));
        RebuildOrders();
        return true;
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
        _fromDate = fromDate;
        _toDate = toDate;
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
            var tradeId = 0;
            await _referenceQueryModel.ExecuteObservableAsync(
                async model => await model.NewTradeIdAsync(value => tradeId = value),
                cancellationToken);
            return tradeId;
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception, "New Trade Id Error");
            throw;
        }
    }

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
            FundReadModel[] funds = [];
            FundOrderReadModel[] orders = [];
            FundOrderTradeReadModel[] trades = [];
            await _fundQueryModel.ExecuteObservableAsync(async model =>
            {
                await model.GetFundsAsync(value => funds = value);
                orders = await model.GetFundOrdersAsync();
                trades = await model.GetFundOrderTradesAsync();
            }, cancellationToken);

            var selectedFundId = SelectedFund?.FundId;
            var selectedOrderId = SelectedFundOrder?.OrderId;
            var selectedTradeId = SelectedFundOrderTrade?.TradeId;
            var fundSnapshots = funds
                .Where(fund => !string.IsNullOrWhiteSpace(fund.Name))
                .Select(CloneFund)
                .ToArray();
            var orderSnapshots = orders.Select(CloneOrder).ToArray();
            foreach (var fund in fundSnapshots)
            {
                foreach (var order in orderSnapshots.Where(order => order.FundId == fund.FundId))
                {
                    foreach (var trade in trades.Where(trade => trade.OrderId == order.OrderId))
                        order.Add(trade);
                    fund.Add(order);
                }
            }

            Funds = fundSnapshots;
            _fundSelectedIndex = selectedFundId is null
                ? (Funds.Count > 0 ? 0 : -1)
                : Funds.ToList().FindIndex(fund => fund.FundId == selectedFundId);
            if (_fundSelectedIndex < 0 && Funds.Count > 0)
                _fundSelectedIndex = 0;
            OnPropertyChanged(nameof(FundSelectedIndex));
            OnPropertyChanged(nameof(SelectedFund));
            RebuildOrders(selectedOrderId, selectedTradeId);
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception, "Loading Funds Error");
            throw;
        }
    }

    async Task ExecuteMutationAsync(
        Func<FundCommandModel, Task<Guid>> submit,
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
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);
            await PublishChangeAsync(terminalEvent, cancellationToken);
        }
        catch (ModelOperationException exception)
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
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);
            await PublishChangeAsync(@event, CancellationToken.None);
        }
        catch (ModelOperationException exception)
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
        await _appRoot.GetModel<StatusConsoleModel>().ExecuteObservableAsync(
            async model => await model.WriteConsoleAsync(LogSourceType.TradeOrder, status),
            cancellationToken);
        await LoadCoreAsync(cancellationToken);
    }

    async Task ExecuteFeedCommandAsync(Func<MarketDataFeedCommandModel, Task> command, string caption)
    {
        try
        {
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteObservableAsync(
                async model => await command(model));
        }
        catch (ModelOperationException exception)
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

    void PublishError(ModelOperationException exception, string caption)
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
