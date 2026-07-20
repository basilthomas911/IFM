using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command;

public static class CreateFundTransaction
{
    /// <summary>
    /// Handle a single <see cref="CreateFundTransactionCommand"/> by building the corresponding
    /// <see cref="FundTransactionEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The command that requests creation of a fund transaction.</param>
    /// <param name="state">The command state used to persist and validate the event.</param>
    /// <returns>True when the state was updated successfully; otherwise false.</returns>
    public static ServiceResult<GuidResult> Execute(this CreateFundTransactionCommand e, FundTransactionCommandState state)
    {
        if (e.FundTransaction is null)
            return e.UpdateFailed($"{e.CommandName}: fund transaction is null");

        var fundTransactionEvent = e.FundTransaction.CreateFundTransactionEvent(e.CommandId, e.OriginatedOn, e.OriginatedBy);
        if (fundTransactionEvent is not null)
        {
            var fundTransaction = fundTransactionEvent.FundTransaction.TransactionType switch
            {
                FundTransactionType.OpeningTrade => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.OpeningTradeAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.TradeCommission => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.TradeCommissionAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.UnrealizedTradePnl => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.UnrealizedTradePnlAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.RealizedTradePnl => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.RealizedTradePnlAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.CashDeposit => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.CashDepositAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                FundTransactionType.CashWithdrawal => fundTransactionEvent.UpdateCashWithdrawalBalance(state),
                FundTransactionType.CashWithdrawalAdjustment => fundTransactionEvent.UpdateFundTransactionBalance(state),
                _ => default
            };
            if (fundTransaction is null)
                return e.UpdateFailed($"CreateFundTransaction: unsupported fund transaction type: {fundTransactionEvent.FundTransaction.TransactionType}");
            EventInitHelper.SetProperty(fundTransactionEvent, nameof(FundTransactionEvent.FundTransaction), fundTransaction);
        }
        else
            return e.UpdateFailed($"CreateFundTransaction: fund transaction {e.FundTransaction.TransactionType} does not exist");
        return e.UpdatedOk(() =>  state.Update(fundTransactionEvent!, e));
    }

    /// <summary>
    /// create fund transaction event from fund transaction
    /// </summary>
    /// <param name="fundTransaction"></param>
    /// <param name="commandId"></param>
    /// <param name="createdOn"></param>
    /// <param name="createdBy"></param>
    /// <returns></returns>
    public static FundTransactionEvent CreateFundTransactionEvent(this FundTransactionReadModel fundTransaction, Guid commandId, DateTime createdOn, string createdBy)
       => fundTransaction.TransactionType switch
       {
           FundTransactionType.OpeningTrade => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.TradeCommission => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.UnrealizedTradePnl => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.RealizedTradePnl => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.CashDeposit => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.CashWithdrawal => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.OpeningTradeAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.TradeCommissionAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.UnrealizedTradePnlAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.RealizedTradePnlAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.CashDepositAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           FundTransactionType.CashWithdrawalAdjustment => NewFundTransactionEvent(fundTransaction, commandId, createdOn, createdBy),
           _ =>default
       };

    /// <summary>
    /// Creates a new instance of a fund transaction event using the specified transaction details, command identifier,
    /// creation timestamp, and creator information.
    /// </summary>
    /// <param name="fundTransaction">The fund transaction view model containing the details of the transaction to associate with the event. Cannot be
    /// null.</param>
    /// <param name="commandId">The unique identifier of the command that initiated the event.</param>
    /// <param name="createdOn">The date and time when the event was created, in UTC.</param>
    /// <param name="createdBy">The identifier of the user or system that created the event. Cannot be null or empty.</param>
    /// <returns>A new instance of <see cref="FundTransactionEvent"/> populated with the provided transaction, command, and
    /// creation details.</returns>
    static FundTransactionEvent NewFundTransactionEvent(FundTransactionReadModel fundTransaction, Guid commandId, DateTime createdOn, string createdBy)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FundTransactionEvent.Actor, FundTransactionEvent.Verb, fundTransaction.EntityId.Format()),
            EntityId = fundTransaction.EntityId,
            CommandId = commandId,
            FundTransaction = fundTransaction,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };

    /// <summary>
    /// Updates the balance of the fund transaction based on the current balance of the fund and the transaction amount.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    static FundTransactionReadModel UpdateFundTransactionBalance(this FundTransactionEvent e, FundTransactionCommandState state)
    {
        var currentBalance = state.GetCurrentBalance(e.FundTransaction.FundId);
        return e.FundTransaction with { Balance = currentBalance + e.FundTransaction.Amount };
    }

    /// <summary>
    /// Creates a cash withdrawal fund transaction based on the current balance of the fund.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    static FundTransactionReadModel UpdateCashWithdrawalBalance(this FundTransactionEvent e, FundTransactionCommandState state)
    {
        var currentBalance = state.GetCurrentBalance(e.FundTransaction.FundId);
        return e.FundTransaction with { Balance = currentBalance - e.FundTransaction.Amount };
    }
}
