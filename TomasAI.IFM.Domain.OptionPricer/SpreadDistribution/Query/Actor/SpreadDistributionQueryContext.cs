using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="SpreadDistributionQueryActor"/>.</summary>
public interface ISpreadDistributionQueryContext : IQueryActorContext<SpreadDistributionQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<SpreadDistributionQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="SpreadDistributionQueryActor"/>.</summary>
public sealed class SpreadDistributionQueryContext : QueryActorContext, IQueryActorContext<SpreadDistributionQueryActor>, ISpreadDistributionQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public SpreadDistributionQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<SpreadDistributionQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, SpreadDistributionQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<SpreadDistributionQueryActor> Logger { get; }
}
