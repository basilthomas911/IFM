using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

public class AdjustFundTransactionReadModel : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly FundTransactionReadModel _fundTransaction;
    readonly decimal _fundBalance;
    Guid _commandId;
    Action<Guid> _setCommandId;
    ICollection<IEvent> _consumeEvents;
    FundEventModel _eventModel;
    FundCommandModel _commandModel;

    /// <summary>
    /// create adjust fund transaction view model
    /// </summary>
    /// <param name="appRoot"></param>
    public AdjustFundTransactionReadModel(IAppRoot appRoot, FundTransactionReadModel fundTransaction, decimal fundBalance) : base(appRoot)
    {
        _fundTransaction = fundTransaction;
        _fundBalance = fundBalance;
        _commandId = Guid.Empty;
        _eventModel = AppRoot.GetModel<FundEventModel>();
        _commandModel = AppRoot.GetModel<FundCommandModel>();
        _setCommandId = commandId => _commandId = commandId;
        _consumeEvents = new IEvent[]{
            new OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent { },
            new OpeningTradeFundTransactionAdjustmentCreatedFailEvent{ },
            new RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent{ },
            new RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent{ },
            new TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent{ },
            new TradeCommissionFundTransactionAdjustmentCreatedFailEvent{ },
            new UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent{ },
            new UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent{ },
        };
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    public FundTransactionReadModel FundTransaction => _fundTransaction;
    public decimal FundBalance => _fundBalance;
    public Action OnFundTransactionAdjustment { get; set; } = null!;
    public Action<string, string> OnErrorMessage { get; set; } = null!;

    /// <summary>
    /// start listening for fund transaction adjustment events
    /// </summary>
    public Task StartListener()
        => InitializeAsync(CancellationToken.None);

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteAsync(async e => {
            cancellationToken.ThrowIfCancellationRequested();
            e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await e.StartFundListenerAsync(_consumeEvents, HandleEventAsync);
        });

    /// <summary>
    /// stop listening for fund transaction adjustment events
    /// </summary>
    public Task StopListener()
        => StopAsync(CancellationToken.None);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteAsync(async e => {
            cancellationToken.ThrowIfCancellationRequested();
            e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await e.StopFundListenerAsync();
        });

    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);
    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();

    /// <summary>
    /// created adjusted fund transaction
    /// </summary>
    /// <param name="e"></param>
    public Task AdjustFundTransaction(FundTransactionReadModel e)
        => _commandModel.ExecuteAsync(async model =>  {
            model.OnError((_, errorMsg) => OnErrorMessage(errorMsg, "Fund Transaction Adjustment Error"));
            await model.CreateAdjustmentTransactionAsync(
                adjustmentTransactionType: e.TransactionType,
                fundId: e.FundId,
                orderId: e.OrderId,
                tradeId: e.TradeId,
                tradeType: e.TradeType,
                valueDate: e.ValueDate,
                tradeStatus: e.TradeStatus,
                description: e.Description,
                amount: e.Amount,
                balance: _fundBalance);
        });

    /// <summary>
    /// return adjustment transaction
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="comment"></param>
    /// <returns></returns>
    public FundTransactionReadModel GetAdjustmentTransaction(decimal amount, string comment)
        => new (
            transactionId: _fundTransaction.TransactionId,
            transactionType: GetAdjustmentTransactionType(),
            transactionDate: DateTime.Now,
            fundId: _fundTransaction.FundId,
            orderId: _fundTransaction.OrderId,
            tradeId: _fundTransaction.TradeId,
            tradeType: _fundTransaction.TradeType,
            valueDate: _fundTransaction.ValueDate,
            tradeStatus: _fundTransaction.TradeStatus,
            description: comment,
            amount: amount,
            balance: _fundBalance
        );

    /// <summary>
    /// return adjustment transaction type
    /// </summary>
    /// <returns></returns>
    public FundTransactionType GetAdjustmentTransactionType()
        => _fundTransaction.TransactionType switch {
            FundTransactionType.OpeningTrade => FundTransactionType.OpeningTradeAdjustment,
            FundTransactionType.RealizedTradePnl => FundTransactionType.RealizedTradePnlAdjustment,
            FundTransactionType.TradeCommission => FundTransactionType.TradeCommissionAdjustment,
            FundTransactionType.UnrealizedTradePnl => FundTransactionType.UnrealizedTradePnlAdjustment,
            _ => throw new InvalidOperationException($"AdjustFundTransactionReadModel.GetAdjustmentTransactionType: invalid fund transaction type for adjustment '{_fundTransaction.TransactionType}'")
        };

    ValueTask HandleEventAsync(IEvent e)
    {
        if (_commandId != e.CommandId)
            return ValueTask.CompletedTask;
        return e switch
        {
            OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent => FundTransactionAdjustmentCompleted(),
            OpeningTradeFundTransactionAdjustmentCreatedFailEvent o => FundTransactionAdjustmentFailed(o),
            RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent => FundTransactionAdjustmentCompleted(),
            RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent o => FundTransactionAdjustmentFailed(o),
            TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent => FundTransactionAdjustmentCompleted(),
            TradeCommissionFundTransactionAdjustmentCreatedFailEvent o => FundTransactionAdjustmentFailed(o),
            UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent => FundTransactionAdjustmentCompleted(),
            UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent o => FundTransactionAdjustmentFailed(o),
            _ => ValueTask.CompletedTask
        };
    }

    ValueTask FundTransactionAdjustmentCompleted()
    {
        OnFundTransactionAdjustment?.Invoke();
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }

    ValueTask FundTransactionAdjustmentFailed(IErrorEvent e)
    {
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Fund Transaction Adjustment for {e.GetType().Name.Replace("FundTransactionAdjustmentCreatedFailEvent", "")} failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        return ValueTask.CompletedTask;
    }
}
