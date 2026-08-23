using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesMarketPriceRealtimeActor"/>.</summary>
public interface IFuturesMarketPriceRealtimeContext : IRealtimeActorContext<FuturesMarketPriceRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesMarketPriceRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesMarketPriceRealtimeActor"/>.</summary>
public sealed class FuturesMarketPriceRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesMarketPriceRealtimeActor>, IFuturesMarketPriceRealtimeContext
{
    /// <summary>Initializes the typed realtime context.</summary>
    public FuturesMarketPriceRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesMarketPriceRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesMarketPriceRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesMarketPriceRealtimeActor> Logger { get; }
}

