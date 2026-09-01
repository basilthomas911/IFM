using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;

/// <summary>
/// Defines which asynchronous signal variants can contribute to a Market Outlook snapshot.
/// </summary>
internal static class MarketOutlookComponentEligibility
{
    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesRsiSignalReadModel signal)
        => signal.ContractId == entityId.ContractId
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && signal.PeriodLength == FuturesIntradaySignalActivationProfile.RsiPeriodLength
            && signal.IsWarm
            && signal.RSI >= 0d
            && signal.Metadata is not { IsValid: false };

    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesTdiSignalReadModel signal)
        => signal.ContractId == entityId.ContractId
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && signal.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId;

    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesItiSignalV2ReadModel signal)
        => signal.ContractId == entityId.ContractId
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == TimeFrameType.Daily
            && signal.IntrinsicTimeMode is IntrinsicTimeModeType.TrendDirectionChanged
                or IntrinsicTimeModeType.TrendExtremeChanged
                or IntrinsicTimeModeType.TrendReversalChanged
                or IntrinsicTimeModeType.Trending;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesEmaSignalReadModel signal)
        => signal.Metadata.ContractId == entityId.ContractId
            && signal.Metadata.ValueDate == entityId.ValueDate
            && signal.Metadata.TimeFrame == TimeFrameType.Daily
            && signal.Metadata.IsValid;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesBbSignalReadModel signal)
        => signal.Metadata.ContractId == entityId.ContractId
            && signal.Metadata.ValueDate == entityId.ValueDate
            && signal.Metadata.TimeFrame == TimeFrameType.Daily
            && signal.Metadata.IsValid;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesTradeSignalV2ReadModel signal)
        => signal.ContractId == entityId.ContractId
            && signal.ValueDate == entityId.ValueDate;

    internal static bool IsEligibleAtPublicationBoundary(
        MarketOutlookEntityId entityId,
        FuturesEmaSignalReadModel signal) =>
        IsEligibleAtPublicationBoundary(entityId, signal.Metadata);

    internal static bool IsEligibleAtPublicationBoundary(
        MarketOutlookEntityId entityId,
        FuturesBbSignalReadModel signal) =>
        IsEligibleAtPublicationBoundary(entityId, signal.Metadata);

    static bool IsEligibleAtPublicationBoundary(
        MarketOutlookEntityId entityId,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common.MarketAnalyticsSignalMetadata metadata) =>
        metadata.IsValid
        && metadata.TimeFrame == TimeFrameType.Daily
        && metadata.ValueDate <= entityId.ValueDate
        && (string.Equals(metadata.ContractId, entityId.ContractId, StringComparison.Ordinal)
            || metadata.FuturesSeriesId is { } continuation
            && entityId.ContractId.StartsWith(
                continuation.RootSymbol,
                StringComparison.OrdinalIgnoreCase));

    internal static bool IsEligible(
        MarketOutlookComponentChangedRealtimeEvent source,
        out string reason)
    {
        var eligible = SelectEligible(source, out reason);
        return eligible.FuturesRsiSignal is not null
            || eligible.FuturesTdiSignal is not null
            || eligible.FuturesItiSignal is not null
            || eligible.VixFuturesPrice > 0
            || eligible.FuturesEmaSignal is not null
            || eligible.FuturesBbSignal is not null
            || eligible.FuturesTradeSignal is not null;
    }

    /// <summary>
    /// Independently admits every valid component in a composite message. Invalid siblings are
    /// removed and reported; they never suppress a valid component carried beside them.
    /// </summary>
    internal static MarketOutlookComponentChangedRealtimeEvent SelectEligible(
        MarketOutlookComponentChangedRealtimeEvent source,
        out string reason)
    {
        List<string> rejected = [];
        var rsi = source.FuturesRsiSignal;
        if (rsi is not null && !IsEligible(source.EntityId, rsi))
        {
            rejected.Add("rsi-profile");
            rsi = null;
        }
        var tdi = source.FuturesTdiSignal;
        if (tdi is not null && !IsEligible(source.EntityId, tdi))
        {
            rejected.Add("tdi-profile");
            tdi = null;
        }
        var iti = source.FuturesItiSignal;
        if (iti is not null && !IsEligible(source.EntityId, iti))
        {
            rejected.Add("iti-profile");
            iti = null;
        }
        var vix = source.VixFuturesPrice;
        if (vix < 0.01m || vix > 200m)
        {
            if (vix != 0)
                rejected.Add("vx-range");
            vix = 0;
        }
        var ema = source.FuturesEmaSignal;
        if (ema is not null && !IsEligible(source.EntityId, ema))
        {
            rejected.Add("ema-profile");
            ema = null;
        }
        var bb = source.FuturesBbSignal;
        if (bb is not null && !IsEligible(source.EntityId, bb))
        {
            rejected.Add("bb-profile");
            bb = null;
        }
        var tradeSignal = source.FuturesTradeSignal;
        if (tradeSignal is not null && !IsEligible(source.EntityId, tradeSignal))
        {
            rejected.Add("trade-signal-identity");
            tradeSignal = null;
        }
        reason = rejected.Count == 0 ? string.Empty : string.Join(", ", rejected);
        return source with
        {
            FuturesRsiSignal = rsi,
            FuturesTdiSignal = tdi,
            FuturesItiSignal = iti,
            VixFuturesPrice = vix,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb,
            FuturesTradeSignal = tradeSignal
        };
    }
}
