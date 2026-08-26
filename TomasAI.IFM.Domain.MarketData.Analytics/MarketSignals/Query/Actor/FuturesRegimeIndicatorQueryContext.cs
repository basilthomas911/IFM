using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Query.Actor;

/// <summary>Defines readonly runtime services for latest regime-indicator queries.</summary>
public interface IFuturesRegimeIndicatorQueryContext
    : IQueryActorContext<FuturesRegimeIndicatorQueryActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the typed query logger.</summary>
    ILogger<FuturesRegimeIndicatorQueryActor> Logger { get; }
}

/// <summary>Provides the closed generic query context.</summary>
public sealed class FuturesRegimeIndicatorQueryContext
    : QueryActorContext,
      IQueryActorContext<FuturesRegimeIndicatorQueryActor>,
      IFuturesRegimeIndicatorQueryContext
{
    /// <summary>Initializes the query context.</summary>
    public FuturesRegimeIndicatorQueryContext(
        IActorSupervisor supervisor,
        ILogger<FuturesRegimeIndicatorQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesRegimeIndicatorQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc />
    public ILogger<FuturesRegimeIndicatorQueryActor> Logger { get; }
}
