using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="YieldCurveRateQueryActor"/>.</summary>
public interface IYieldCurveRateQueryContext : IQueryActorContext<YieldCurveRateQueryActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<YieldCurveRateQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="YieldCurveRateQueryActor"/>.</summary>
public sealed class YieldCurveRateQueryContext : QueryActorContext, IQueryActorContext<YieldCurveRateQueryActor>, IYieldCurveRateQueryContext
{
    /// <summary>Initializes the context.</summary>
    public YieldCurveRateQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<YieldCurveRateQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, YieldCurveRateQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<YieldCurveRateQueryActor> Logger { get; }
}
