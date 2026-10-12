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

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesEodDataEventActor"/>.</summary>
public interface IFuturesEodDataEventContext : IEventActorContext<FuturesEodDataEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEodDataEventActor> Logger { get; }
    /// <summary>Gets the BlackboardService service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}

