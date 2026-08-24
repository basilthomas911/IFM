using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="SpreadDistributionJobEventActor"/>.</summary>
public interface ISpreadDistributionJobEventContext : IEventActorContext<SpreadDistributionJobEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<SpreadDistributionJobEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionJobEventActor"/>.</summary>
public sealed class SpreadDistributionJobEventContext : EventActorContext, IEventActorContext<SpreadDistributionJobEventActor>, ISpreadDistributionJobEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public SpreadDistributionJobEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<SpreadDistributionJobEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, SpreadDistributionJobEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<SpreadDistributionJobEventActor> Logger { get; }
}
