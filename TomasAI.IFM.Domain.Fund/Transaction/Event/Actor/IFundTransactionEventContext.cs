using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundTransactionEventActor"/>.
/// </summary>
public interface IFundTransactionEventContext : IEventActorContext<FundTransactionEventActor>
{
    /// <summary>Gets the actor supervisor used by the event actor runtime.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the logger associated with the event actor.</summary>
    ILogger<FundTransactionEventActor> Logger { get; }
}
