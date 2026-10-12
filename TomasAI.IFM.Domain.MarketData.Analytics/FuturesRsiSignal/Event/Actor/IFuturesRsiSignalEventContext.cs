using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesRsiSignalEventActor"/>.</summary>
public interface IFuturesRsiSignalEventContext : IEventActorContext<FuturesRsiSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the MarketDataApi service supplied to the actor context.</summary>
    IMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesRsiSignalEventActor> Logger { get; }
    /// <summary>Gets the BlackboardService service supplied to the actor context.</summary>
    IBlackboardService BlackboardService { get; }
}
