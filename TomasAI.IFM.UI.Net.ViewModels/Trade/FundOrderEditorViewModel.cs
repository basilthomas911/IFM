using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Exposes observable state for a new manual Portfolio Fund order.</summary>
public sealed class FundOrderEditorViewModel : ObservableObject, IAsyncDisposable
{
    readonly int _fundId;
    readonly DateTime _orderDate;
    readonly PortfolioOrderEditorStatus _orderStatus = PortfolioOrderEditorStatus.Open;
    readonly IReferenceDataService _referenceDataService;
    readonly TimeProvider _timeProvider;
    readonly bool _allocateOrderId;
    int _orderId;
    string _reference = string.Empty;
    PresentationError? _lastError;
    long _errorSequence;

    /// <summary>Creates a new-order editor for one Portfolio Fund.</summary>
    public FundOrderEditorViewModel(
        int fundId,
        IReferenceDataService referenceDataService,
        TimeProvider? timeProvider = null,
        bool allocateOrderId = true)
    {
        if (fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
        _fundId = fundId;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _allocateOrderId = allocateOrderId;
        _orderDate = EasternTime.GetNow(_timeProvider);
        LoadOperation = new AsyncOperation(LoadCoreAsync);
        LoadOperation.PropertyChanged += OperationPropertyChanged;
    }

    /// <summary>Gets the generated order identifier, or zero when Portfolio authority allocates it on save.</summary>
    public int OrderId
    {
        get => _orderId;
        private set
        {
            if (!SetProperty(ref _orderId, value)) return;
            OnPropertyChanged(nameof(FundOrder));
        }
    }

    public DateTime OrderDate => _orderDate;
    public PortfolioOrderEditorStatus OrderStatus => _orderStatus;

    /// <summary>Gets the optional, multiline operator reference.</summary>
    public string Reference
    {
        get => _reference;
        private set => SetProperty(ref _reference, value);
    }

    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public bool IsBusy => LoadOperation.IsRunning;

    /// <summary>Saving a draft has no required operator-entered fields.</summary>
    public bool CanSave => true;

    public IAsyncOperation LoadOperation { get; }

    public ManualFundOrderDraftEditorModel FundOrder
    {
        get
        {
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            return new ManualFundOrderDraftEditorModel(
                FundId: _fundId,
                OrderId: OrderId,
                OrderDate: EasternTime.ToUtc(OrderDate),
                OrderStatus: OrderStatus,
                Reference: Reference.Trim(),
                CreatedBy: user,
                CreatedOn: nowUtc,
                UpdatedBy: user,
                UpdatedOn: nowUtc);
        }
    }

    /// <summary>Updates the optional reference without collapsing embedded line breaks.</summary>
    public void SetReference(string reference)
    {
        Reference = reference ?? string.Empty;
        OnPropertyChanged(nameof(FundOrder));
    }

    public async ValueTask DisposeAsync()
    {
        LoadOperation.PropertyChanged -= OperationPropertyChanged;
        try
        {
            await ((IAsyncDisposable)LoadOperation).DisposeAsync();
        }
        catch (Exception exception) when (ReferenceEquals(LoadOperation.LastFailure, exception))
        {
            // The caller already observed this completed operation failure.
        }
    }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!_allocateOrderId) return;
        try
        {
            OrderId = (await _referenceDataService.GetNextOrderIdAsync(cancellationToken)).RequireValue();
        }
        catch (UiOperationException exception)
        {
            LastError = new PresentationError(
                Interlocked.Increment(ref _errorSequence),
                exception.ErrorCode,
                exception.Message,
                "New Fund Order Error");
            throw;
        }
    }

    void OperationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not (nameof(IAsyncOperation.IsRunning) or nameof(IAsyncOperation.CanExecute)))
            return;
        OnPropertyChanged(nameof(IsBusy));
    }
}