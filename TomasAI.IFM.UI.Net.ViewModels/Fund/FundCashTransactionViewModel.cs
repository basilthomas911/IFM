using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

/// <summary>
/// Coordinates a cash deposit or withdrawal with its correlated terminal event.
/// </summary>
public sealed class FundCashTransactionViewModel : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly FundEventModel _eventModel;
    readonly FundCommandModel _commandModel;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    FundTransactionReadModel? _pendingTransaction;
    ModelOperationException? _failure;
    Guid _commandId;
    bool _isCompleted;

    public FundCashTransactionViewModel(
        IAppRoot appRoot,
        FundReadModel fund,
        DateOnly valueDate,
        FundTransactionType transactionType)
        : base(appRoot)
    {
        if (transactionType is not (FundTransactionType.CashDeposit or FundTransactionType.CashWithdrawal))
            throw new ArgumentOutOfRangeException(
                nameof(transactionType), transactionType, "Only cash deposits and withdrawals are supported.");
        Fund = fund ?? throw new ArgumentNullException(nameof(fund));
        ValueDate = valueDate;
        TransactionType = transactionType;
        _eventModel = AppRoot.GetModel<FundEventModel>();
        _commandModel = AppRoot.GetModel<FundCommandModel>();
        SubmitOperation = new AsyncOperation(
            SubmitCoreAsync,
            () => _pendingTransaction is not null && CommandId == Guid.Empty);
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    public FundReadModel Fund { get; }
    public DateOnly ValueDate { get; }
    public FundTransactionType TransactionType { get; }
    public IAsyncOperation SubmitOperation { get; }

    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetProperty(ref _isCompleted, value);
    }

    public ModelOperationException? Failure
    {
        get => _failure;
        private set => SetProperty(ref _failure, value);
    }

    public FundTransactionReadModel CreateTransaction(decimal amount, string description)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cash transaction amount must be positive.");
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Cash transaction description is required.", nameof(description));
        return new FundTransactionReadModel(
            transactionId: 0,
            transactionDate: DateTime.UtcNow,
            transactionType: TransactionType,
            fundId: Fund.FundId,
            orderId: 0,
            tradeId: 0,
            tradeType: TradeType.Unknown,
            valueDate: ValueDate,
            tradeStatus: TradeStatus.Open,
            description: description.Trim(),
            amount: amount,
            balance: Fund.Balance);
    }

    public void SetPendingTransaction(FundTransactionReadModel transaction)
    {
        _pendingTransaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        lock (_correlationGate)
            _earlyTerminalEvents.Clear();
        IsCompleted = false;
        Failure = null;
        SubmitOperation.NotifyCanExecuteChanged();
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);
    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            async model => await model.StartFundListenerAsync(
                [new FundTransactionCreatedCompleteEvent(), new FundTransactionCreatedFailEvent()],
                HandleEventAsync),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            async model => await model.StopFundListenerAsync(),
            cancellationToken);

    async Task SubmitCoreAsync(CancellationToken cancellationToken)
    {
        var transaction = _pendingTransaction
            ?? throw new InvalidOperationException("A cash transaction must be supplied before submission.");
        await _commandModel.ExecuteObservableAsync(
            async model =>
            {
                var commandId = await model.CreateFundTransactionAsync(transaction);
                IEvent? earlyEvent;
                lock (_correlationGate)
                {
                    _commandId = commandId;
                    _earlyTerminalEvents.Remove(commandId, out earlyEvent);
                    _earlyTerminalEvents.Clear();
                }
                OnPropertyChanged(nameof(CommandId));
                SubmitOperation.NotifyCanExecuteChanged();
                if (earlyEvent is not null)
                    await HandleEventAsync(earlyEvent);
            },
            cancellationToken);
    }

    async ValueTask HandleEventAsync(IEvent domainEvent)
    {
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (SubmitOperation.IsRunning && _earlyTerminalEvents.Count < 32)
                    _earlyTerminalEvents[domainEvent.CommandId] = domainEvent;
                return;
            }
            if (_commandId != domainEvent.CommandId)
                return;
        }

        switch (domainEvent)
        {
            case FundTransactionCreatedCompleteEvent:
                Complete();
                break;
            case IErrorEvent error:
                await FailAsync(error);
                break;
        }
    }

    void Complete()
    {
        lock (_correlationGate)
            _commandId = Guid.Empty;
        OnPropertyChanged(nameof(CommandId));
        IsCompleted = true;
        SubmitOperation.NotifyCanExecuteChanged();
    }

    async Task FailAsync(IErrorEvent error)
    {
        lock (_correlationGate)
            _commandId = Guid.Empty;
        OnPropertyChanged(nameof(CommandId));
        Failure = new ModelOperationException(error.ErrorCode, error.ErrorMessage);
        SubmitOperation.NotifyCanExecuteChanged();
        await WriteStatusConsole(
            LogSourceType.Fund,
            error.ErrorCode,
            $"Fund cash transaction failed: {error.ErrorMessage}");
    }
}
