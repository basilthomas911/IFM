using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Handles one source TDI generated message after realtime projection admission.</summary>
public static class FuturesTdiSignalGenerated
{
    /// <summary>Keeps the source lifecycle message observational, as before.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTdiSignalGeneratedEvent @event,
        IFuturesTdiSignalRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(true);
    }
}
