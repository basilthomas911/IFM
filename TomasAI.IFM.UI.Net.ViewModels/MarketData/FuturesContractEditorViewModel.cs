using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Exposes the complete observable state and guarded operations used by the futures-contract editor.
/// </summary>
public sealed class FuturesContractEditorViewModel : BaseEditorViewModel
{
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

    readonly MarketDataCommandModel _commandModel;
    readonly MarketDataQueryModel _queryModel;
    readonly ReferenceQueryModel _referenceQueryModel;
    IReadOnlyList<LookupTypeReadModel> _symbols = [];
    IReadOnlyList<LookupTypeReadModel> _securityTypes = [];
    IReadOnlyList<LookupTypeReadModel> _currencies = [];
    IReadOnlyList<LookupTypeReadModel> _exchanges = [];
    IReadOnlyList<LookupTypeReadModel> _multipliers = [];
    IReadOnlyList<FuturesContractV2ReadModel> _futuresContracts = [];
    string _lastStatusMessage = string.Empty;
    FuturesContractV2ReadModel? _pendingAdd;
    PendingChange? _pendingChange;
    FuturesContractId? _pendingRemove;

    /// <summary>
    /// Creates the editor and resolves its framework-neutral Models from the application composition root.
    /// </summary>
    public FuturesContractEditorViewModel(IAppRoot appRoot) : base(appRoot)
    {
        _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
        _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
        _referenceQueryModel = AppRoot.GetModel<ReferenceQueryModel>();

        LoadOperation = new AsyncOperation(LoadCoreAsync);
        AddOperation = new AsyncOperation(AddCoreAsync, () => _pendingAdd is not null);
        ChangeOperation = new AsyncOperation(ChangeCoreAsync, () => _pendingChange is not null);
        RemoveOperation = new AsyncOperation(RemoveCoreAsync, () => _pendingRemove is not null);
    }

    /// <summary>Gets the available currencies.</summary>
    public IReadOnlyList<LookupTypeReadModel> Currencies
    {
        get => _currencies;
        private set => SetProperty(ref _currencies, value);
    }

    /// <summary>Gets the available security types.</summary>
    public IReadOnlyList<LookupTypeReadModel> SecurityTypes
    {
        get => _securityTypes;
        private set => SetProperty(ref _securityTypes, value);
    }

    /// <summary>Gets the available exchanges.</summary>
    public IReadOnlyList<LookupTypeReadModel> Exchanges
    {
        get => _exchanges;
        private set => SetProperty(ref _exchanges, value);
    }

    /// <summary>Gets the available contract multipliers.</summary>
    public IReadOnlyList<LookupTypeReadModel> Multipliers
    {
        get => _multipliers;
        private set => SetProperty(ref _multipliers, value);
    }

    /// <summary>Gets the available underlying symbols.</summary>
    public IReadOnlyList<LookupTypeReadModel> Symbols
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

    static LookupTypeReadModel GetLookup(IReadOnlyList<LookupTypeReadModel> values, int index)
        => index >= 0 && index < values.Count
            ? values[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var securityTypes = await LoadLookupAsync(
            (model, completed) => model.LoadSecurityTypesAsync(completed), cancellationToken);
        var currencies = await LoadLookupAsync(
            (model, completed) => model.LoadCurrenciesAsync(completed), cancellationToken);
        var exchanges = await LoadLookupAsync(
            (model, completed) => model.LoadExchangesAsync(completed), cancellationToken);
        var multipliers = await LoadLookupAsync(
            (model, completed) => model.LoadMultipliersAsync(completed), cancellationToken);
        var symbols = await LoadLookupAsync(
            (model, completed) => model.LoadSymbolsAsync(completed), cancellationToken);
        var futuresContracts = await QueryFuturesContractsAsync(cancellationToken);

        SecurityTypes = securityTypes;
        Currencies = currencies;
        Exchanges = exchanges;
        Multipliers = multipliers;
        Symbols = symbols;
        FuturesContracts = futuresContracts;
    }

    async Task<IReadOnlyList<LookupTypeReadModel>> LoadLookupAsync(
        Func<ReferenceQueryModel, Action<ICollection<LookupTypeReadModel>>, Task> load,
        CancellationToken cancellationToken)
    {
        ICollection<LookupTypeReadModel> result = [];
        await _referenceQueryModel.ExecuteObservableAsync(
            model => load(model, loaded => result = loaded ?? []),
            cancellationToken);
        return result.ToArray();
    }

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
        await _commandModel.ExecuteObservableAsync(
            model => model.AddFuturesContractAsync(contract, true), cancellationToken);
        await CompleteMutationAsync(
            $"Futures Contract {contract.ContractId} Added", cancellationToken);
        _pendingAdd = null;
        AddOperation.NotifyCanExecuteChanged();
    }

    async Task ChangeCoreAsync(CancellationToken cancellationToken)
    {
        var change = _pendingChange ?? throw new InvalidOperationException("No futures contract is prepared for change.");
        await _commandModel.ExecuteObservableAsync(
            model => model.ChangeFuturesContractAsync(
                change.OriginalContractId, change.Contract, true),
            cancellationToken);
        await CompleteMutationAsync(
            $"Futures Contract {change.OriginalContractId} Changed", cancellationToken);
        _pendingChange = null;
        ChangeOperation.NotifyCanExecuteChanged();
    }

    async Task RemoveCoreAsync(CancellationToken cancellationToken)
    {
        var contractId = _pendingRemove ?? throw new InvalidOperationException("No futures contract is prepared for removal.");
        await _commandModel.ExecuteObservableAsync(
            model => model.RemoveFuturesContractAsync(contractId, true), cancellationToken);
        await CompleteMutationAsync(
            $"Futures Contract {contractId} Removed", cancellationToken);
        _pendingRemove = null;
        RemoveOperation.NotifyCanExecuteChanged();
    }

    async Task CompleteMutationAsync(string statusMessage, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        FuturesContracts = await QueryFuturesContractsAsync(cancellationToken);
        LastStatusMessage = statusMessage;
    }

    sealed record PendingChange(
        FuturesContractId OriginalContractId,
        FuturesContractV2ReadModel Contract);
}
