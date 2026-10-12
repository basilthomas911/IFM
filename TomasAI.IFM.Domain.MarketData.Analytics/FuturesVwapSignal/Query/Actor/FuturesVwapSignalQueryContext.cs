using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;

/// <summary>Provides the closed generic VWAP Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVwapSignalQueryContext : QueryActorContext,
IFuturesVwapSignalQueryContext
{
    /// <summary>Initializes the readonly Query context.</summary>
    public FuturesVwapSignalQueryContext(
        IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<FuturesVwapSignalQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesVwapSignalQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public ILogger<FuturesVwapSignalQueryActor> Logger { get; }
}
