using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

public interface IMarketConditionDecisionReferenceCsvAdapter
{
    Task ExportAsync(IReadOnlyCollection<MarketConditionDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default);
}

/// <summary>Exports typed Market Condition reference rows to deterministic Excel-compatible CSV.</summary>
public sealed class MarketConditionDecisionReferenceCsvAdapter
    : IMarketConditionDecisionReferenceCsvAdapter
{
    static readonly string[] Headers =
    [
        "PipelineStage", "GeneratorVersion", "DecisionSchemaVersion", "CoverageKind", "IsAuthoritative",
        "IsCompleteEnumeration", "CaseCode", "Name", "CoverageTags", "TargetHorizon", "RegimeDirection",
        "TrendPhase", "VolatilityLevel", "VolatilityChange", "TermStructure", "StructureClassification",
        "Breakout", "TriggerConflict", "OptionQualityBlocked", "RegimeNoNewTrade", "Tradeability",
        "ConditionType", "Direction", "Phase", "Strength", "Confidence", "VolatilityBehavior",
        "LiquidityQuality", "DataQuality", "UpstreamAlignment", "PrimaryReasonCode", "Reasons",
        "BlockingReasons", "EvidenceFeatures", "HintTradeType", "HintTimeFrame", "HintSuitability",
        "HintConfidence", "HintReasonCode", "HintIsAdvisory"
    ];

    public Task ExportAsync(IReadOnlyCollection<MarketConditionDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default) =>
        CsvDataExportWriter.ExportAsync(results, fileName, overwrite, Headers, Row, cancellationToken);

    static string[] Row(MarketConditionDecisionReferenceDto value) =>
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
        value.TargetHorizon.ToString(),
        value.RegimeDirection.ToString(),
        value.TrendPhase.ToString(),
        value.VolatilityLevel.ToString(),
        value.VolatilityChange.ToString(),
        value.TermStructure.ToString(),
        value.StructureClassification.ToString(),
        value.Breakout.ToString(),
        CsvDataExportWriter.Value(value.TriggerConflict),
        CsvDataExportWriter.Value(value.OptionQualityBlocked),
        CsvDataExportWriter.Value(value.RegimeNoNewTrade),
        value.Tradeability.ToString(),
        value.ConditionType.ToString(),
        value.Direction.ToString(),
        value.Phase.ToString(),
        CsvDataExportWriter.Value(value.Strength),
        CsvDataExportWriter.Value(value.Confidence),
        value.VolatilityBehavior.ToString(),
        value.LiquidityQuality.ToString(),
        value.DataQuality.ToString(),
        value.UpstreamAlignment.ToString(),
        value.PrimaryReasonCode,
        CsvDataExportWriter.Values(value.Reasons),
        CsvDataExportWriter.Values(value.BlockingReasons),
        CsvDataExportWriter.Values(value.EvidenceFeatures),
        value.HintTradeType.ToString(),
        value.HintTimeFrame.ToString(),
        value.HintSuitability.ToString(),
        CsvDataExportWriter.Value(value.HintConfidence),
        value.HintReasonCode,
        CsvDataExportWriter.Value(value.HintIsAdvisory)
    ];
}
