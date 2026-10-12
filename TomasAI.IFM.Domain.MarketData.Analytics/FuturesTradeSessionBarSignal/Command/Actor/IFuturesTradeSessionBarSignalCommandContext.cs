using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Actor;

/// <summary>Defines readonly services required by the trade-session bar signal Command actor.</summary>
public interface IFuturesTradeSessionBarSignalCommandContext
    : ICommandActorContext<FuturesTradeSessionBarSignalCommandActor>
{
    /// <summary>Gets the typed event-source repository.</summary>
    IEventSourceActorStateRepository<FuturesTradeSessionBarSignalCommandState> Repository { get; }
    /// <summary>Gets the durable bar event projector.</summary>
    IEventProjector<FuturesTradeSessionBarSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarSignalCommandActor> Logger { get; }
}
