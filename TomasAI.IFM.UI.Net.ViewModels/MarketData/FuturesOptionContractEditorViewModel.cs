using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
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

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Coordinates observable futures-option editor state with correlated market-data command events.
/// </summary>
public sealed class FuturesOptionContractEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly MarketDataEventService _eventModel;
    readonly MarketDataCommandService _commandModel;
    readonly MarketDataQueryService _queryModel;
    readonly IReferenceDataService _referenceDataService;
    readonly ICollection<IEvent> _consumeEvents;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    readonly AsyncOperation _loadOperation;
    readonly AsyncOperation _loadContractsOperation;
    readonly AsyncOperation _addOperation;
    readonly AsyncOperation _changeOperation;
    readonly AsyncOperation _removeOperation;
    IReadOnlyList<LookupTypeUiModel> _symbols = [];
    IReadOnlyList<LookupTypeUiModel> _securityTypes = [];
    IReadOnlyList<LookupTypeUiModel> _currencies = [];
    IReadOnlyList<LookupTypeUiModel> _exchanges = [];
    IReadOnlyList<LookupTypeUiModel> _multipliers = [];
    IReadOnlyList<LookupTypeUiModel> _optionTypes = [];
    IReadOnlyList<FuturesOptionContractReadModel> _futuresOptionContracts = [];
    FuturesOptionContractReadModel? _pendingAdd;
    PendingChange? _pendingChange;
    FuturesOptionContractReadModel? _pendingRemove;
    TaskCompletionSource<IEvent>? _terminalCompletion;
    Guid _commandId;
    string _selectedSymbol = string.Empty;
    string _lastStatusMessage = string.Empty;

    /// <summary>
    /// Creates the editor and resolves its Models from the application composition root.
    /// </summary>
    public FuturesOptionContractEditorViewModel(
        IAppRoot appRoot,
        IReferenceDataService referenceDataService) : base(appRoot)
    {
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        _eventModel = AppRoot.Services.MarketDataEvents;
        _commandModel = AppRoot.Services.MarketDataCommands;
        _queryModel = AppRoot.Services.MarketDataQueries;
        _consumeEvents =
        [
            new FuturesOptionContractAddedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractAddedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractChangedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesOptionContractRemovedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}")
        ];

        _loadOperation = new AsyncOperation(LoadCoreAsync);
        _loadContractsOperation = new AsyncOperation(
            LoadContractsCoreAsync,
            () => !string.IsNullOrWhiteSpace(SelectedSymbol));
        _addOperation = new AsyncOperation(
            AddCoreAsync,
            () => _pendingAdd is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _changeOperation = new AsyncOperation(
            ChangeCoreAsync,
            () => _pendingChange is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _removeOperation = new AsyncOperation(
            RemoveCoreAsync,
            () => _pendingRemove is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _addOperation.PropertyChanged += MutationOperationPropertyChanged;
        _changeOperation.PropertyChanged += MutationOperationPropertyChanged;
        _removeOperation.PropertyChanged += MutationOperationPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    /// <summary>Gets the available underlying symbols.</summary>
    public IReadOnlyList<LookupTypeUiModel> Symbols
    {
        get => _symbols;
        private set => SetProperty(ref _symbols, value);
    }

    /// <summary>Gets the available option security types.</summary>
    public IReadOnlyList<LookupTypeUiModel> SecurityTypes
    {
        get => _securityTypes;
        private set => SetProperty(ref _securityTypes, value);
    }

    /// <summary>Gets the available currencies.</summary>
    public IReadOnlyList<LookupTypeUiModel> Currencies
    {
        get => _currencies;
        private set => SetProperty(ref _currencies, value);
    }

    /// <summary>Gets the available exchanges.</summary>
    public IReadOnlyList<LookupTypeUiModel> Exchanges
    {
        get => _exchanges;
        private set => SetProperty(ref _exchanges, value);
    }

    /// <summary>Gets the available contract multipliers.</summary>
    public IReadOnlyList<LookupTypeUiModel> Multipliers
    {
        get => _multipliers;
        private set => SetProperty(ref _multipliers, value);
    }

    /// <summary>Gets the available option types.</summary>
    public IReadOnlyList<LookupTypeUiModel> OptionTypes
    {
        get => _optionTypes;
        private set => SetProperty(ref _optionTypes, value);
    }

    /// <summary>Gets the option contracts for <see cref="SelectedSymbol"/>.</summary>
    public IReadOnlyList<FuturesOptionContractReadModel> FuturesOptionContracts
    {
        get => _futuresOptionContracts;
        private set => SetProperty(ref _futuresOptionContracts, value);
    }

    /// <summary>Gets the symbol whose option contracts are currently published.</summary>
    public string SelectedSymbol
    {
        get => _selectedSymbol;
        private set => SetProperty(ref _selectedSymbol, value);
    }

    /// <summary>Gets the correlated command identifier while a mutation is awaiting its terminal event.</summary>
    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }

    /// <summary>Gets the latest successful mutation status.</summary>
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    /// <summary>Gets whether all lookup dependencies required by the editor are available.</summary>
    public bool AllLookupTypesLoaded =>
        Symbols.Count > 0 && SecurityTypes.Count > 0 && Currencies.Count > 0 &&
        Exchanges.Count > 0 && Multipliers.Count > 0 && OptionTypes.Count > 0;

    /// <summary>Gets the operation that starts the listener and loads a coherent editor snapshot.</summary>
    public IAsyncOperation LoadOperation => _loadOperation;

    /// <summary>Gets the operation that reloads contracts for the selected symbol.</summary>
    public IAsyncOperation LoadContractsOperation => _loadContractsOperation;

    /// <summary>Gets the correlated operation that adds the prepared option contract.</summary>
    public IAsyncOperation AddOperation => _addOperation;

    /// <summary>Gets the correlated operation that changes the prepared option contract.</summary>
    public IAsyncOperation ChangeOperation => _changeOperation;

    /// <summary>Gets the correlated operation that removes the prepared option contract.</summary>
    public IAsyncOperation RemoveOperation => _removeOperation;

    /// <summary>Selects the symbol used by the next contract load.</summary>
    public void SelectSymbol(int index)
    {
        SelectedSymbol = GetLookup(Symbols, index).ShortCode;
        _loadContractsOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares an option contract for the next add operation.</summary>
    public void PrepareAdd(FuturesOptionContractReadModel contract)
    {
        _pendingAdd = contract ?? throw new ArgumentNullException(nameof(contract));
        PrepareMutation();
        _addOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares an original identifier and replacement contract for the next change operation.</summary>
    public void PrepareChange(string originalContractId, FuturesOptionContractReadModel contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContractId);
        ArgumentNullException.ThrowIfNull(contract);
        _pendingChange = new PendingChange(originalContractId, contract);
        PrepareMutation();
        _changeOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares an option contract for the next remove operation.</summary>
    public void PrepareRemove(FuturesOptionContractReadModel contract)
    {
        _pendingRemove = contract ?? throw new ArgumentNullException(nameof(contract));
        PrepareMutation();
        _removeOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Gets an underlying symbol by presentation index.</summary>
    public LookupTypeUiModel GetSymbol(int index) => GetLookup(Symbols, index);

    /// <summary>Gets an option type by presentation index.</summary>
    public LookupTypeUiModel GetOptionType(int index) => GetLookup(OptionTypes, index);

    /// <summary>Gets a security type by presentation index.</summary>
    public LookupTypeUiModel GetSecurityType(int index) => GetLookup(SecurityTypes, index);

    /// <summary>Gets a currency by presentation index.</summary>
    public LookupTypeUiModel GetCurrency(int index) => GetLookup(Currencies, index);

    /// <summary>Gets an exchange by presentation index.</summary>
    public LookupTypeUiModel GetExchange(int index) => GetLookup(Exchanges, index);

    /// <summary>Gets a multiplier by presentation index.</summary>
    public LookupTypeUiModel GetMultiplier(int index) => GetLookup(Multipliers, index);

    /// <summary>Gets an option contract by presentation index, or <see langword="null"/> for an invalid index.</summary>
    public FuturesOptionContractReadModel? GetFuturesOptionContract(int index)
        => index >= 0 && index < FuturesOptionContracts.Count ? FuturesOptionContracts[index] : null;

    /// <summary>Gets the presentation index of an option-type short code.</summary>
    public int GetOptionTypeIndex(string shortCode) => GetLookupIndex(OptionTypes, shortCode);

    /// <summary>Gets the presentation index of a security-type short code.</summary>
    public int GetSecurityTypeIndex(string shortCode) => GetLookupIndex(SecurityTypes, shortCode);

    /// <summary>Gets the presentation index of a currency short code.</summary>
    public int GetCurrencyIndex(string shortCode) => GetLookupIndex(Currencies, shortCode);

    /// <summary>Gets the presentation index of an exchange short code.</summary>
    public int GetExchangeIndex(string shortCode) => GetLookupIndex(Exchanges, shortCode);

    /// <summary>Gets the presentation index of a multiplier short code.</summary>
    public int GetMultiplierIndex(string shortCode) => GetLookupIndex(Multipliers, shortCode);

    /// <summary>Starts the market-data terminal-event listener once.</summary>
    public Task StartListener() => InitializeAsync(CancellationToken.None);

    /// <summary>Stops the market-data terminal-event listener and cancels editor operations.</summary>
    public Task StopListener() => StopAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancelOperations();
        await AwaitOperationsStoppedAsync();
        await _lifecycle.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _addOperation.PropertyChanged -= MutationOperationPropertyChanged;
        _changeOperation.PropertyChanged -= MutationOperationPropertyChanged;
        _removeOperation.PropertyChanged -= MutationOperationPropertyChanged;
        await _lifecycle.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync).AsTask(),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StopMarketDataListenerAsync().AsTask(),
            cancellationToken);

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var securityTypes = await LoadLookupAsync("SecurityType", cancellationToken);
        var currencies = await LoadLookupAsync("Currency", cancellationToken);
        var exchanges = await LoadLookupAsync("Exchange", cancellationToken);
        var multipliers = await LoadLookupAsync("Multiplier", cancellationToken);
        var optionTypes = await LoadLookupAsync("OptionType", cancellationToken);
        var symbols = await LoadLookupAsync("Symbol", cancellationToken);
        var selectedSymbol = symbols.FirstOrDefault()?.ShortCode ?? string.Empty;
        var contracts = string.IsNullOrWhiteSpace(selectedSymbol)
            ? []
            : await QueryContractsAsync(selectedSymbol, cancellationToken);

        SecurityTypes = securityTypes;
        Currencies = currencies;
        Exchanges = exchanges;
        Multipliers = multipliers;
        OptionTypes = optionTypes;
        Symbols = symbols;
        SelectedSymbol = selectedSymbol;
        FuturesOptionContracts = contracts;
        NotifyMutationCanExecuteChanged();
        _loadContractsOperation.NotifyCanExecuteChanged();
    }

    async Task LoadContractsCoreAsync(CancellationToken cancellationToken)
        => FuturesOptionContracts = await QueryContractsAsync(SelectedSymbol, cancellationToken);

    async Task<IReadOnlyList<LookupTypeUiModel>> LoadLookupAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => (await _referenceDataService.GetLookupTypesAsync(lookupTypeName, cancellationToken))
            .RequireValue();

    async Task<IReadOnlyList<FuturesOptionContractReadModel>> QueryContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        FuturesOptionContractReadModel[] result = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.GetFuturesOptionContractsAsync(symbol, loaded => result = loaded ?? []),
            cancellationToken);
        return result;
    }

    Task AddCoreAsync(CancellationToken cancellationToken)
    {
        var contract = _pendingAdd
            ?? throw new InvalidOperationException("No futures option contract is prepared for add.");
        return ExecuteMutationAsync(
            model => model.AddFuturesOptionContractAsync(contract, true),
            contract.Symbol,
            $"Futures Option Contract {contract.ContractId} Added",
            () => _pendingAdd = null,
            cancellationToken);
    }

    Task ChangeCoreAsync(CancellationToken cancellationToken)
    {
        var change = _pendingChange
            ?? throw new InvalidOperationException("No futures option contract is prepared for change.");
        return ExecuteMutationAsync(
            model => model.ChangeFuturesOptionContractAsync(
                change.OriginalContractId, change.Contract, true),
            change.Contract.Symbol,
            $"Futures Option Contract {change.OriginalContractId} Changed",
            () => _pendingChange = null,
            cancellationToken);
    }

    Task RemoveCoreAsync(CancellationToken cancellationToken)
    {
        var contract = _pendingRemove
            ?? throw new InvalidOperationException("No futures option contract is prepared for removal.");
        return ExecuteMutationAsync(
            model => model.RemoveFuturesOptionContractAsync(contract.ContractId, true),
            contract.Symbol,
            $"Futures Option Contract {contract.ContractId} Removed",
            () => _pendingRemove = null,
            cancellationToken);
    }

    async Task ExecuteMutationAsync(
        Func<MarketDataCommandService, Task<Guid>> submit,
        string symbol,
        string statusMessage,
        Action clearPending,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid commandId = Guid.Empty;
            await _commandModel.ExecuteObservableAsync(
                async model => commandId = await submit(model),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException("The market-data command returned an empty correlation identifier.");

            var terminalEvent = await AwaitTerminalEventAsync(commandId, cancellationToken);
            if (terminalEvent is IErrorEvent error)
                throw new UiServiceOperationException(error.ErrorCode, error.ErrorMessage);

            SelectedSymbol = symbol;
            FuturesOptionContracts = await QueryContractsAsync(symbol, cancellationToken);
            LastStatusMessage = statusMessage;
            clearPending();
        }
        finally
        {
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
        OnPropertyChanged(nameof(CommandId));
        if (earlyEvent is not null)
            completion.TrySetResult(earlyEvent);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    ValueTask HandleEventAsync(IEvent @event)
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (IsMutationRunning && _earlyTerminalEvents.Count < 32)
                    _earlyTerminalEvents[@event.CommandId] = @event;
                return ValueTask.CompletedTask;
            }
            if (_commandId != @event.CommandId)
                return ValueTask.CompletedTask;
            completion = _terminalCompletion;
        }
        completion?.TrySetResult(@event);
        return ValueTask.CompletedTask;
    }

    bool IsMutationRunning =>
        _addOperation.IsRunning || _changeOperation.IsRunning || _removeOperation.IsRunning;

    void PrepareMutation()
    {
        lock (_correlationGate)
            _earlyTerminalEvents.Clear();
        LastStatusMessage = string.Empty;
    }

    void ClearCorrelation()
    {
        lock (_correlationGate)
        {
            _commandId = Guid.Empty;
            _terminalCompletion = null;
            _earlyTerminalEvents.Clear();
        }
        OnPropertyChanged(nameof(CommandId));
    }

    void MutationOperationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IAsyncOperation.IsRunning))
            NotifyMutationCanExecuteChanged();
    }

    void NotifyMutationCanExecuteChanged()
    {
        _addOperation.NotifyCanExecuteChanged();
        _changeOperation.NotifyCanExecuteChanged();
        _removeOperation.NotifyCanExecuteChanged();
    }

    void CancelOperations()
    {
        _loadOperation.Cancel();
        _loadContractsOperation.Cancel();
        _addOperation.Cancel();
        _changeOperation.Cancel();
        _removeOperation.Cancel();
    }

    async Task AwaitOperationsStoppedAsync()
    {
        await AwaitOperationStoppedAsync(_loadOperation);
        await AwaitOperationStoppedAsync(_loadContractsOperation);
        await AwaitOperationStoppedAsync(_addOperation);
        await AwaitOperationStoppedAsync(_changeOperation);
        await AwaitOperationStoppedAsync(_removeOperation);
    }

    static async Task AwaitOperationStoppedAsync(AsyncOperation operation)
    {
        try
        {
            await operation.DisposeAsync();
        }
        catch (Exception) when (operation.LastFailure is not null)
        {
            // The operation's caller already observes LastFailure; shutdown only awaits ownership cleanup.
        }
    }

    static LookupTypeUiModel GetLookup(IReadOnlyList<LookupTypeUiModel> values, int index)
        => index >= 0 && index < values.Count
            ? values[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    static int GetLookupIndex(IReadOnlyList<LookupTypeUiModel> values, string shortCode)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index].ShortCode.Equals(shortCode, StringComparison.OrdinalIgnoreCase))
                return index;
        return -1;
    }

    sealed record PendingChange(string OriginalContractId, FuturesOptionContractReadModel Contract);
}
