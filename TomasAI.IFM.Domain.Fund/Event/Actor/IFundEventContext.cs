using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Event.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundEventActor"/> in addition to the shared event actor context
/// operations.
/// </summary>
public interface IFundEventContext : IEventActorContext<FundEventActor>
{
    /// <summary>
    /// Gets the actor supervisor used by the Fund event actor runtime.
    /// </summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>
    /// Gets the logger associated with the Fund event actor.
    /// </summary>
    ILogger<FundEventActor> Logger { get; }
}
