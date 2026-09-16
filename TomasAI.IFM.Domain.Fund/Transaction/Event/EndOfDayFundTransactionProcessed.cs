using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event;

/// <summary>Handles <see cref="EndOfDayFundTransactionProcessedEvent"/> as a terminal Fund transaction notification.</summary>
public static class EndOfDayFundTransactionProcessed
{
    /// <summary>Acknowledges the event after validating its handler inputs.</summary>
    public static ValueTask ExecuteAsync(this EndOfDayFundTransactionProcessedEvent eventValue, IFundTransactionEventContext context)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.CompletedTask;
    }
}
