using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

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
