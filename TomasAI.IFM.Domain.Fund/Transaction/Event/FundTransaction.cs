using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event;

/// <summary>Handles <see cref="FundTransactionEvent"/> as a terminal Fund transaction notification.</summary>
public static class FundTransaction
{
    /// <summary>Acknowledges the event after validating its handler inputs.</summary>
    public static ValueTask ExecuteAsync(this FundTransactionEvent eventValue, IFundTransactionEventContext context)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.CompletedTask;
    }
}
