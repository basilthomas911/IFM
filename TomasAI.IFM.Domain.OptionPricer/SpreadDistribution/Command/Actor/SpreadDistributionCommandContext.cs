using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared.Validation;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="SpreadDistributionCommandActor"/>.</summary>
public interface ISpreadDistributionCommandContext : ICommandActorContext<SpreadDistributionCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<SpreadDistributionCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<SpreadDistributionCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionCommandActor"/>.</summary>
public sealed class SpreadDistributionCommandContext : CommandActorContext, ICommandActorContext<SpreadDistributionCommandActor>, ISpreadDistributionCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public SpreadDistributionCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<SpreadDistributionCommandActor> eventProjector,
        ILogger<SpreadDistributionCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, SpreadDistributionCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IEventProjector<SpreadDistributionCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<SpreadDistributionCommandActor> Logger { get; }
}
