using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public sealed class OptionVolatilitySeriesParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.OptionVolatilitySeriesComponent;
    public ParameterComponentSummary Summary => new(
        "option-volatility", "Option Volatility", ComponentCode, "Series",
        [ParameterSchemaRegistry.CurrentOptionVolatilitySeriesSchemaVersion], true);

    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));

    public static OptionVolatilitySeriesParameterSet CreateDefault(Guid setId)
    {
        OptionVolatilityValidation.RequireSetId(setId);
        return new() { ParameterSetId = setId };
    }

    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionVolatilityValidation.Validate<OptionVolatilitySeriesParameterSet>(
            ComponentCode, payloadJson, schemaVersion, ValidateValue);

    static void ValidateValue(OptionVolatilitySeriesParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.Common(value.SchemaVersion, value.ParameterSetId, value.Version, issues);
        OptionVolatilityValidation.Required(value.SeriesId, "SeriesId", issues);
        OptionVolatilityValidation.Required(value.MethodologyVersion, "MethodologyVersion", issues);
        OptionVolatilityValidation.Required(value.UnderlyingRoot, "UnderlyingRoot", issues);
        OptionVolatilityValidation.Required(value.Venue, "Venue", issues);
        OptionVolatilityValidation.Required(value.Currency, "Currency", issues);
        OptionVolatilityValidation.Required(value.MarketDataProvider, "MarketDataProvider", issues);
        OptionVolatilityValidation.Required(value.Dataset, "Dataset", issues);
        OptionVolatilityValidation.Required(value.ExchangeCalendar, "ExchangeCalendar", issues);
        OptionVolatilityValidation.Required(value.TimeZone, "TimeZone", issues);
        OptionVolatilityValidation.RequiredEnum(value.Environment, "Environment", issues);
        OptionVolatilityValidation.RequiredEnum(value.AtmConvention, "AtmConvention", issues);
        OptionVolatilityValidation.RequiredEnum(value.OptionCombination, "OptionCombination", issues);
        OptionVolatilityValidation.RequiredEnum(value.ExpirySelection, "ExpirySelection", issues);
        OptionVolatilityValidation.RequiredEnum(value.Interpolation, "Interpolation", issues);
        OptionVolatilityValidation.RequiredEnum(value.UnderlyingMatch, "UnderlyingMatch", issues);
        OptionVolatilityValidation.RequiredEnum(value.QuoteMark, "QuoteMark", issues);
        OptionVolatilityValidation.RequiredEnum(value.CrossedMarketPolicy, "CrossedMarketPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.LockedMarketPolicy, "LockedMarketPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.RollPolicy, "RollPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.DailySamplingPolicy, "DailySamplingPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.GapPolicy, "GapPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.RankBounds, "RankBounds", issues);
        OptionVolatilityValidation.RequiredEnum(value.PercentileConvention, "PercentileConvention", issues);
        OptionVolatilityValidation.RequiredEnum(value.ImpliedVolatilityUnit, "ImpliedVolatilityUnit", issues);
        OptionVolatilityValidation.RequiredEnum(value.RankAndPercentileUnit, "RankAndPercentileUnit", issues);
        OptionVolatilityValidation.Positive(value.TargetMaturityCalendarDays, "TargetMaturityCalendarDays", issues);
        OptionVolatilityValidation.Positive(value.MinimumDisplayedSize, "MinimumDisplayedSize", issues);
        OptionVolatilityValidation.Positive(value.MaximumQuoteAgeSeconds, "MaximumQuoteAgeSeconds", issues);
        OptionVolatilityValidation.Positive(value.MaximumIvAgeSeconds, "MaximumIvAgeSeconds", issues);
        OptionVolatilityValidation.Positive(value.MaximumQuoteSkewMilliseconds, "MaximumQuoteSkewMilliseconds", issues);
        OptionVolatilityValidation.Positive(value.IntradayCoalescingMinutes, "IntradayCoalescingMinutes", issues);
        OptionVolatilityValidation.InRange(value.MaximumIntradayCheckpoints, 1, 84, "MaximumIntradayCheckpoints", issues);
        OptionVolatilityValidation.Positive(value.HistoricalLookbackSessions, "HistoricalLookbackSessions", issues);
        OptionVolatilityValidation.InRange(value.MinimumValidObservations, 1, value.HistoricalLookbackSessions,
            "MinimumValidObservations", issues);
        if (value.MinimumCoverage is <= 0 or > 1)
            OptionVolatilityValidation.Add(issues, "MinimumCoverage", "Minimum coverage must be greater than zero and no greater than one.");
        OptionVolatilityValidation.UniqueRequired(value.EligibleProductFamilies, "EligibleProductFamilies", issues);
        if (!string.IsNullOrWhiteSpace(value.TimeZone))
        {
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZone); }
            catch (TimeZoneNotFoundException) { OptionVolatilityValidation.Add(issues, "TimeZone", "Time zone is not recognized."); }
            catch (InvalidTimeZoneException) { OptionVolatilityValidation.Add(issues, "TimeZone", "Time zone is invalid."); }
        }

        if (value.Pricers is not { Length: > 0 })
            OptionVolatilityValidation.Add(issues, "Pricers", "At least one exercise-style pricer selection is required.");
        else
        {
            if (value.Pricers.Any(selection => selection is null))
                OptionVolatilityValidation.Add(issues, "Pricers", "Pricer selections cannot be null.");
            var selections = value.Pricers.Where(selection => selection is not null).ToArray();
            if (selections.Select(selection => selection.ExerciseStyle).Distinct().Count() != selections.Length)
                OptionVolatilityValidation.Add(issues, "Pricers", "Exercise styles must be unique.");
            foreach (var selection in selections)
            {
                if (selection.ExerciseStyle == OptionVolatilityExerciseStyle.Unknown)
                    OptionVolatilityValidation.Add(issues, "Pricers/ExerciseStyle", "An exercise style is required.");
                OptionVolatilityValidation.UniqueRequired(selection.PricerVersions, "Pricers/PricerVersions", issues);
            }
            foreach (var style in new[] { OptionVolatilityExerciseStyle.European, OptionVolatilityExerciseStyle.American })
                if (selections.All(selection => selection.ExerciseStyle != style))
                    OptionVolatilityValidation.Add(issues, "Pricers", $"A {style} pricer selection is required.");
        }
    }
}

public sealed class OptionVolatilityConsumerRulesParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent;
    public ParameterComponentSummary Summary => new(
        "option-volatility", "Option Volatility", ComponentCode, "Consumer Rules",
        [ParameterSchemaRegistry.CurrentOptionVolatilityConsumerRulesSchemaVersion], true);

    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));

    public static OptionVolatilityConsumerRulesParameterSet CreateDefault(Guid setId)
    {
        OptionVolatilityValidation.RequireSetId(setId);
        return new() { ParameterSetId = setId };
    }

    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionVolatilityValidation.Validate<OptionVolatilityConsumerRulesParameterSet>(
            ComponentCode, payloadJson, schemaVersion, ValidateValue);

    static void ValidateValue(OptionVolatilityConsumerRulesParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.Common(value.SchemaVersion, value.ParameterSetId, value.Version, issues);
        OptionVolatilityValidation.Required(value.SeriesId, "SeriesId", issues);
        OptionVolatilityValidation.Required(value.MethodologyVersion, "MethodologyVersion", issues);
        OptionVolatilityValidation.Required(value.MetricPolicyVersion, "MetricPolicyVersion", issues);
        OptionVolatilityValidation.RequiredEnum(value.OpenMarketEvidence, "OpenMarketEvidence", issues);
        OptionVolatilityValidation.RequiredEnum(value.ClosedMarketEvidence, "ClosedMarketEvidence", issues);
        OptionVolatilityValidation.RequiredEnum(value.UnavailablePolicy, "UnavailablePolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.IronCondor, "IronCondor", issues);
        OptionVolatilityValidation.RequiredEnum(value.VerticalSpread, "VerticalSpread", issues);
        OptionVolatilityValidation.RequiredEnum(value.FuturesOutright, "FuturesOutright", issues);
        OptionVolatilityValidation.RequiredEnum(value.HeldOptions, "HeldOptions", issues);
        OptionVolatilityValidation.RequiredEnum(value.ProtectiveClose, "ProtectiveClose", issues);
        OptionVolatilityValidation.RequiredEnum(value.ProtectiveCancel, "ProtectiveCancel", issues);
        OptionVolatilityValidation.Positive(value.LiveMaximumAgeMinutes, "LiveMaximumAgeMinutes", issues);
        OptionVolatilityValidation.InRange(value.MaximumSessionLag, 0, 30, "MaximumSessionLag", issues);
        if (!value.ExactDecisionSnapshotRequired)
            OptionVolatilityValidation.Add(issues, "ExactDecisionSnapshotRequired", "Consumer decisions must retain the exact accepted snapshot.");
        if (value.AllowDownstreamSilentReplacement)
            OptionVolatilityValidation.Add(issues, "AllowDownstreamSilentReplacement", "Downstream consumers cannot silently replace accepted evidence.");
        if (value.NumericalRules is null)
        {
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Numerical rules are required, even when every value is null.");
            return;
        }
        var rules = value.NumericalRules;
        OptionVolatilityValidation.OptionalRange(rules.MinimumIvRank, 0, 100, "NumericalRules/MinimumIvRank", issues);
        OptionVolatilityValidation.OptionalRange(rules.MaximumIvRank, 0, 100, "NumericalRules/MaximumIvRank", issues);
        OptionVolatilityValidation.OptionalRange(rules.MinimumIvPercentile, 0, 100, "NumericalRules/MinimumIvPercentile", issues);
        OptionVolatilityValidation.OptionalRange(rules.MaximumIvPercentile, 0, 100, "NumericalRules/MaximumIvPercentile", issues);
        OptionVolatilityValidation.OptionalRange(rules.DeltaTargetAdjustment, -1, 1, "NumericalRules/DeltaTargetAdjustment", issues);
        OptionVolatilityValidation.OptionalMinimum(rules.SpreadWidthAdjustment, 0, "NumericalRules/SpreadWidthAdjustment", issues);
        OptionVolatilityValidation.OptionalMinimum(rules.RiskLimitAdjustment, 0, "NumericalRules/RiskLimitAdjustment", issues);
        if (rules.MinimumIvRank > rules.MaximumIvRank)
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Minimum IV Rank cannot exceed maximum IV Rank.");
        if (rules.MinimumIvPercentile > rules.MaximumIvPercentile)
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Minimum IV Percentile cannot exceed maximum IV Percentile.");
    }
}

public sealed class OptionVolatilityRetentionParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.OptionVolatilityRetentionComponent;
    public ParameterComponentSummary Summary => new(
        "option-volatility", "Option Volatility", ComponentCode, "Retention",
        [ParameterSchemaRegistry.CurrentOptionVolatilityRetentionSchemaVersion], true);

    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));

    public static OptionVolatilityRetentionParameterSet CreateDefault(Guid setId)
    {
        OptionVolatilityValidation.RequireSetId(setId);
        return new() { ParameterSetId = setId };
    }

    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionVolatilityValidation.Validate<OptionVolatilityRetentionParameterSet>(
            ComponentCode, payloadJson, schemaVersion, ValidateValue);

    static void ValidateValue(OptionVolatilityRetentionParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.Common(value.SchemaVersion, value.ParameterSetId, value.Version, issues);
        OptionVolatilityValidation.Positive(value.SourceObservationYears, "SourceObservationYears", issues);
        OptionVolatilityValidation.Positive(value.FinalizedDailyMetricYears, "FinalizedDailyMetricYears", issues);
        OptionVolatilityValidation.Positive(value.IntradayCheckpointDays, "IntradayCheckpointDays", issues);
        OptionVolatilityValidation.Positive(value.DecisionSnapshotYearsAfterCompletion, "DecisionSnapshotYearsAfterCompletion", issues);
        OptionVolatilityValidation.Positive(value.BackfillStagingDays, "BackfillStagingDays", issues);
        OptionVolatilityValidation.Positive(value.FailedPublicationDays, "FailedPublicationDays", issues);
        OptionVolatilityValidation.InRange(value.MaximumPageSize, 1, 256, "MaximumPageSize", issues);
        OptionVolatilityValidation.RequiredEnum(value.ReferencedEvidencePolicy, "ReferencedEvidencePolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.LatestPointerLifetime, "LatestPointerLifetime", issues);
        OptionVolatilityValidation.RequiredEnum(value.DefinitionLifetime, "DefinitionLifetime", issues);
        OptionVolatilityValidation.RequiredEnum(value.PublishedParameterLifetime, "PublishedParameterLifetime", issues);
        OptionVolatilityValidation.RequiredEnum(value.CorrectionPolicy, "CorrectionPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.PartitionBucket, "PartitionBucket", issues);
        OptionVolatilityValidation.RequiredEnum(value.TradeIvBackfill, "TradeIvBackfill", issues);
        OptionVolatilityValidation.UniqueDefined(value.HistoricalSources, "HistoricalSources", issues);
        OptionVolatilityValidation.UniqueDefined(value.IsolatedEnvironments, "IsolatedEnvironments", issues);
        var requiredSources = new[]
        {
            HistoricalVolatilitySource.DatabentoQuoteHistory,
            HistoricalVolatilitySource.IfmReferenceMetadata
        };
        if (value.HistoricalSources is null || requiredSources.Any(source => !value.HistoricalSources.Contains(source)))
            OptionVolatilityValidation.Add(issues, "HistoricalSources", "Databento quote history and IFM reference metadata are required.");
        var requiredEnvironments = new[]
        {
            OptionVolatilityEnvironment.Production,
            OptionVolatilityEnvironment.Paper,
            OptionVolatilityEnvironment.Simulation
        };
        if (value.IsolatedEnvironments is null || requiredEnvironments.Any(environment => !value.IsolatedEnvironments.Contains(environment)))
            OptionVolatilityValidation.Add(issues, "IsolatedEnvironments", "Production, Paper, and Simulation must be isolated.");
        if (value.AllowUnqualifiedBackfillPublication)
            OptionVolatilityValidation.Add(issues, "AllowUnqualifiedBackfillPublication", "Unqualified backfill cannot be published.");
    }
}

static class OptionVolatilityValidation
{
    internal static void RequireSetId(Guid setId)
    {
        if (setId == Guid.Empty) throw new ArgumentException("Set identity is required.", nameof(setId));
    }

    internal static ParameterValidationIssue[] Validate<T>(
        string componentCode, string payloadJson, int schemaVersion, Action<T, List<ParameterValidationIssue>> validate)
    {
        try
        {
            var canonical = ParameterCanonicalPayloadModel.Canonicalize(payloadJson);
            var structural = ParameterSchemaRegistry.Default.ValidateStructure(componentCode, schemaVersion, canonical);
            if (structural.Length != 0) return structural;
            var value = JsonSerializer.Deserialize<T>(canonical);
            if (value is null) return [new("PARAM.OBJECT_REQUIRED", "Payload", "A parameter object is required.")];
            var issues = new List<ParameterValidationIssue>();
            validate(value, issues);
            if (typeof(T).GetProperty(nameof(OptionVolatilitySeriesParameterSet.SchemaVersion))?.GetValue(value) is int declared
                && declared != schemaVersion)
                issues.Add(new("PARAM.SCHEMA_MISMATCH", "SchemaVersion", "Payload schema differs from selected schema."));
            return issues.ToArray();
        }
        catch (Exception error) when (error is ArgumentException or JsonException)
        {
            return [new("PARAM.STRUCTURE_INVALID", "Payload", error.Message)];
        }
    }

    internal static void Common(int schemaVersion, Guid setId, int version, List<ParameterValidationIssue> issues)
    {
        if (schemaVersion != 1) Add(issues, "SchemaVersion", "Only schema version 1 is supported.", "PARAM.SCHEMA_MISMATCH");
        if (setId == Guid.Empty) Add(issues, "ParameterSetId", "Parameter-set identity is required.", "PARAM.IDENTITY_INVALID");
        if (version <= 0) Add(issues, "Version", "Parameter-set version must be positive.", "PARAM.VERSION_INVALID");
    }

    internal static void Required(string? value, string path, List<ParameterValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(issues, path, "A non-empty value is required.");
    }

    internal static void RequiredEnum<T>(T value, string path, List<ParameterValidationIssue> issues) where T : struct, Enum
    {
        if (Convert.ToInt32(value) == 0) Add(issues, path, "A defined non-unknown value is required.");
    }

    internal static void Positive(int value, string path, List<ParameterValidationIssue> issues) =>
        InRange(value, 1, int.MaxValue, path, issues);

    internal static void InRange(int value, int minimum, int maximum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum || value > maximum) Add(issues, path, $"Value must be between {minimum} and {maximum}.");
    }

    internal static void OptionalRange(decimal? value, decimal minimum, decimal maximum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum || value > maximum) Add(issues, path, $"Value must be between {minimum} and {maximum}.");
    }

    internal static void OptionalMinimum(decimal? value, decimal minimum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum) Add(issues, path, $"Value must be at least {minimum}.");
    }

    internal static void UniqueRequired(string[]? values, string path, List<ParameterValidationIssue> issues)
    {
        if (values is not { Length: > 0 } || values.Any(string.IsNullOrWhiteSpace))
            Add(issues, path, "At least one non-empty value is required.");
        else if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            Add(issues, path, "Values must be unique.");
    }

    internal static void UniqueDefined<T>(T[]? values, string path, List<ParameterValidationIssue> issues) where T : struct, Enum
    {
        if (values is not { Length: > 0 } || values.Any(value => Convert.ToInt32(value) == 0))
            Add(issues, path, "At least one defined value is required.");
        else if (values.Distinct().Count() != values.Length)
            Add(issues, path, "Values must be unique.");
    }

    internal static void Add(List<ParameterValidationIssue> issues, string path, string message, string code = "PARAM.VALUE_INVALID") =>
        issues.Add(new(code, path, message));
}
