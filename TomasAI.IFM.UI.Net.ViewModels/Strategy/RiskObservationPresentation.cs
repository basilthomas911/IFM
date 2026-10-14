using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
namespace TomasAI.IFM.UI.Net.ViewModels.Strategy;
/// <summary>Shared read-only Risk decision text used by both Windows clients.</summary>
public static class RiskObservationPresentation
{
    /// <summary>Renders current authorization separately from the frozen historical calculation.</summary>
    /// <param name="observation">The exact invocation and freshly checked authority.</param>
    public static string Render(RiskObservation observation)
    {
        var view = observation.Snapshot.State; var result = observation.Calculation; var b = new StringBuilder();
        b.AppendLine($"CURRENT AUTHORITY: {observation.CurrentAuthority} (checked {observation.CheckedAtUtc:O})");
        b.AppendLine($"Fund outcome: {observation.FundSynchronization}; projection behind: {observation.ProjectionBehind}");
        b.AppendLine($"Workflow: {view.WorkflowId}; revision: {view.WorkflowRevision}; invocation: {view.RiskExecution?.CommandId}");
        b.AppendLine($"Workflow state: {view.Status}; financial phase: {view.FinancialHandoff?.Phase}; stop reason: {view.StopReasonCode}");
        if (view.RiskExecution is { } input)
        {
            b.AppendLine($"Composer result: {input.CompositionResult.ResultId}; policy: {input.PolicyId} version {input.PolicyVersion}");
            b.AppendLine($"Evaluated: {input.EvaluatedAtUtc:O}; original expiry: {input.ExpiresAtUtc:O}; funding environment: {input.SizingAuthority.Environment}");
        }
        if (result?.Requirements is { } requirement) b.AppendLine($"Settlement cash {requirement.SettlementCash}; margin funding {requirement.MarginFunding}; fees {requirement.FeeReserve}; variation reserve {requirement.VariationReserve}");
        b.AppendLine($"Accepted by workflow: {observation.CalculationAccepted}");
        b.AppendLine($"Historical calculation: {result?.Outcome.ToString() ?? "Not completed"}; units: {result?.StrategyUnits}");
        if (result is not null) { b.AppendLine($"Result: {result.ResultId}; hash: {RiskContracts.Hash(result)}"); b.AppendLine(string.Join(Environment.NewLine, result.Reasons)); }
        if (view.RiskExplanation is { } explanation)
        {
            b.AppendLine($"Explanation schema {explanation.SchemaVersion}; hash {explanation.ContentHash}");
            b.AppendLine($"Available cash {explanation.AvailableCash}; effective loss budget {explanation.EffectiveLossBudget}; market multiplier {explanation.MarketMultiplier}");
            foreach (var condition in explanation.Conditions) b.AppendLine(condition);
            foreach (var q in explanation.Quantities)
            {
                b.AppendLine($"{q.Units} units require {q.Cash} cash; available at evaluation: {explanation.AvailableCash}; fits={q.CashFits}. Loss charge {q.Loss}; budget {explanation.EffectiveLossBudget}; fits={q.LossFits}.");
                foreach (var limit in q.Limits) { var bound = explanation.Limits[limit.LimitIndex]; b.AppendLine($"  {bound}: proposed={limit.Proposed}; existing={limit.Existing}; fits={limit.Fits}"); }
            }
        }
        else b.AppendLine("Detailed explanation unavailable for this historical snapshot.");
        b.AppendLine($"Resize evidence: {view.RiskResize}");
        b.AppendLine("Open Evidence for frozen inputs, source versions, hashes, receipts and Fund state.");
        return b.ToString();
    }
}
