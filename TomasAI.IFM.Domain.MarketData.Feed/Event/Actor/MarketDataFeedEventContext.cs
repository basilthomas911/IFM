using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="MarketDataFeedEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class MarketDataFeedEventContext : EventActorContext, IMarketDataFeedEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public MarketDataFeedEventContext(
        IActorSupervisor supervisor,
        ILogger<MarketDataFeedEventActor> logger,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataLifecycle,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Event, MarketDataFeedEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        MarketDataLifecycle = IsArgumentNull.Set(marketDataLifecycle);
        OptionTradeLiveFeedMap = IsArgumentNull.Set(optionTradeLiveFeedMap);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<MarketDataFeedEventActor> Logger { get; }
    /// <inheritdoc/>
    public TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi MarketDataLifecycle { get; }
    /// <inheritdoc/>
    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}
