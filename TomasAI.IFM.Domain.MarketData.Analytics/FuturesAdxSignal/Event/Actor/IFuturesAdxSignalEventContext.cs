using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesAdxSignalEventActor"/>.</summary>
public interface IFuturesAdxSignalEventContext : IEventActorContext<FuturesAdxSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesAdxSignalEventActor> Logger { get; }
    /// <summary>Gets the MarketDataApi service supplied to the actor context.</summary>
    IMarketDataApi MarketDataApi { get; }
}
