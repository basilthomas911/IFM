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

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionJobCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class SpreadDistributionJobCommandContext : CommandActorContext, ISpreadDistributionJobCommandContext
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
