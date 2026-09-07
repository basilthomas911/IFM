using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record AssessmentSourceBinding(
    [property: Key(0)] string SourceId,
    [property: Key(1)] bool Required,
    [property: Key(2)] int MaximumAgeSeconds);

[MessagePackObject]
public sealed record MarketConditionAssessmentHorizonProfile
{
    [Key(0)] public TimeFrameType Horizon { get; init; }
    [Key(1)] public Guid RegimeProfileId { get; init; }
    [Key(2)] public int RegimeProfileVersion { get; init; }
    [Key(3)] public int RegimeMaximumAgeSeconds { get; init; } = 120;
    [Key(4)] public int ResultLifetimeSeconds { get; init; } = 30;
    [Key(5)] public string SummaryTemplateVersion { get; init; } = "mc-assessment-v1";
}

/// <summary>Market-only configuration. Routing and fund mandate fields are deliberately absent.</summary>
[MessagePackObject]
public sealed record MarketConditionAssessmentParameterSet
{
    AssessmentSourceBinding[] _sources =
    [new("ReferenceQuote", true, 2), new("FeedHealth", true, 15), new("SessionCalendar", true, 60),
     new("EventRiskCalendar", true, 900), new("LastTrade", false, 5), new("NormalizedMovement", false, 15),
     new("VolatilityChange", false, 15)];
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ParameterSetId { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public string MarketProfileId { get; init; } = string.Empty;
    [Key(4)] public string InstrumentRoot { get; init; } = "ES";
    [Key(5)] public TimeFrameType TargetHorizon { get; init; }
    [Key(6)] public string ReferencePolicy { get; init; } = "OnTheRun";
    [Key(7)] public string CalendarBinding { get; init; } = "CME";
    [Key(8)] public MarketConditionAssessmentHorizonProfile HorizonProfile { get; init; } = new();
    [Key(9)] public AssessmentSourceBinding[] Sources { get => [.. _sources.OrderBy(x => x.SourceId, StringComparer.Ordinal)]; init => _sources = value is null ? [] : [.. value.OrderBy(x => x.SourceId, StringComparer.Ordinal)]; }
    [Key(10)] public int FutureClockSkewSeconds { get; init; } = 2;
    [Key(11)] public int SnapshotCaptureAttempts { get; init; } = 3;
    [Key(12)] public int MaximumExecutionMilliseconds { get; init; } = 5000;
    [Key(13)] public int TriggerMaximumAgeSeconds { get; init; } = 30;
    [Key(14)] public decimal TickSize { get; init; } = 0.25m;
    [Key(15)] public decimal HealthySpreadTicks { get; init; } = 1;
    [Key(16)] public decimal DegradedSpreadTicks { get; init; } = 2;
    [Key(17)] public decimal HealthyBestSize { get; init; } = 10;
    [Key(18)] public decimal DegradedBestSize { get; init; } = 5;
    [Key(19)] public decimal MovementStressThreshold { get; init; } = 1.50m;
    [Key(20)] public decimal VolatilityChangeStressThreshold { get; init; } = 0.15m;
    [Key(21)] public int CalendarDownloadMaximumAgeSeconds { get; init; } = 86400;
    [Key(22)] public int HighImpactBeforeMinutes { get; init; } = 15;
    [Key(23)] public int HighImpactAfterMinutes { get; init; } = 10;
    [Key(24)] public int RateDecisionBeforeMinutes { get; init; } = 30;
    [Key(25)] public int RateDecisionAfterMinutes { get; init; } = 20;
    // This revision supports one explicit economic-calendar authority. Changing it requires a new profile/version.
    [Key(26)] public string EconomicCalendarDataset { get; init; } = "EconomicCalendar";
    [Key(27)] public string EconomicCalendarProvider { get; init; } = "FMP";
    [Key(28)] public string EconomicCalendarScopes { get; init; } = "ALL,US";
    [Key(29)] public string CalendarCoveragePolicy { get; init; } = "FMP.CalendarCoverage.v1";

    public void Validate()
    {
        var supported = new[] { "ReferenceQuote", "FeedHealth", "SessionCalendar", "EventRiskCalendar", "LastTrade", "NormalizedMovement", "VolatilityChange" };
        if (SchemaVersion != 1 || ParameterSetId == Guid.Empty || Version <= 0 ||
            string.IsNullOrWhiteSpace(MarketProfileId) || MarketProfileId.Length > 128 || InstrumentRoot != "ES" ||
            !IsHorizon(TargetHorizon) || ReferencePolicy != "OnTheRun" || CalendarBinding != "CME" ||
            EconomicCalendarDataset != "EconomicCalendar" || EconomicCalendarProvider != "FMP" ||
            EconomicCalendarScopes != "ALL,US" || CalendarCoveragePolicy != "FMP.CalendarCoverage.v1" ||
            HorizonProfile is null || HorizonProfile.Horizon != TargetHorizon || HorizonProfile.RegimeProfileId == Guid.Empty ||
            HorizonProfile.RegimeProfileVersion <= 0 || HorizonProfile.RegimeMaximumAgeSeconds <= 0 ||
            HorizonProfile.ResultLifetimeSeconds <= 0 || HorizonProfile.SummaryTemplateVersion != "mc-assessment-v1" ||
            Sources.Length != supported.Length || Sources.Any(x => x is null || !supported.Contains(x.SourceId) || x.MaximumAgeSeconds <= 0) ||
            Sources.Select(x => x.SourceId).Distinct(StringComparer.Ordinal).Count() != supported.Length ||
            Sources.Any(x => x.Required != (x.SourceId is "ReferenceQuote" or "FeedHealth" or "SessionCalendar" or "EventRiskCalendar")) ||
            FutureClockSkewSeconds is < 0 or > 60 || SnapshotCaptureAttempts is < 1 or > 10 ||
            MaximumExecutionMilliseconds is < 1 or > 60000 || TriggerMaximumAgeSeconds <= 0 || TickSize <= 0 ||
            HealthySpreadTicks < 0 || DegradedSpreadTicks < HealthySpreadTicks || DegradedBestSize < 0 || HealthyBestSize < DegradedBestSize ||
            MovementStressThreshold <= 0 || VolatilityChangeStressThreshold <= 0 || CalendarDownloadMaximumAgeSeconds is < 1 or > 86400 ||
            new[] { HighImpactBeforeMinutes, HighImpactAfterMinutes, RateDecisionBeforeMinutes, RateDecisionAfterMinutes }.Any(x => x is < 0 or > 1440))
            throw new ArgumentException("Invalid market assessment profile or unsupported source binding.");
    }

    public static bool IsHorizon(TimeFrameType value) => value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;
    public static MarketConditionAssessmentParameterSet CreateDefault(string marketProfileId, TimeFrameType horizon,
        Guid parameterId, Guid regimeProfileId, int regimeVersion) => new()
    {
        ParameterSetId = parameterId, Version = 1, MarketProfileId = marketProfileId, TargetHorizon = horizon,
        HorizonProfile = new() { Horizon = horizon, RegimeProfileId = regimeProfileId, RegimeProfileVersion = regimeVersion,
            ResultLifetimeSeconds = horizon switch { TimeFrameType.Daily => 30, TimeFrameType.Weekly => 60, TimeFrameType.Monthly => 90, _ => throw new ArgumentException("Unsupported horizon.") } }
    };
}

public static class MarketConditionAssessmentHash
{
    static readonly JsonSerializerOptions Options = CreateOptions();
    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CanonicalDecimalConverter());
        return options;
    }
    sealed class CanonicalDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer,decimal value,JsonSerializerOptions options)
            =>writer.WriteRawValue(value.ToString("G29",System.Globalization.CultureInfo.InvariantCulture));
    }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value,Options);
    public static string Compute<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));
    public static string Parameters(MarketConditionAssessmentParameterSet p)
    {
        p.Validate();
        return Compute(p with { Sources = p.Sources });
    }
}

[MessagePackObject]
public sealed record MarketConditionAssessmentBinding
{
    [Key(0)] public short ModeVersion { get; init; } = 1;
    [Key(1)] public MarketConditionAssessmentParameterSet Parameters { get; init; } = new();
    [Key(2)] public string PayloadSha256 { get; init; } = string.Empty;
    public void Validate()
    {
        if (ModeVersion != 1 || PayloadSha256 != MarketConditionAssessmentHash.Parameters(Parameters))
            throw new ArgumentException("Invalid frozen assessment mode or parameter hash.");
    }
}
