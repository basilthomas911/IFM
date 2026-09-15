using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Handles one failed TDI realtime projection.</summary>
public static class FuturesTdiSignalGeneratedFail
{
    /// <summary>Logs the typed failure while preserving its terminal no-retry behavior.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTdiSignalGeneratedFailEvent failed,
        IFuturesTdiSignalRealtimeContext context)
    {
        context.Logger.LogError(
            "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
            failed.EventName, failed.EntityId, failed.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
