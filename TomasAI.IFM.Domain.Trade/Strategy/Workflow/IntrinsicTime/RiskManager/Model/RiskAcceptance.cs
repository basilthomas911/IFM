using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Verifies the exact persisted invocation and independently recalculates a proposed terminal result.</summary>
public static class RiskAcceptance
{
    public static RiskAssessmentResult Validate(ExecuteRiskManagementPipelineCommand accepted,
        CompleteRiskManagementCommand command, IRiskEvaluator model, DateTime now, CancellationToken token = default)
    {
        RiskUnitModel.Require(now.Kind == DateTimeKind.Utc && now < accepted.ExpiresAtUtc, "RM.TIME.EXPIRED");
        RiskUnitModel.Require(new List<ValidationError>().ValidateRiskFields(accepted).Count == 0
            && command.WorkflowId == accepted.WorkflowId && command.EntityId == accepted.WorkflowEntityId
            && command.InputWorkflowRevision == accepted.InputWorkflowRevision && command.SourceEventId == accepted.CommandId
            && command.CorrelationId == accepted.CorrelationId && command.CausationId == accepted.CommandId,
            "RM.RESULT.INVALID");
        var result = command.Result.ReadRiskResult();
        RiskContracts.ValidateResult(result);
        RiskUnitModel.Require(result.InputHash == accepted.InputSha256 && result.InvocationId == accepted.CommandId
            && command.CompletedAtUtc == result.ProducedAtUtc, "RM.RESULT.INVALID");
        var recomputed = model.Calculate(accepted, token);
        RiskUnitModel.Require(RiskContracts.Hash(recomputed) == RiskContracts.Hash(result), "RM.RESULT.INVALID");
        return result;
    }
}
