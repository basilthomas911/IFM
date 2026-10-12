using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;

/// <summary>Defines readonly services required by the stateless bar signal Event actor.</summary>
public interface IFuturesTradeSessionBarSignalEventContext
    : IEventActorContext<FuturesTradeSessionBarSignalEventActor>
{
    /// <summary>Gets the server clock used to timestamp realtime publication.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarSignalEventActor> Logger { get; }
}
