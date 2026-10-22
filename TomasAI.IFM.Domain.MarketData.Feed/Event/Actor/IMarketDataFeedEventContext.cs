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
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi MarketDataLifecycle { get; }
    /// <summary>Gets the OptionTradeLiveFeedMap service.</summary>
    IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; }
    /// <summary>Gets the BlackboardService service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}
