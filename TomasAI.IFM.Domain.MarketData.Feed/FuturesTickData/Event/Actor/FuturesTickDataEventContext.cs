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

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesTickDataEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTickDataEventContext : EventActorContext, IFuturesTickDataEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesTickDataEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesTickDataEventActor> logger,
        ApplicationMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesTickDataEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTickDataEventActor> Logger { get; }
    /// <inheritdoc/>
    public ApplicationMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}

