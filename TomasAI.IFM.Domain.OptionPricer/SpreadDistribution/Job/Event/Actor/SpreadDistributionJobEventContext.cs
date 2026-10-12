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

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionJobEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class SpreadDistributionJobEventContext : EventActorContext, ISpreadDistributionJobEventContext
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
