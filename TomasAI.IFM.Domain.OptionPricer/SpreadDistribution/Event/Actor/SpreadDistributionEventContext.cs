using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="SpreadDistributionEventActor"/>.</summary>
public interface ISpreadDistributionEventContext : IEventActorContext<SpreadDistributionEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<SpreadDistributionEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionEventActor"/>.</summary>
public sealed class SpreadDistributionEventContext : EventActorContext, IEventActorContext<SpreadDistributionEventActor>, ISpreadDistributionEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public SpreadDistributionEventContext(
        IActorSupervisor supervisor,
        ILogger<SpreadDistributionEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, SpreadDistributionEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<SpreadDistributionEventActor> Logger { get; }
}
