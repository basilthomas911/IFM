using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Defines the readonly runtime services required by <see cref="TradePlanQueryActor"/>.</summary>
public interface ITradePlanQueryContext : IQueryActorContext<TradePlanQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<TradePlanQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TradePlanQueryActor"/>.</summary>
public sealed class TradePlanQueryContext : QueryActorContext, IQueryActorContext<TradePlanQueryActor>, ITradePlanQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public TradePlanQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<TradePlanQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, TradePlanQueryActor.ActorName))
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
    public ILogger<TradePlanQueryActor> Logger { get; }
}
