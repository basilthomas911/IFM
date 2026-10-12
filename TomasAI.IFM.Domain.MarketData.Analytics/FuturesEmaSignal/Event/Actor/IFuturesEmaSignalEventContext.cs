using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;

/// <summary>Defines the readonly EMA event context.</summary>
public interface IFuturesEmaSignalEventContext : IEventActorContext<FuturesEmaSignalEventActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalEventActor> Logger { get; }
}
