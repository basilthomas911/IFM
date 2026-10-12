using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;

/// <summary>Defines readonly services required by the VX term-structure Event actor.</summary>
public interface IFuturesVxTermStructureSignalEventContext
    : IEventActorContext<FuturesVxTermStructureSignalEventActor>
{
    ILogger<FuturesVxTermStructureSignalEventActor> Logger { get; }
}
