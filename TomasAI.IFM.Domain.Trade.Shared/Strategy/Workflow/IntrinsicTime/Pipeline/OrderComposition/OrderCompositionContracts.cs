using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Framework.Serialization;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

public static class OrderCompositionContracts
{
    public static OrderCompositionResult ReadResult(StrategyStageResultEnvelope envelope)
    {
        var r = envelope.ReadCompositionResult();
        Require(r.SchemaVersion == 1 && r.ResultId != Guid.Empty && r.ResultId == r.InvocationId && r.InputWorkflowRevision > 0
            && r.InputSha256.Length == 64 && !r.Reasons.IsDefaultOrEmpty && r.ProducedAtUtc == r.EvaluatedAtUtc
            && r.CandidateCounts.Generated == r.CandidateCounts.Eligible + r.CandidateCounts.Rejected
            && r.CandidateCounts.Generated is >= 0 and <= 4096, "OC.RESULT.INVALID");
        Require(r.Outcome == CompositionOutcome.Composed ? r.Candidate is not null && r.ValidUntilUtc > r.EvaluatedAtUtc
            : r.Outcome == CompositionOutcome.NoCandidate && r.Candidate is null && r.ValidUntilUtc is null, "OC.RESULT.INVALID");
        if (r.Candidate is { } c)
            Require(c.CandidateId == r.InvocationId && c.UnitQuantity == 1 && c.LiquidityCapacityUnits >= 1
                && c.ApprovalState == "Unapproved" && c.Legs.Length is 1 or 2 or 4 && c.Legs.All(x => x.Ratio == 1)
                && c.CandidateHash == CompositionHash.Candidate(c) && c.ValidUntilUtc == r.ValidUntilUtc
                && c.SnapshotHash == r.DecisionContext.SnapshotHash && c.BindingHash == r.DecisionContext.BindingHash,
                "OC.RESULT.INVALID");
        Require(MessagePackBinarySerializer.MeasureContent(r) <= 524288, "OC.CONTRACT.PAYLOAD_SIZE");
        return r;
    }
    public static bool SameCompletion(OrderCompositionFunctionCompletedEvent a, OrderCompositionFunctionCompletedEvent b) =>
        a.Id == b.Id && a.CommandId == b.CommandId && a.EntityId == b.EntityId && a.WorkflowId == b.WorkflowId
        && a.InputWorkflowRevision == b.InputWorkflowRevision && a.RequestFingerprint == b.RequestFingerprint
        && a.Result.HasSameContent(b.Result);
}
