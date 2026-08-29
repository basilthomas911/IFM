using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

public interface IRegimeDiscoveryDecisionReferenceCsvAdapter
{
    Task ExportAsync(IReadOnlyCollection<RegimeDiscoveryDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default);
}

/// <summary>Exports typed Regime Discovery reference rows to deterministic Excel-compatible CSV.</summary>
public sealed class RegimeDiscoveryDecisionReferenceCsvAdapter
    : IRegimeDiscoveryDecisionReferenceCsvAdapter
{
    static readonly string[] Headers =
    [
        "PipelineStage", "GeneratorVersion", "DecisionSchemaVersion", "CoverageKind", "IsAuthoritative",
        "IsCompleteEnumeration", "CaseCode", "Name", "CoverageTags", "TrendDirection", "TrendPhase",
        "TrendStrength", "TrendScore", "TrendConfidence", "TrendTimeFrameAgreement", "VolatilityLevel",
        "VolatilityChange", "TermStructure", "VolatilityScore", "VolatilityConfidence",
        "StructureClassification", "StructureDirection", "Breakout", "StructureScore", "StructureConfidence",
        "DecisionDirection", "DirectionalScore", "RiskAdjustedConviction", "DecisionConfidence",
        "ConfidenceBand", "Quality", "Restrictions", "Reasons"
    ];

    public Task ExportAsync(IReadOnlyCollection<RegimeDiscoveryDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default) =>
        CsvDataExportWriter.ExportAsync(results, fileName, overwrite, Headers, Row, cancellationToken);

    static string[] Row(RegimeDiscoveryDecisionReferenceDto value) =>
    [
        value.PipelineStage,
        CsvDataExportWriter.Value(value.GeneratorVersion),
        CsvDataExportWriter.Value(value.DecisionSchemaVersion),
        value.CoverageKind,
        CsvDataExportWriter.Value(value.IsAuthoritative),
        CsvDataExportWriter.Value(value.IsCompleteEnumeration),
        value.CaseCode,
        value.Name,
        CsvDataExportWriter.Values(value.CoverageTags),
        value.TrendDirection.ToString(),
        value.TrendPhase.ToString(),
        value.TrendStrength.ToString(),
        CsvDataExportWriter.Value(value.TrendScore),
        CsvDataExportWriter.Value(value.TrendConfidence),
        CsvDataExportWriter.Value(value.TrendTimeFrameAgreement),
        value.VolatilityLevel.ToString(),
        value.VolatilityChange.ToString(),
        value.TermStructure.ToString(),
        CsvDataExportWriter.Value(value.VolatilityScore),
        CsvDataExportWriter.Value(value.VolatilityConfidence),
        value.StructureClassification.ToString(),
        value.StructureDirection.ToString(),
        value.Breakout.ToString(),
        CsvDataExportWriter.Value(value.StructureScore),
        CsvDataExportWriter.Value(value.StructureConfidence),
        value.DecisionDirection.ToString(),
        CsvDataExportWriter.Value(value.DirectionalScore),
        CsvDataExportWriter.Value(value.RiskAdjustedConviction),
        CsvDataExportWriter.Value(value.DecisionConfidence),
        value.ConfidenceBand.ToString(),
        value.Quality.ToString(),
        CsvDataExportWriter.Values(value.Restrictions),
        CsvDataExportWriter.Values(value.Reasons)
    ];
}
