using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="SpreadDistributionJobCommandActor"/>.</summary>
public interface ISpreadDistributionJobCommandContext : ICommandActorContext<SpreadDistributionJobCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<SpreadDistributionJobCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<SpreadDistributionJobCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionJobCommandActor"/>.</summary>
public sealed class SpreadDistributionJobCommandContext : CommandActorContext, ICommandActorContext<SpreadDistributionJobCommandActor>, ISpreadDistributionJobCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public SpreadDistributionJobCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<SpreadDistributionJobCommandActor> eventProjector,
        ILogger<SpreadDistributionJobCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, SpreadDistributionJobCommandActor.ActorName))
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
    public IEventProjector<SpreadDistributionJobCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<SpreadDistributionJobCommandActor> Logger { get; }
}
