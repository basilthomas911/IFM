using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;

/// <summary>
/// Defines which asynchronous signal variants can contribute to a Market Outlook snapshot.
/// </summary>
internal static class MarketOutlookComponentEligibility
{
    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesRsiSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && signal.PeriodLength == FuturesIntradaySignalActivationProfile.RsiPeriodLength
            && signal.IsWarm
            && signal.RSI >= 0d
            && signal.Metadata is not { IsValid: false };

    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesTdiSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == FuturesTradeSignalPrerequisites.SignalTimePeriod
            && string.Equals(signal.ConfigurationId,FuturesTdiConfiguration.StandardConfigurationId,StringComparison.Ordinal);

    internal static bool IsEligible(
        MarketOutlookEntityId entityId,
        FuturesItiSignalV2ReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == TimeFrameType.Daily
            && signal.IntrinsicTimeMode is IntrinsicTimeModeType.TrendDirectionChanged
                or IntrinsicTimeModeType.TrendExtremeChanged
                or IntrinsicTimeModeType.TrendReversalChanged
                or IntrinsicTimeModeType.Trending;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesEmaSignalReadModel signal)
        => string.Equals(signal.Metadata.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.Metadata.ValueDate == entityId.ValueDate
            && signal.Metadata.TimeFrame == TimeFrameType.Daily
            && signal.Metadata.IsValid;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesBbSignalReadModel signal)
        => string.Equals(signal.Metadata.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.Metadata.ValueDate == entityId.ValueDate
            && signal.Metadata.TimeFrame == TimeFrameType.Daily
            && signal.Metadata.IsValid;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesTradeSignalV2ReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesVwapSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.IsWarm
            && signal.Vwap is > 0m;

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesAdxSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == TimeFrameType.FiveMinutes
            && signal.PeriodLength == FuturesIntradaySignalActivationProfile.AdxPeriodLength
            && signal.IsWarm
            && signal.Metadata is not { IsValid: false };

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesAtrSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == TimeFrameType.FiveMinutes
            && signal.PeriodLength == FuturesIntradaySignalActivationProfile.AtrPeriodLength
            && signal.IsWarm
            && signal.AtrValue > 0d
            && signal.AtrRatio is not null
            && signal.Metadata is not { IsValid: false };

    internal static bool IsEligible(MarketOutlookEntityId entityId, FuturesMacdSignalReadModel signal)
        => string.Equals(signal.ContractId,entityId.ContractId,StringComparison.Ordinal)
            && signal.ValueDate == entityId.ValueDate
            && signal.TimePeriod == TimeFrameType.FiveMinutes
            && signal.SignalEmaPeriod == FuturesMacdConfiguration.ConventionalSignalEmaPeriod
            && signal.FastEmaPeriod == FuturesMacdConfiguration.ConventionalFastEmaPeriod
            && signal.SlowEmaPeriod == FuturesMacdConfiguration.ConventionalSlowEmaPeriod
            && signal.IsWarm
            && signal.Metadata is not { IsValid: false };

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
            || eligible.FuturesTradeSignal is not null
            || eligible.FuturesVwapSignal is not null
            || eligible.FuturesAdxSignal is not null
            || eligible.FuturesAtrSignal is not null
            || eligible.FuturesMacdSignal is not null;
    }

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

    /// <summary>
    /// Independently admits every valid component in a composite message. Invalid siblings are
    /// removed and reported; they never suppress a valid component carried beside them.
    /// </summary>
    internal static MarketOutlookComponentChangedRealtimeEvent SelectEligible(
        MarketOutlookComponentChangedRealtimeEvent source,
        out string reason)
    {
        List<string> rejected = [];
        var rsi = SelectComponent(source.FuturesRsiSignal, source.EntityId, IsEligible, rejected, "rsi-profile");
        var tdi = SelectComponent(source.FuturesTdiSignal, source.EntityId, IsEligible, rejected, "tdi-profile");
        var iti = SelectComponent(source.FuturesItiSignal, source.EntityId, IsEligible, rejected, "iti-profile");
        var vix = source.VixFuturesPrice;
        if (vix < 0.01m || vix > 200m)
        {
            if (vix != 0)
                rejected.Add("vx-range");
            vix = 0;
        }
        var ema = SelectComponent(source.FuturesEmaSignal, source.EntityId, IsEligible, rejected, "ema-profile");
        var bb = SelectComponent(source.FuturesBbSignal, source.EntityId, IsEligible, rejected, "bb-profile");
        var tradeSignal = SelectComponent(source.FuturesTradeSignal, source.EntityId, IsEligible, rejected, "trade-signal-identity");
        var vwap = SelectComponent(source.FuturesVwapSignal, source.EntityId, IsEligible, rejected, "vwap-profile");
        var adx = SelectComponent(source.FuturesAdxSignal, source.EntityId, IsEligible, rejected, "adx-profile");
        var atr = SelectComponent(source.FuturesAtrSignal, source.EntityId, IsEligible, rejected, "atr-profile");
        var macd = SelectComponent(source.FuturesMacdSignal, source.EntityId, IsEligible, rejected, "macd-profile");
        reason = rejected.Count == 0 ? string.Empty : string.Join(", ", rejected);
        return source with
        {
            FuturesRsiSignal = rsi,
            FuturesTdiSignal = tdi,
            FuturesItiSignal = iti,
            VixFuturesPrice = vix,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb,
            FuturesTradeSignal = tradeSignal,
            FuturesVwapSignal = vwap,
            FuturesAdxSignal = adx,
            FuturesAtrSignal = atr,
            FuturesMacdSignal = macd
        };
    }
    /// <summary>Removes an incompatible component and records its exact rejection code; valid siblings remain eligible.</summary>
    static T? SelectComponent<T>(T? component, MarketOutlookEntityId entityId,
        Func<MarketOutlookEntityId, T, bool> eligible, List<string> rejected, string reason) where T : class
    {
        if (component is null || eligible(entityId, component)) return component;
        rejected.Add(reason);
        return null;
    }

}
