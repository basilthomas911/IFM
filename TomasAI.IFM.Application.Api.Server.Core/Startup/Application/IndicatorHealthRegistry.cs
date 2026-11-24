using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Identifies an active producer output independently of whether it has published since restart.</summary>
public sealed record IndicatorHealthEntry(string ContractId, TimeFrameType TimeFrame, string Indicator,
    bool Warm, bool Valid, DateTimeOffset? SourceAsOfUtc, DateTimeOffset? ExpectedAsOfUtc, string Reason)
{
    /// <summary>Strategy-specific qualification is enforced at admission, independently of other horizons.</summary>
    public bool AffectsGlobalReadiness { get; init; } = true;
    /// <summary>Gets whether both initialization and current observation coverage qualify.</summary>
    public bool Ready => Warm && Valid && SourceAsOfUtc is { } at
        && (ExpectedAsOfUtc is null || at >= ExpectedAsOfUtc) && Reason.Length == 0;
}

/// <summary>Provides one interval policy shared by startup reconciliation and recurring audits.</summary>
public static class IndicatorHealthRegistry
{
    /// <summary>Identifies every enum interval explicitly, including intervals with no active runtime producer.</summary>
    public static IReadOnlyDictionary<TimeFrameType, string> TimeFrameCoverage { get; } =
        Enum.GetValues<TimeFrameType>().ToDictionary(frame => frame, frame => frame == TimeFrameType.None ? "Invalid"
            : FuturesIntradaySignalActivationProfile.TimeFrames.Contains(frame) ? "Active intraday producers"
            : frame == TimeFrameType.Daily ? "Active Daily ITI; EMA-only regime uses FiveMinutes baseline"
            : frame == TimeFrameType.Weekly ? "Active Weekly ITI; EMA-only regime uses FourHours baseline"
            : frame == TimeFrameType.Monthly ? "Active Monthly ITI; EMA-only regime uses Daily baseline"
            : "Inactive: no automatically configured indicator producer");
    /// <summary>Every automatically attached intraday processor must be audited, including five-minute Market Outlook inputs.</summary>
    public static IReadOnlyList<TimeFrameType> IntradayFrames => FuturesIntradaySignalActivationProfile.TimeFrames;

    /// <summary>Optional experimental volatility metrics have no active producer and are not readiness dependencies.</summary>
    public static IReadOnlyList<string> InactiveMetrics { get; } = Array.AsReadOnly(new[]
        { "RealizedVolatilityPercentile", "PriorVolatilityComposite" });

    /// <summary>Uses exchange-calendar completed intervals, allowing the existing thirty-second publication budget.</summary>
    public static DateTimeOffset MinimumSourceTime(TimeFrameType frame, DateTimeOffset now, IMarketSessionCalendar calendar)
        => RsiHistoricalSeedWindowModel.Create(frame, 1, now.AddSeconds(-30), calendar)[0].StartUtc;

    /// <summary>Daily outputs belong to the last completed exchange session, never an unfinished current day.</summary>
    public static DateOnly ObservationDate(TimeFrameType frame, DateOnly activeDate, DateTimeOffset now, IMarketSessionCalendar calendar)
        => frame == TimeFrameType.Daily
            ? RsiHistoricalSeedWindowModel.Create(frame, 1, now.AddSeconds(-30), calendar)[0].ValueDate
            : activeDate;

    /// <summary>Builds an explicit failure rather than treating a missing projection as a warm indicator.</summary>
    public static IndicatorHealthEntry Evaluate(string contract, TimeFrameType frame, string indicator,
        bool warm, bool valid, DateTimeOffset? source, DateTimeOffset? expected)
        => new(contract, frame, indicator, warm, valid, source, expected,
            source is null ? "Missing projection" : !warm ? "Not warm" : !valid ? "Invalid observation"
            : expected is { } minimum && source < minimum ? "Stale observation" : string.Empty);
}
