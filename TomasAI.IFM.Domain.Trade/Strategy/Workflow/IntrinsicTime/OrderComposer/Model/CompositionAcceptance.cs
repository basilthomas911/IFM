using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Recomputes economic content from the accepted request before any durable risk continuation.</summary>
public static class CompositionAcceptance
{
    public static OrderCompositionResult Validate(ExecuteOrderCompositionPipelineCommand accepted,
        CompleteOrderCompositionCommand command, IOrderComposer model, DateTime now, CancellationToken token = default)
    {
        Require(accepted.InputSha256 == accepted.Fingerprint() && command.WorkflowId == accepted.WorkflowId
            && command.EntityId == accepted.WorkflowEntityId && command.InputWorkflowRevision == accepted.InputWorkflowRevision
            && command.SourceEventId == accepted.CommandId && now < accepted.ExpiresAtUtc, "OC.RESULT.INVALID");
        var result = OrderCompositionContracts.ReadResult(command.Result);
        Require(result.InputSha256 == accepted.InputSha256 && result.InvocationId == accepted.CommandId, "OC.RESULT.INVALID");
        var recomputed = model.Calculate(accepted, token);
        Require(CompositionHash.Compute(recomputed) == CompositionHash.Compute(result), "OC.RESULT.INVALID");
        if (result.Candidate is { } candidate)
        {
            Require(now < candidate.ValidUntilUtc, "OC.TIME.EXPIRED");
            var selected = command.SelectedContracts;
            Require(selected is not null && selected.PricingPlanId == accepted.MarketSnapshot.ScopeId
                && selected.ContractIds.SequenceEqual(candidate.Legs.Select(x => x.InstrumentId).Order(StringComparer.Ordinal)), "OC.RESULT.INVALID");
        }
        else Require(command.SelectedContracts is null, "OC.RESULT.INVALID");
        return result;
    }
}
