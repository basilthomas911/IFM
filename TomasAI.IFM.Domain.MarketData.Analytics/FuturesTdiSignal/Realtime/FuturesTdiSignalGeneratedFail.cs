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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failed);
        global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write41(context.Logger, null, failed.EventName, failed.EntityId, failed.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
