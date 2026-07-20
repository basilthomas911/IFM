using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Model;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.State;

/// <summary>
/// Represents the persisted state and event application logic for fund transactions handled by the
/// fund transaction command actor.
/// </summary>
/// <remarks>
/// This state object maintains an in-memory collection of fund transactions and provides methods
/// used by the actor to determine existence of transactions and to apply domain events that
/// modify the persisted state. It is intended to be used within the event-sourced actor
/// infrastructure and not directly by application code.
/// </remarks>
public class FundTransactionCommandState(IFundDbContext db)
    :  BaseEventSourceActorState<FundTransactionCommandState>, IEventSourceActorState<FundTransactionCommandState>
{
    /// <summary>
    /// Gets or sets the actor thread identifier for this state instance.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    readonly FundTransactionCollection _fundTransactions = new();

    /// <summary>
    /// Applies a domain event to the current state instance.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply.</param>
    /// <returns>True when the event was successfully applied; otherwise false.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FundTransactionEvent e => On(e),
                FundTransactionsEvent e => On(e),
                EndOfDayFundTransactionProcessedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Determines whether any fund transactions exist for the given fund and order identifiers.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>True if at least one transaction exists for the specified fund and order; otherwise false.</returns>
    internal bool FundTransactionExists(int fundId, int orderId)
        => _fundTransactions.Exists(fundId, orderId);

    /// <summary>
    /// Determines whether no fund transaction exists for the given fund and order identifiers.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>True when no transaction exists for the specified fund and order; otherwise false.</returns>
    internal bool FundTransactionDoesNotExist(int fundId, int orderId)
        => !FundTransactionExists(fundId, orderId);

    public decimal GetCurrentBalance(int fundId)
        => db.GetFundBalanceAsync(fundId).Result;

    public IFundTransaction? GetTransaction(FundTransactionEntityId key, TradeStatus tradeStatus)
        => _fundTransactions.Get(key, tradeStatus);

    public IFundTransaction? GetLastTransaction(int fundId, DateOnly valueDate)
        => _fundTransactions.Get(fundId, valueDate);

    /// <summary>
    /// Dispatch handler for single fund transaction events. Routes to specific handlers based on transaction type.
    /// </summary>
    /// <param name="e">The <see cref="FundTransactionEvent"/> to apply.</param>
    /// <returns>True when the event was handled successfully; otherwise false.</returns>
    bool On(FundTransactionEvent e)
    {
        var fundTx = new FundTransaction(e.FundTransaction);
        _fundTransactions.Add(fundTx);
        return true;
    }

    /// <summary>
    /// Handle a batch event containing multiple fund transactions and/or transaction events.
    /// </summary>
    /// <param name="e">The <see cref="FundTransactionsEvent"/> to apply.</param>
    /// <returns>True when the event was handled successfully; otherwise false.</returns>
    bool On(FundTransactionsEvent e)
    {
        if (e.FundTransactions is not null)
        {
            foreach (var o in e.FundTransactions)
                _fundTransactions.Add(new FundTransaction(o));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handle an end-of-day processed transaction event. This will compute and store the
    /// resulting transaction derived from the provided event.
    /// </summary>
    /// <param name="e">The <see cref="EndOfDayFundTransactionProcessedEvent"/> to apply.</param>
    /// <returns>True when the event was applied successfully; otherwise false.</returns>
    bool On(EndOfDayFundTransactionProcessedEvent e)
    {
        var key = new FundTransactionEntityId(e.FundTransaction.FundId, e.FundTransaction.OrderId);
        var prevTradeTransaction = _fundTransactions?.Get(key, TradeStatus.EndOfDay);
        prevTradeTransaction ??= _fundTransactions?.Get(key, TradeStatus.Open);
        var unrealizedTradePnlTransaction = new FundTransaction(e.FundTransaction);
        unrealizedTradePnlTransaction = unrealizedTradePnlTransaction.SetBalance((prevTradeTransaction?.Balance ?? 0m) + e.FundTransaction.Amount);
        EventInitHelper.SetProperty(e, nameof(EndOfDayFundTransactionProcessedEvent.FundTransaction), unrealizedTradePnlTransaction.ToViewModel());
        _fundTransactions?.Add(unrealizedTradePnlTransaction);
        return true;
    }


}
