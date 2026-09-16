using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event;

/// <summary>Handles <see cref="FundTransactionsEvent"/> as a terminal Fund transaction notification.</summary>
public static class FundTransactions
{
    /// <summary>Acknowledges the event after validating its handler inputs.</summary>
    public static ValueTask ExecuteAsync(this FundTransactionsEvent eventValue, IFundTransactionEventContext context)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.CompletedTask;
    }
}
