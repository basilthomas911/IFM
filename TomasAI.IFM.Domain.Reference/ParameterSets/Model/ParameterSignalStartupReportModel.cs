using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public static class ParameterSignalStartupReportModel
{
    public static void Validate(ParameterStartupRun run, ParameterSignalStartupReport report)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (report is null || report.RunId != run.RunId || !string.Equals(report.PlanFingerprint,run.Plan.Fingerprint,StringComparison.Ordinal) || report.ValueDate == default ||
         report.RecordedAtUtc.Kind != DateTimeKind.Utc || report.ContractId is null || report.ContractId.Length > 128 || report.Outcomes is null ||
         report.Outcomes.Length != run.Plan.Steps.Length || report.Outcomes.Select(x => x.Key).Distinct().Count() != report.Outcomes.Length)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.STARTUP_REPORT_INVALID");
#pragma warning restore MA0015
        foreach (var outcome in report.Outcomes)
        {
            var step = run.Plan.Steps.SingleOrDefault(x => x.Key == outcome.Key);
            if (step is null || !Enum.IsDefined(outcome.Status) || outcome.Detail is null || outcome.Detail.Length > 2000 ||
             (step.Prepare && outcome.Status == ParameterSignalPreparationStatus.NotRequested) ||
             (!step.Prepare && outcome.Status != ParameterSignalPreparationStatus.NotRequested))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.STARTUP_REPORT_INVALID");
#pragma warning restore MA0015
        }
    }
}
