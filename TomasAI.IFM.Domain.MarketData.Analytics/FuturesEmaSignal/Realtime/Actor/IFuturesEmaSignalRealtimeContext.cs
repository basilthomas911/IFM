using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;

/// <summary>Defines the readonly EMA realtime context.</summary>
public interface IFuturesEmaSignalRealtimeContext : IRealtimeActorContext<FuturesEmaSignalRealtimeActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalRealtimeActor> Logger { get; }
}
