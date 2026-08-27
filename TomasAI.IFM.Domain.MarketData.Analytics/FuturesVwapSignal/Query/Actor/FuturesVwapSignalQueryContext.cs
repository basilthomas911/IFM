using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;

/// <summary>Defines readonly services required by the VWAP Query actor.</summary>
public interface IFuturesVwapSignalQueryContext : IQueryActorContext<FuturesVwapSignalQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesVwapSignalQueryActor> Logger { get; }
}

/// <summary>Provides the closed generic VWAP Query context.</summary>
public sealed class FuturesVwapSignalQueryContext : QueryActorContext,
    IQueryActorContext<FuturesVwapSignalQueryActor>, IFuturesVwapSignalQueryContext
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
