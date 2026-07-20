using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command;

public static class CreateFundTransactions
{
    /// <summary>
    /// Handle a <see cref="CreateFundTransactionsCommand"/> (batch create) by producing a
    /// <see cref="FundTransactionsEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The batch create command containing fund transactions to create.</param>
    /// <param name="state">The command state used to persist and validate the event.</param>
    /// <returns>True when the state was updated successfully; otherwise false.</returns>
    public static ServiceResult<GuidResult> Execute(this CreateFundTransactionsCommand e, FundTransactionCommandState state)
    {
        var currentBalance = state.GetCurrentBalance(e.FundTransactions[0].FundId);
        try
        {
            FundTransactionReadModel[] fundTransactions = [.. CreateFundTransactions(e, currentBalance)];
            return e.UpdatedOk( () => state.Update(e.CreateFundTransactionsEvent(fundTransactions), e));
        }
        catch (Exception ex)
        {
            return e.UpdateFailed(ex.Message);
        }

        /// <summary>
        /// Creates fund transactions based on the provided command and current balance.
        /// </summary>
        /// <param name="e">The create fund transactions command.</param>
        /// <param name="currentBalance">The current balance of the fund.</param>
        /// <returns>An enumerable of created fund transaction read models.</returns>
        static IEnumerable<FundTransactionReadModel> CreateFundTransactions(CreateFundTransactionsCommand e, decimal currentBalance)
        {
            foreach (var fundTx in e.FundTransactions)
            {
                var updateFundTx = fundTx.TransactionType switch
                {
                    FundTransactionType.OpeningTrade => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.OpeningTradeAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.TradeCommission => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.TradeCommissionAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.UnrealizedTradePnl => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.UnrealizedTradePnlAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.RealizedTradePnl => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.RealizedTradePnlAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.CashDeposit => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.CashDepositAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    FundTransactionType.CashWithdrawal => CreateCashWithdrawal(fundTx, currentBalance),
                    FundTransactionType.CashWithdrawalAdjustment => UpdateFundTransaction(fundTx, currentBalance),
                    _ => throw new CreateFundTransactionException($"Unsupported fund transaction type: {fundTx.TransactionType}"),
                };
                yield return updateFundTx;
                currentBalance = updateFundTx.Balance;
            }
        }

        /// <summary>
        /// Updates the balance of the fund transaction based on the current balance of the fund and the transaction amount.
        /// </summary>
        /// <param name="e">The fund transaction view model containing the details of the transaction to update. Cannot be null.</param>
        /// <param name="currentBalance">The current balance of the fund.</param>
        /// <returns>A new instance of <see cref="FundTransactionReadModel"/> with the updated balance.</returns>
        static FundTransactionReadModel UpdateFundTransaction(FundTransactionReadModel e, decimal currentBalance)
            => e with { Balance = currentBalance + e.Amount };

        /// <summary>
        /// Creates a cash withdrawal fund transaction based on the current balance of the fund.
        /// </summary>
        /// <param name="e">The fund transaction view model containing the details of the transaction to create. Cannot be null.</param>
        /// <param name="currentBalance">The current balance of the fund.</param>
        /// <returns>A new instance of <see cref="FundTransactionReadModel"/> representing the cash withdrawal transaction.</returns>
        static FundTransactionReadModel CreateCashWithdrawal(FundTransactionReadModel e, decimal currentBalance)
            => e with { Balance = currentBalance - e.Amount };
    }

    /// <summary>
    /// Creates a <see cref="FundTransactionsEvent"/> based on the provided command and created fund transactions.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="fundTransactions"></param>
    /// <returns></returns>
    static FundTransactionsEvent CreateFundTransactionsEvent(this CreateFundTransactionsCommand e, FundTransactionReadModel[] fundTransactions)
            => new()
            {
                CommandId = e.CommandId,
                Subject = new ActorSubject(ActorType.Event, FundTransactionsEvent.Actor, FundTransactionsEvent.Verb, e.EntityId.Format()),
                EntityId = e.EntityId,
                FundTransactions = fundTransactions,
                CreatedOn = e.OriginatedOn,
                CreatedBy = e.OriginatedBy
            };
}
