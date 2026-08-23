using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesClosingPriceEventActor"/>.</summary>
public interface IFuturesClosingPriceEventContext : IEventActorContext<FuturesClosingPriceEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesClosingPriceEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesClosingPriceEventActor"/>.</summary>
public sealed class FuturesClosingPriceEventContext : EventActorContext, IEventActorContext<FuturesClosingPriceEventActor>, IFuturesClosingPriceEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesClosingPriceEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesClosingPriceEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesClosingPriceEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesClosingPriceEventActor> Logger { get; }
}

