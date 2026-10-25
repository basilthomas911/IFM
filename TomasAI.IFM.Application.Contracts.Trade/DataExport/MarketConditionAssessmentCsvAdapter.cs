using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

public sealed class MarketConditionAssessmentCsvAdapter
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public Task ExportAsync(IReadOnlyCollection<MarketConditionAssessmentReferenceRow> rows, string fileName, bool overwrite = true, CancellationToken cancellationToken = default)
        => CsvDataExportWriter.ExportAsync(rows, fileName, overwrite,
            ["Mode", "SchemaVersion", "CaseCode", "CoverageKind", "IsAuthoritative", "TargetHorizon", "Availability", "Condition", "Confidence", "Liquidity", "Stress", "Session", "Events", "Restrictions", "Limitations", "ValidUntilUtc", "Summary"],
            row => [row.Mode,row.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),row.CaseCode,row.CoverageKind,row.IsAuthoritative.ToString(),row.Result.TargetHorizon.ToString(),
                row.Result.Assessment.Availability.ToString(),row.Result.Assessment.ConditionType?.ToString()??"",row.Result.Assessment.AssessmentConfidence?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"",
                row.Result.Assessment.LiquidityCondition.ToString(),row.Result.Assessment.StressState.ToString(),row.Result.Assessment.SessionState.ToString(),row.Result.Assessment.EventRiskState.ToString(),
                string.Join(";",row.Result.Assessment.InheritedRestrictions),string.Join(";",row.Result.Assessment.LimitationReasons),row.Result.Assessment.ValidUntilUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture)??"",row.Result.SummaryText], cancellationToken);
}
