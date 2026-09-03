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

/// <summary>Defines the runtime services required by <see cref="MarketDataFeedEventActor"/>.</summary>
public interface IMarketDataFeedEventContext : IEventActorContext<MarketDataFeedEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<MarketDataFeedEventActor> Logger { get; }
    /// <summary>Gets the sole lifecycle-owner request boundary.</summary>
    IMarketDataLifecycleRequests MarketDataLifecycle { get; }
    /// <summary>Gets the OptionTradeLiveFeedMap service.</summary>
    IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; }
    /// <summary>Gets the BlackboardService service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="MarketDataFeedEventActor"/>.</summary>
public sealed class MarketDataFeedEventContext : EventActorContext, IEventActorContext<MarketDataFeedEventActor>, IMarketDataFeedEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public MarketDataFeedEventContext(
        IActorSupervisor supervisor,
        ILogger<MarketDataFeedEventActor> logger,
        IMarketDataLifecycleRequests marketDataLifecycle,
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
    public IMarketDataLifecycleRequests MarketDataLifecycle { get; }
    /// <inheritdoc/>
    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}
