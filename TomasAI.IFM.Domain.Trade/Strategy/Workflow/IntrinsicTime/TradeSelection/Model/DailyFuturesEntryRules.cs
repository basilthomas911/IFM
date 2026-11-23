using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;

public static class DailyFuturesEntryRules
{
    public static DailyFuturesEntryState? Advance(DailyFuturesEntryState? previous, IntrinsicTimeStrategyWorkflowView view, bool stale)
    {
        if (view.EntityId.Assignment is not { Family: "Future", Timeframe: TimeFrameType.Daily }
            || view.TriggerEvent.FuturesItiSignal is not { TimePeriod: TimeFrameType.Daily } signal) return null;
        var empty = new DailyFuturesEntryState { ContractId = signal.ContractId, ValueDate = signal.ValueDate };
        if (signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged)
        {
            if (stale || signal.DailyAtr is not { } atr || !atr.IsUsableAt(signal.ContractId, signal.IntrinsicTime)
                || signal.Threshold != atr.Value || atr.ContractId != signal.ContractId
                || atr.CalculatedAtUtc > signal.IntrinsicTime || atr.ObservedAtUtc > signal.IntrinsicTime
                || signal.IntrinsicTime - atr.ObservedAtUtc > TimeSpan.FromSeconds(630)) return empty;
            return empty with { DcTriggerId = view.TriggerEventId, DcAtUtc = signal.IntrinsicTime,
                DcPrice = (decimal)signal.IntrinsicPrice, Threshold = (decimal)atr.Value, Atr = atr };
        }
        return previous is not null && previous.ContractId == signal.ContractId && previous.ValueDate == signal.ValueDate
            ? previous : empty;
    }

    public static string? Rejection(IntrinsicTimeStrategyWorkflowView view, RegimeDirection direction, DateTime at)
    {
        if (view.DailyFuturesEntry is not { } entry) return null; // Historical workflows keep their frozen policy.
        if (direction is not (RegimeDirection.Up or RegimeDirection.Down)) return "TS.DAILY.EMA_RANGEBOUND";
        if (!InEntryWindow(at)) return "TS.DAILY.OUTSIDE_ENTRY_WINDOW";
        if (entry.DcTriggerId == Guid.Empty || entry.Atr is null || entry.Threshold <= 0) return "TS.DAILY.NOT_ARMED";
        if (entry.Consumed) return "TS.DAILY.DC_CONSUMED";
        var signal = view.TriggerEvent.FuturesItiSignal!;
        if (signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged) return "TS.DAILY.ARMED_WAIT";
        if (signal.IntrinsicTimeMode is not (IntrinsicTimeModeType.Trending or IntrinsicTimeModeType.TrendExtremeChanged
            or IntrinsicTimeModeType.TrendOvershootExtended)) return "TS.DAILY.NOT_CONTINUATION";
        if (signal.IntrinsicTime <= entry.DcAtUtc || signal.ContractId != entry.ContractId || signal.ValueDate != entry.ValueDate)
            return "TS.DAILY.ARM_IDENTITY";
        var signedMove = (direction == RegimeDirection.Up ? 1 : -1) * ((decimal)signal.IntrinsicPrice - entry.DcPrice);
        return signedMove > entry.Threshold * .25m ? null : "TS.DAILY.ENTRY_DISTANCE";
    }

    public static bool InEntryWindow(DateTime at)
        => DailyFuturesSession.EntryAllowed(at);
}
