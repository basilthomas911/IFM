using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

/// <summary>
/// Coordinates a fund-adjustment command with its correlated completion or failure event.
/// </summary>
public sealed class AdjustFundTransactionReadModel : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly FundTransactionReadModel _fundTransaction;
    readonly decimal _fundBalance;
    readonly ICollection<IEvent> _consumeEvents;
    readonly FundEventService _eventModel;
    readonly FundCommandService _commandModel;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    FundTransactionReadModel? _pendingAdjustment;
    UiServiceOperationException? _adjustmentFailure;
    Guid _commandId;
    bool _isAdjustmentCompleted;

    public AdjustFundTransactionReadModel(
        IAppRoot appRoot,
        FundTransactionReadModel fundTransaction,
        decimal fundBalance)
        : base(appRoot)
    {
        _fundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        _fundBalance = fundBalance;
        _eventModel = AppRoot.Services.FundEvents;
        _commandModel = AppRoot.Services.FundCommands;
        _consumeEvents =
        [
            new OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent(),
            new OpeningTradeFundTransactionAdjustmentCreatedFailEvent(),
            new RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent(),
            new RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent(),
            new TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent(),
            new TradeCommissionFundTransactionAdjustmentCreatedFailEvent(),
            new UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent(),
            new UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent()
        ];
        SubmitAdjustmentOperation = new AsyncOperation(
            SubmitAdjustmentCoreAsync,
            () => _pendingAdjustment is not null && CommandId == Guid.Empty);
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    public FundTransactionReadModel FundTransaction => _fundTransaction;
    public decimal FundBalance => _fundBalance;
    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }
    public IAsyncOperation SubmitAdjustmentOperation { get; }

    public bool IsAdjustmentCompleted
    {
        get => _isAdjustmentCompleted;
        private set => SetProperty(ref _isAdjustmentCompleted, value);
    }

    public UiServiceOperationException? AdjustmentFailure
    {
        get => _adjustmentFailure;
        private set => SetProperty(ref _adjustmentFailure, value);
    }

    public void SetPendingAdjustment(FundTransactionReadModel adjustment)
    {
        _pendingAdjustment = adjustment ?? throw new ArgumentNullException(nameof(adjustment));
        lock (_correlationGate)
            _earlyTerminalEvents.Clear();
        IsAdjustmentCompleted = false;
        AdjustmentFailure = null;
        SubmitAdjustmentOperation.NotifyCanExecuteChanged();
    }

    public Task StartListener() => InitializeAsync(CancellationToken.None);

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            async model => await model.StartFundListenerAsync(_consumeEvents, HandleEventAsync),
            cancellationToken);

    public Task StopListener() => StopAsync(CancellationToken.None);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            async model => await model.StopFundListenerAsync(),
            cancellationToken);

    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);
    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();

    public FundTransactionReadModel GetAdjustmentTransaction(decimal amount, string comment)
        => new(
            _fundTransaction.TransactionId,
            DateTime.UtcNow,
            GetAdjustmentTransactionType(),
            _fundTransaction.FundId,
            _fundTransaction.OrderId,
            _fundTransaction.TradeId,
            _fundTransaction.TradeType,
            _fundTransaction.ValueDate,
            _fundTransaction.TradeStatus,
            comment,
            amount,
            _fundBalance);

    public FundTransactionType GetAdjustmentTransactionType()
        => _fundTransaction.TransactionType switch
        {
            FundTransactionType.OpeningTrade => FundTransactionType.OpeningTradeAdjustment,
            FundTransactionType.RealizedTradePnl => FundTransactionType.RealizedTradePnlAdjustment,
            FundTransactionType.TradeCommission => FundTransactionType.TradeCommissionAdjustment,
            FundTransactionType.UnrealizedTradePnl => FundTransactionType.UnrealizedTradePnlAdjustment,
            _ => throw new InvalidOperationException(
                $"Invalid fund transaction type for adjustment '{_fundTransaction.TransactionType}'.")
        };

    async Task SubmitAdjustmentCoreAsync(CancellationToken cancellationToken)
    {
        var adjustment = _pendingAdjustment
            ?? throw new InvalidOperationException("An adjustment must be supplied before submission.");
        await _commandModel.ExecuteObservableAsync(
            async model =>
            {
                var commandId = await model.CreateAdjustmentTransactionAsync(
                    adjustment.TransactionType,
                    adjustment.FundId,
                    adjustment.OrderId,
                    adjustment.TradeId,
                    adjustment.TradeType,
                    adjustment.ValueDate,
                    adjustment.TradeStatus,
                    adjustment.Description,
                    adjustment.Amount,
                    _fundBalance);
                IEvent? earlyEvent;
                lock (_correlationGate)
                {
                    _commandId = commandId;
                    _earlyTerminalEvents.Remove(commandId, out earlyEvent);
                    _earlyTerminalEvents.Clear();
                }
                OnPropertyChanged(nameof(CommandId));
                SubmitAdjustmentOperation.NotifyCanExecuteChanged();
                if (earlyEvent is not null)
                    await HandleEventAsync(earlyEvent);
            },
            cancellationToken);
    }

    async ValueTask HandleEventAsync(IEvent @event)
    {
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (SubmitAdjustmentOperation.IsRunning && _earlyTerminalEvents.Count < 32)
                    _earlyTerminalEvents[@event.CommandId] = @event;
                return;
            }
            if (_commandId != @event.CommandId)
                return;
        }

        switch (@event)
        {
            case OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent:
            case RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent:
            case TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent:
            case UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent:
                CompleteAdjustment();
                break;
            case IErrorEvent error:
                await FailAdjustmentAsync(error);
                break;
        }
    }

    void CompleteAdjustment()
    {
        lock (_correlationGate)
            _commandId = Guid.Empty;
        OnPropertyChanged(nameof(CommandId));
        IsAdjustmentCompleted = true;
        SubmitAdjustmentOperation.NotifyCanExecuteChanged();
    }

    async Task FailAdjustmentAsync(IErrorEvent error)
    {
        lock (_correlationGate)
            _commandId = Guid.Empty;
        OnPropertyChanged(nameof(CommandId));
        AdjustmentFailure = new UiServiceOperationException(error.ErrorCode, error.ErrorMessage);
        SubmitAdjustmentOperation.NotifyCanExecuteChanged();
        await WriteStatusConsole(
            LogSourceType.MarketData,
            error.ErrorCode,
            $"Fund transaction adjustment failed: {error.ErrorMessage}");
    }
}
