using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
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
/// Exposes the complete observable state and guarded operations used by the futures-contract editor.
/// </summary>
public sealed class FuturesContractEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    static readonly TimeSpan TerminalTimeout = TimeSpan.FromSeconds(30);
    static readonly IReadOnlyDictionary<int, string> ContractMonthMap =
        new Dictionary<int, string>
        {
            [1] = "F",
            [2] = "G",
            [3] = "H",
            [4] = "J",
            [5] = "K",
            [6] = "M",
            [7] = "N",
            [8] = "Q",
            [9] = "U",
            [10] = "V",
            [11] = "X",
            [12] = "Z"
        };

    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly MarketDataEventService _eventModel;
    readonly MarketDataCommandService _commandModel;
    readonly MarketDataQueryService _queryModel;
    readonly IReferenceDataService _referenceDataService;
    readonly ICollection<IEvent> _consumeEvents;
    readonly TerminalEventCorrelation _terminalCorrelation = new();
    IReadOnlyList<LookupTypeUiModel> _symbols = [];
    IReadOnlyList<LookupTypeUiModel> _securityTypes = [];
    IReadOnlyList<LookupTypeUiModel> _currencies = [];
    IReadOnlyList<LookupTypeUiModel> _exchanges = [];
    IReadOnlyList<LookupTypeUiModel> _multipliers = [];
    IReadOnlyList<FuturesContractV2ReadModel> _futuresContracts = [];
    string _lastStatusMessage = string.Empty;
    FuturesContractV2ReadModel? _pendingAdd;
    PendingChange? _pendingChange;
    FuturesContractId? _pendingRemove;

    /// <summary>
    /// Creates the editor and resolves its framework-neutral Models from the application composition root.
    /// </summary>
    public FuturesContractEditorViewModel(
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
            new FuturesContractAddedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesContractAddedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesContractChangedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesContractChangedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesContractRemovedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new FuturesContractRemovedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}")
        ];

        LoadOperation = new AsyncOperation(LoadCoreAsync);
        AddOperation = new AsyncOperation(AddCoreAsync, () => _pendingAdd is not null);
        ChangeOperation = new AsyncOperation(ChangeCoreAsync, () => _pendingChange is not null);
        RemoveOperation = new AsyncOperation(RemoveCoreAsync, () => _pendingRemove is not null);
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    /// <summary>Gets the available currencies.</summary>
    public IReadOnlyList<LookupTypeUiModel> Currencies
    {
        get => _currencies;
        private set => SetProperty(ref _currencies, value);
    }

    /// <summary>Gets the available security types.</summary>
    public IReadOnlyList<LookupTypeUiModel> SecurityTypes
    {
        get => _securityTypes;
        private set => SetProperty(ref _securityTypes, value);
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

    /// <summary>Gets the available underlying symbols.</summary>
    public IReadOnlyList<LookupTypeUiModel> Symbols
    {
        get => _symbols;
        private set => SetProperty(ref _symbols, value);
    }

    /// <summary>Gets the values shown by the currently-traded selector.</summary>
    public IReadOnlyList<string> CurrentlyTraded { get; } = ["Yes", "No"];

    /// <summary>Gets the current coherent futures-contract snapshot.</summary>
    public IReadOnlyList<FuturesContractV2ReadModel> FuturesContracts
    {
        get => _futuresContracts;
        private set => SetProperty(ref _futuresContracts, value);
    }

    /// <summary>Gets the latest successful mutation status suitable for a host status surface.</summary>
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    /// <summary>Gets the single-flight operation that loads lookup and contract state.</summary>
    public IAsyncOperation LoadOperation { get; }

    /// <summary>Gets the guarded operation that adds the prepared contract.</summary>
    public IAsyncOperation AddOperation { get; }

    /// <summary>Gets the guarded operation that changes the prepared contract.</summary>
    public IAsyncOperation ChangeOperation { get; }

    /// <summary>Gets the guarded operation that removes the prepared contract.</summary>
    public IAsyncOperation RemoveOperation { get; }

    /// <summary>Gets the accepted command identifier while a mutation is awaiting its terminal event.</summary>
    public Guid CommandId => _terminalCorrelation.CommandId;

    /// <summary>Gets whether every lookup dependency required by the editor is available.</summary>
    public bool AllLookupTypesLoaded =>
        Currencies.Count > 0 &&
        SecurityTypes.Count > 0 &&
        Exchanges.Count > 0 &&
        Multipliers.Count > 0 &&
        Symbols.Count > 0;

    /// <summary>Prepares a contract for the next add operation.</summary>
    public void PrepareAdd(FuturesContractV2ReadModel futuresContract)
    {
        _pendingAdd = futuresContract ?? throw new ArgumentNullException(nameof(futuresContract));
        AddOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares an original identifier and replacement contract for the next change operation.</summary>
    public void PrepareChange(
        FuturesContractId originalContractId,
        FuturesContractV2ReadModel futuresContract)
    {
        ArgumentNullException.ThrowIfNull(originalContractId);
        ArgumentNullException.ThrowIfNull(futuresContract);
        _pendingChange = new PendingChange(originalContractId, futuresContract);
        ChangeOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares a contract identifier for the next remove operation.</summary>
    public void PrepareRemove(FuturesContractId contractId)
    {
        ArgumentNullException.ThrowIfNull(contractId);
        _pendingRemove = contractId;
        RemoveOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Gets a currency short code by presentation index.</summary>
    public string GetCurrency(int index) => GetLookup(Currencies, index).ShortCode;

    /// <summary>Gets a security-type short code by presentation index.</summary>
    public string GetSecurityType(int index) => GetLookup(SecurityTypes, index).ShortCode;

    /// <summary>Gets an exchange short code by presentation index.</summary>
    public string GetExchange(int index) => GetLookup(Exchanges, index).ShortCode;

    /// <summary>Gets a multiplier short code by presentation index.</summary>
    public string GetMultiplier(int index) => GetLookup(Multipliers, index).ShortCode;

    /// <summary>Gets an underlying symbol short code by presentation index.</summary>
    public string GetSymbol(int index) => GetLookup(Symbols, index).ShortCode;

    /// <summary>Gets an underlying symbol description by presentation index.</summary>
    public string GetSymbolDescription(int index) => GetLookup(Symbols, index).Description;

    /// <summary>Gets the standard futures month code for a one-based calendar month.</summary>
    public string GetContractMonth(int month)
        => ContractMonthMap.TryGetValue(month, out var code) ? code : "<empty>";

    /// <summary>Gets a contract by presentation index, or <see langword="null"/> for an invalid index.</summary>
    public FuturesContractV2ReadModel? GetFuturesContract(int index)
        => index >= 0 && index < FuturesContracts.Count ? FuturesContracts[index] : null;

    static LookupTypeUiModel GetLookup(IReadOnlyList<LookupTypeUiModel> values, int index)
        => index >= 0 && index < values.Count
            ? values[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var securityTypes = await LoadLookupAsync("SecurityType", cancellationToken);
        var currencies = await LoadLookupAsync("Currency", cancellationToken);
        var exchanges = await LoadLookupAsync("Exchange", cancellationToken);
        var multipliers = await LoadLookupAsync("Multiplier", cancellationToken);
        var symbols = await LoadLookupAsync("Symbol", cancellationToken);
        var futuresContracts = await QueryFuturesContractsAsync(cancellationToken);

        SecurityTypes = securityTypes;
        Currencies = currencies;
        Exchanges = exchanges;
        Multipliers = multipliers;
        Symbols = symbols;
        FuturesContracts = futuresContracts;
    }

    async Task<IReadOnlyList<LookupTypeUiModel>> LoadLookupAsync(
        string lookupTypeName,
        CancellationToken cancellationToken)
        => (await _referenceDataService.GetLookupTypesAsync(lookupTypeName, cancellationToken))
            .RequireValue();

    async Task<IReadOnlyList<FuturesContractV2ReadModel>> QueryFuturesContractsAsync(
        CancellationToken cancellationToken)
    {
        FuturesContractV2ReadModel[] result = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.GetFuturesContractsAsync(loaded => result = loaded ?? []),
            cancellationToken);
        return result;
    }

    async Task AddCoreAsync(CancellationToken cancellationToken)
    {
        var contract = _pendingAdd ?? throw new InvalidOperationException("No futures contract is prepared for add.");
        await ExecuteMutationAsync(
            model => model.AddFuturesContractAsync(contract, true),
            $"Futures Contract {contract.ContractId} Added",
            () =>
            {
                _pendingAdd = null;
                AddOperation.NotifyCanExecuteChanged();
            },
            cancellationToken);
    }

    async Task ChangeCoreAsync(CancellationToken cancellationToken)
    {
        var change = _pendingChange ?? throw new InvalidOperationException("No futures contract is prepared for change.");
        await ExecuteMutationAsync(
            model => model.ChangeFuturesContractAsync(
                change.OriginalContractId, change.Contract, true),
            $"Futures Contract {change.OriginalContractId} Changed",
            () =>
            {
                _pendingChange = null;
                ChangeOperation.NotifyCanExecuteChanged();
            },
            cancellationToken);
    }

    async Task RemoveCoreAsync(CancellationToken cancellationToken)
    {
        var contractId = _pendingRemove ?? throw new InvalidOperationException("No futures contract is prepared for removal.");
        await ExecuteMutationAsync(
            model => model.RemoveFuturesContractAsync(contractId, true),
            $"Futures Contract {contractId} Removed",
            () =>
            {
                _pendingRemove = null;
                RemoveOperation.NotifyCanExecuteChanged();
            },
            cancellationToken);
    }

    async Task ExecuteMutationAsync(
        Func<MarketDataCommandService, Task<Guid>> submit,
        string statusMessage,
        Action clearPending,
        CancellationToken cancellationToken)
    {
        _terminalCorrelation.BeginAttempt();
        OnPropertyChanged(nameof(CommandId));
        try
        {
            Guid commandId = Guid.Empty;
            await _commandModel.ExecuteObservableAsync(
                async model => commandId = await submit(model),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException(
                    "The futures-contract command returned an empty correlation identifier.");

            var terminal = await _terminalCorrelation.AwaitAsync(
                commandId,
                TerminalTimeout,
                TimeProvider.System,
                cancellationToken);
            OnPropertyChanged(nameof(CommandId));
            if (terminal is IErrorEvent error)
                throw new UiServiceOperationException(error.ErrorCode, error.ErrorMessage);

            FuturesContracts = await QueryFuturesContractsAsync(cancellationToken);
            LastStatusMessage = statusMessage;
            clearPending();
        }
        finally
        {
            _terminalCorrelation.EndAttempt();
            OnPropertyChanged(nameof(CommandId));
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        LoadOperation.Cancel();
        AddOperation.Cancel();
        ChangeOperation.Cancel();
        RemoveOperation.Cancel();
        while (LoadOperation.IsRunning
               || AddOperation.IsRunning
               || ChangeOperation.IsRunning
               || RemoveOperation.IsRunning)
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        await _lifecycle.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await _lifecycle.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StartMarketDataListenerAsync(
                _consumeEvents,
                @event =>
                {
                    _terminalCorrelation.TryPublish(@event);
                    return ValueTask.CompletedTask;
                }).AsTask(),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StopMarketDataListenerAsync().AsTask(),
            cancellationToken);

    sealed record PendingChange(
        FuturesContractId OriginalContractId,
        FuturesContractV2ReadModel Contract);
}
