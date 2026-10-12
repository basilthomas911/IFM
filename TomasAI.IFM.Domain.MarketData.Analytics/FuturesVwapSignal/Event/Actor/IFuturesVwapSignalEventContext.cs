using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;

/// <summary>Defines readonly services required by the stateless VWAP Event actor.</summary>
public interface IFuturesVwapSignalEventContext : IEventActorContext<FuturesVwapSignalEventActor>
{
    ILogger<FuturesVwapSignalEventActor> Logger { get; }
}
