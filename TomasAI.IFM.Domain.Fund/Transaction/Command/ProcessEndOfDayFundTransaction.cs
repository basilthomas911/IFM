using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command;

public static class ProcessEndOfDayFundTransaction
{
    /// <summary>
    /// Process an end-of-day fund transaction command.
    /// </summary>
    /// <param name="e">The <see cref="ProcessEndOfDayFundTransactionCommand"/> containing the transaction to process.</param>
    /// <param name="state">The command state used to validate existence and to persist the resulting event.</param>
    /// <returns>True when the state was updated successfully; otherwise false.</returns>
    /// <exception cref="ProcessEndOfDayFundTransactionException">Thrown when the fund transaction does not exist or the transaction type is invalid.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ProcessEndOfDayFundTransactionCommand e, FundTransactionCommandState state)
    {
        if (state.FundTransactionDoesNotExist(e.FundTransaction.FundId, e.FundTransaction.OrderId))
            return e.UpdateFailed(FundTransactionDoesNotExist(e));
        if (e.FundTransaction.TransactionType != FundTransactionType.UnrealizedTradePnl)
            return e.UpdateFailed(FundTransactionTypeMustBeUnrealizedTradePnl(e));

        var currentBalance = await state.GetCurrentBalanceAsync(e.FundTransaction.FundId).ConfigureAwait(false);
        var transaction = e.FundTransaction with { Balance = currentBalance + e.FundTransaction.Amount };
        return e.UpdatedOk(() => state.Update(e.CreateEndOfDayFundTransactionProcessedEvent(transaction), e));
    }
    
    /// <summary>
    /// Generates a message indicating that the fund transaction does not exist.
    /// </summary>
    /// <param name="e">The process end-of-day fund transaction command.</param>
    /// <returns>The error message.</returns>
    static string FundTransactionDoesNotExist(ProcessEndOfDayFundTransactionCommand e)
        => $"{e.CommandName}: fundId: {e.FundTransaction.FundId} orderId: {e.FundTransaction.OrderId} does not exist";

    /// <summary>
    /// Generates a message indicating that the fund transaction type must be UnrealizedTradePnl.
    /// </summary>
    /// <param name="e">The process end-of-day fund transaction command.</param>
    /// <returns>The error message.</returns>
    static string FundTransactionTypeMustBeUnrealizedTradePnl(ProcessEndOfDayFundTransactionCommand e)
        => $"{e.CommandName}: transaction type must be UnrealizedTradePnl: {e.FundTransaction.TransactionType}";
    
    /// <summary>
    /// Creates an <see cref="EndOfDayFundTransactionProcessedEvent"/> based on the given command and fund transaction.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    static EndOfDayFundTransactionProcessedEvent CreateEndOfDayFundTransactionProcessedEvent(
       this ProcessEndOfDayFundTransactionCommand e, FundTransactionReadModel fundTransaction)
       => new()
       {
           CommandId = e.CommandId,
           Subject = new ActorSubject(ActorType.Event, EndOfDayFundTransactionProcessedEvent.Actor, EndOfDayFundTransactionProcessedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           CorrelationId = e.CorrelationId == Guid.Empty ? e.CommandId : e.CorrelationId,
           FundTransaction = fundTransaction,
           CreatedOn = e.OriginatedOn,
           CreatedBy = e.OriginatedBy
       };
}
