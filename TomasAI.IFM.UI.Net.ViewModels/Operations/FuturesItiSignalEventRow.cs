using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>A framework-neutral Strategy view row for one authoritative Futures ITI change.</summary>
public sealed record FuturesItiSignalEventRow(
    Guid NotificationId,
    Guid SourceEventId,
    long EventId,
    Guid CommandId,
    string ContractId,
    DateOnly ValueDate,
    TimeFrameType TimePeriod,
    long SequenceId,
    DateTime OccurredOn,
    DateTime ReceivedOn,
    IntrinsicTimeModeType Mode,
    IntrinsicTimeTrendType Trend,
    double IntrinsicPrice,
    int IntrinsicTimeGroupId,
    double IntrinsicTimeLength,
    double TrendPrice,
    double TrendExtreme,
    double TrendReversal,
    double TrendDelta,
    double TargetDelta,
    double Threshold,
    double UpTrendTrigger,
    double DownTrendTrigger,
    double BandLevel,
    double ReversalLevel,
    IntrinsicTimeTradeState TradeState,
    DateOnly TimeFrameStartValueDate,
    bool IsHistorical)
{
    public string StableIdentity => string.Join(
        '|',
        ContractId,
        TimeFrameStartValueDate,
        TimePeriod,
        SequenceId,
        OccurredOn.Ticks,
        Mode,
        Trend,
        IntrinsicPrice,
        IntrinsicTimeGroupId,
        IntrinsicTimeLength,
        TrendPrice,
        TrendExtreme,
        TrendReversal,
        TrendDelta,
        TargetDelta,
        Threshold,
        UpTrendTrigger,
        DownTrendTrigger,
        BandLevel,
        ReversalLevel,
        TradeState);

    internal static FuturesItiSignalEventRow FromNotification(
        FuturesItiSignalUpdatedNotifyEvent notification)
        => FromSignal(
            notification.FuturesItiSignal,
            notification.Id,
            notification.SourceEventId,
            notification.EventId,
            notification.CommandId,
            notification.ReceivedOn,
            isHistorical: false);

    internal static FuturesItiSignalEventRow FromHistory(FuturesItiSignalV2ReadModel signal)
        => FromSignal(
            signal,
            Guid.Empty,
            Guid.Empty,
            0,
            Guid.Empty,
            signal.IntrinsicTime,
            isHistorical: true);

    static FuturesItiSignalEventRow FromSignal(
        FuturesItiSignalV2ReadModel signal,
        Guid notificationId,
        Guid sourceEventId,
        long eventId,
        Guid commandId,
        DateTime receivedOn,
        bool isHistorical)
        => new(
            notificationId,
            sourceEventId,
            eventId,
            commandId,
            signal.ContractId,
            signal.ValueDate,
            signal.TimePeriod,
            signal.SequenceId,
            signal.IntrinsicTime,
            receivedOn,
            signal.IntrinsicTimeMode,
            signal.IntrinsicTimeTrend,
            signal.IntrinsicPrice,
            signal.IntrinsicTimeGroupId,
            signal.IntrinsicTimeLength,
            signal.TrendPrice,
            signal.TrendExtreme,
            signal.TrendReversal,
            signal.TrendDelta,
            signal.TargetDelta,
            signal.Threshold,
            signal.UpTrendTrigger,
            signal.DownTrendTrigger,
            signal.BandLevel,
            signal.ReversalLevel,
            signal.TradeState,
            signal.TimeFrameStartValueDate == default
                ? signal.ValueDate
                : signal.TimeFrameStartValueDate,
            isHistorical);
}
