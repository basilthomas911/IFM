using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>
/// Exposes observable state and guarded asynchronous loading for a new fund order.
/// </summary>
public sealed class FundOrderEditorViewModel : ObservableObject, IAsyncDisposable
{
    readonly int _fundId;
    readonly DateTime _orderDate;
    readonly OrderStatus _orderStatus = OrderStatus.Open;
    readonly DateOnly _valueDate;
    readonly ReferenceQueryModel _referenceQueryModel;
    readonly MarketDataFeedQueryModel _marketDataFeedQueryModel;
    readonly TimeProvider _timeProvider;
    int _orderId;
    string _selectedBaseContractId;
    DateOnly _tradeDate;
    DateOnly _maturityDate;
    string _reference = string.Empty;
    FuturesEodDataV2ReadModel? _futuresEodData;
    PresentationError? _lastError;
    long _errorSequence;

    /// <summary>Creates a new-order editor for one fund and trading date.</summary>
    public FundOrderEditorViewModel(
        IAppRoot appRoot,
        DateOnly valueDate,
        IEnumerable<FuturesContractV2ReadModel> baseContracts,
        int fundId,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentNullException.ThrowIfNull(baseContracts);

        _fundId = fundId;
        _valueDate = valueDate;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _orderDate = EasternTime.GetNow(_timeProvider);
        _tradeDate = valueDate;
        _maturityDate = DateOnly.FromDateTime(_orderDate);
        BaseContractIds = baseContracts.Select(contract => contract.ContractId).ToArray();
        _selectedBaseContractId = BaseContractIds.FirstOrDefault() ?? string.Empty;
        _referenceQueryModel = appRoot.GetModel<ReferenceQueryModel>();
        _marketDataFeedQueryModel = appRoot.GetModel<MarketDataFeedQueryModel>();
        LoadOperation = new AsyncOperation(LoadCoreAsync);
        RefreshReferenceOperation = new AsyncOperation(RefreshReferenceCoreAsync, () => !LoadOperation.IsRunning);
        LoadOperation.PropertyChanged += OperationPropertyChanged;
        RefreshReferenceOperation.PropertyChanged += OperationPropertyChanged;
        UpdateReference();
    }

    /// <summary>Gets the generated order identifier.</summary>
    public int OrderId
    {
        get => _orderId;
        private set
        {
            if (!SetProperty(ref _orderId, value))
                return;
            OnPropertyChanged(nameof(FundOrder));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>Gets the immutable order creation timestamp.</summary>
    public DateTime OrderDate => _orderDate;

    /// <summary>Gets the initial order status.</summary>
    public OrderStatus OrderStatus => _orderStatus;

    /// <summary>Gets the trading date.</summary>
    public DateOnly TradeDate
    {
        get => _tradeDate;
        private set => SetProperty(ref _tradeDate, value);
    }

    /// <summary>Gets the order maturity date.</summary>
    public DateOnly MaturityDate
    {
        get => _maturityDate;
        private set => SetProperty(ref _maturityDate, value);
    }

    /// <summary>Gets available base-contract identifiers.</summary>
    public IReadOnlyList<string> BaseContractIds { get; }

    /// <summary>Gets the selected base-contract identifier.</summary>
    public string SelectedBaseContractId
    {
        get => _selectedBaseContractId;
        private set => SetProperty(ref _selectedBaseContractId, value);
    }

    /// <summary>Gets the generated human-readable order reference.</summary>
    public string Reference
    {
        get => _reference;
        private set => SetProperty(ref _reference, value);
    }

    /// <summary>Gets the latest EOD snapshot used to enrich the reference.</summary>
    public FuturesEodDataV2ReadModel? FuturesEodData
    {
        get => _futuresEodData;
        private set => SetProperty(ref _futuresEodData, value);
    }

    /// <summary>Gets the latest coded query error.</summary>
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets whether either editor query is running.</summary>
    public bool IsBusy => LoadOperation.IsRunning || RefreshReferenceOperation.IsRunning;

    /// <summary>Gets whether the current snapshot can be accepted by the modal view.</summary>
    public bool CanSave => !IsBusy
        && OrderId > 0
        && !string.IsNullOrWhiteSpace(SelectedBaseContractId)
        && TradeDate > DateOnly.MinValue
        && MaturityDate >= TradeDate;

    /// <summary>Gets the single-flight operation that loads the identifier and selected-contract EOD data.</summary>
    public IAsyncOperation LoadOperation { get; }

    /// <summary>Gets the single-flight operation that refreshes selected-contract EOD data.</summary>
    public IAsyncOperation RefreshReferenceOperation { get; }

    /// <summary>Gets the immutable domain read model represented by the current editor state.</summary>
    public FundOrderReadModel FundOrder
    {
        get
        {
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            return new FundOrderReadModel(
                fundId: _fundId,
                orderId: OrderId,
                orderDate: EasternTime.ToUtc(OrderDate),
                orderStatus: OrderStatus,
                baseContractId: SelectedBaseContractId,
                tradeDate: TradeDate,
                maturityDate: MaturityDate,
                reference: Reference,
                createdBy: user,
                createdOn: nowUtc,
                updatedBy: user,
                updatedOn: nowUtc);
        }
    }

    /// <summary>Selects a base contract by safe list index.</summary>
    public bool SelectBaseContract(int index)
    {
        if (IsBusy || index < 0 || index >= BaseContractIds.Count)
            return false;

        var contractId = BaseContractIds[index];
        if (contractId == SelectedBaseContractId)
            return false;

        SelectedBaseContractId = contractId;
        FuturesEodData = null;
        UpdateReference();
        OnPropertyChanged(nameof(FundOrder));
        OnPropertyChanged(nameof(CanSave));
        return true;
    }

    /// <summary>Updates the trading date and derived reference.</summary>
    public void SetTradeDate(DateOnly tradeDate)
    {
        TradeDate = tradeDate;
        UpdateReference();
        OnPropertyChanged(nameof(FundOrder));
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>Updates the maturity date and derived reference.</summary>
    public void SetMaturityDate(DateOnly maturityDate)
    {
        MaturityDate = maturityDate;
        UpdateReference();
        OnPropertyChanged(nameof(FundOrder));
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>Overrides the generated reference with an operator-provided audit reference.</summary>
    public void SetReference(string reference)
    {
        Reference = reference?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(FundOrder));
        OnPropertyChanged(nameof(CanSave));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        LoadOperation.PropertyChanged -= OperationPropertyChanged;
        RefreshReferenceOperation.PropertyChanged -= OperationPropertyChanged;
        await DisposeOperationAsync(LoadOperation);
        await DisposeOperationAsync(RefreshReferenceOperation);
    }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var orderId = 0;
            await _referenceQueryModel.ExecuteObservableAsync(
                async model => await model.NewOrderIdAsync(value => orderId = value),
                cancellationToken);
            OrderId = orderId;
            await RefreshReferenceCoreAsync(cancellationToken);
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception, "New Fund Order Error");
            throw;
        }
    }

    async Task RefreshReferenceCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SelectedBaseContractId))
        {
            UpdateReference();
            return;
        }

        try
        {
            FuturesEodDataV2ReadModel? loaded = null;
            await _marketDataFeedQueryModel.ExecuteObservableAsync(
                async model => await model.GetFuturesEodDataAsync(
                    SelectedBaseContractId,
                    _valueDate,
                    value => loaded = value),
                cancellationToken);
            FuturesEodData = loaded;
            UpdateReference();
            OnPropertyChanged(nameof(FundOrder));
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception, "Futures EOD Data Error");
            throw;
        }
    }

    void UpdateReference()
    {
        Reference = FuturesEodData is null
            ? $"{SelectedBaseContractId} @ {TradeDate:MMM dd} - {MaturityDate:MMM dd}"
            : $"{SelectedBaseContractId} @ {TradeDate:MMM dd} - {MaturityDate:MMM dd} => {FuturesEodData.MarketDirection}:{FuturesEodData.MarketVolatility}:{FuturesEodData.PriceDirection}:{FuturesEodData.PriceVolatility}";
    }

    void PublishError(ModelOperationException exception, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            exception.ErrorCode,
            exception.Message,
            caption);

    void OperationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not (nameof(IAsyncOperation.IsRunning) or nameof(IAsyncOperation.CanExecute)))
            return;

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSave));
    }

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
