using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event.Actor;

/// <summary>Defines the readonly Bollinger event context.</summary>
public interface IFuturesBbSignalEventContext : IEventActorContext<FuturesBbSignalEventActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBbSignalEventActor> Logger { get; }
}
