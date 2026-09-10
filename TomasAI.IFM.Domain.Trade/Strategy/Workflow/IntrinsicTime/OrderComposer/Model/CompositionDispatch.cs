using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Builds the final immutable Function request before the workflow commits its dispatch intent.</summary>
public static class CompositionDispatch
{
    public static ExecuteOrderCompositionPipelineCommand Create(IntrinsicTimeStrategyWorkflowView view,
        CompositionPreparation preparation, DateTime now)
    {
        using var trace = WorkflowTrace.Start("composer.dispatch.create", view);
        var start = view.CompositionDispatch ?? throw new InvalidDataException("Accepted preparation is required.");
        using (WorkflowTrace.Start("composer.dispatch.validate_preparation", view))
        {
            TradeSelectionHandoff.ValidateStart(start, now);
            CompositionPreparationService.Validate(preparation);
        }
        using var bindingTrace = WorkflowTrace.Start("composer.dispatch.resolve_binding", view);
        var selected = TradeSelectionContracts.ReadResult(start.AcceptedSelection!);
        var binding = CompositionBindingResolver.Resolve(selected, start.SelectionBinding!, now);
        bindingTrace?.Stop();
        using var requestTrace = WorkflowTrace.Start("composer.dispatch.build_request", view);
        var p = binding.Rules.VariantRules.Single(x => x.VariantKey == binding.Selected.VariantKey).BaseParameters;
        var id = new OrderCompositionExecutionId(view.EntityId, view.WorkflowId, view.WorkflowRevision);
        var request = new ExecuteOrderCompositionPipelineCommand
        {
            SchemaVersion = 1, CommandId = start.CommandId, Subject = new(ActorType.Function, ExecuteOrderCompositionPipelineCommand.Actor,
                ExecuteOrderCompositionPipelineCommand.Verb, id.Format()), EntityId = id, InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view with { CompositionExecution = null, CompositionDispatch = null }, TriggerEvent = view.TriggerEvent, CorrelationId = view.CorrelationId,
            CausationId = start.CausationId, RequestedAtUtc = now, EvaluatedAtUtc = preparation.Snapshot.EvaluatedAtUtc.UtcDateTime,
            ExpiresAtUtc = new[] { view.ExpiresAtUtc, start.Reservation!.Order.ExpiresAtUtc, binding.ValidUntilUtc,
                preparation.Snapshot.ValidUntilUtc.UtcDateTime, now.AddMilliseconds(p.ExecutionMilliseconds) }.Min(),
            AcceptedSelectionEnvelope = start.AcceptedSelection!, SelectionBinding = start.SelectionBinding!, Reservation = start.Reservation!,
            CompositionBinding = binding, MarketSnapshot = CompositionSnapshotAdapter.From(preparation.Snapshot)
        };
        requestTrace?.Stop();
        using (WorkflowTrace.Start("composer.dispatch.fingerprint", view))
            request = request with { InputSha256 = request.Fingerprint() };
        using (WorkflowTrace.Start("composer.dispatch.validate_request", view))
        {
            var errors = new List<ValidationError>().ValidateCompositionFields(request).ValidateCompositionEvidence(request);
            if (errors.Count != 0) throw new CompositionException("OC.CONTRACT.INVALID");
        }
        return request;
    }
}
