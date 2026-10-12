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

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesBarDataEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesBarDataEventContext : EventActorContext, IFuturesBarDataEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesBarDataEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesBarDataEventActor> logger,
        IFuturesBarDataTimer futuresBarDataTimer,
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesBarDataEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        FuturesBarDataTimer = IsArgumentNull.Set(futuresBarDataTimer);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesBarDataEventActor> Logger { get; }
    /// <inheritdoc/>
    public IFuturesBarDataTimer FuturesBarDataTimer { get; }
    /// <inheritdoc/>
    public ApplicationMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}

