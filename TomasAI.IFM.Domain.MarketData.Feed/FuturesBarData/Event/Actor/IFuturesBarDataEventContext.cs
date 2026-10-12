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

/// <summary>Defines the runtime services required by <see cref="FuturesBarDataEventActor"/>.</summary>
public interface IFuturesBarDataEventContext : IEventActorContext<FuturesBarDataEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBarDataEventActor> Logger { get; }
    /// <summary>Gets the FuturesBarDataTimer service.</summary>
    IFuturesBarDataTimer FuturesBarDataTimer { get; }
    /// <summary>Gets the MarketDataApi service.</summary>
    ApplicationMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}

