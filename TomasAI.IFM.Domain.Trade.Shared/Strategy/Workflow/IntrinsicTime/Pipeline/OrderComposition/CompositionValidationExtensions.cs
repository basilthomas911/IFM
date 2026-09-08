using TomasAI.IFM.Shared.EventSourcing;
using FluentValidation;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

/// <summary>Ordered list validation for Function ingress. No remote reads, calculations or side effects.</summary>
public static class CompositionValidationExtensions
{
    public static List<ValidationError> ValidateCompositionFields(this List<ValidationError> errors, ExecuteOrderCompositionPipelineCommand c)
    {
        errors.AddRange(new FieldRules().Execute(c));
        return errors;
    }
    public static List<ValidationError> ValidateCompositionEvidence(this List<ValidationError> errors, ExecuteOrderCompositionPipelineCommand c)
    {
        if (errors.Count != 0) return errors;
        void Check(bool pass, string code) { if (!pass) errors.Add(new(code)); }
        var view = c.WorkflowView;
        var selected = c.AcceptedSelectionEnvelope.SelectionResult;
        Check(c.AcceptedSelectionEnvelope.HasValidPayloadSha256() && selected is { Outcome: TradeSelection.SelectionOutcome.Selected, SelectedCandidate: not null }, "OC.CONTRACT.UPSTREAM_INVALID");
        Check(c.EntityId.InputWorkflowRevision == c.InputWorkflowRevision && c.WorkflowId == view.WorkflowId && c.WorkflowEntityId == view.EntityId
            && view.Status == WorkflowStrategyMachineStatus.Started && c.InputWorkflowRevision == view.WorkflowRevision && view.CurrentStage == StrategyWorkflowStage.OrderComposition
            && c.TriggerEvent.Id == view.TriggerEventId && c.TriggerEvent.Id == view.TriggerEvent.Id
            && c.CorrelationId == view.CorrelationId && c.CompositionBinding.Rules.SupportedHorizon.ToString() == c.TriggerEvent.EntityId.TimePeriod.ToString(), "OC.CONTRACT.IDENTITY");
        Check(c.Subject == new ActorSubject(ActorType.Function, ExecuteOrderCompositionPipelineCommand.Actor, ExecuteOrderCompositionPipelineCommand.Verb, c.EntityId.Format())
            && c.RouteTo == BoundedContextName.OrderCompositionPipelineBoundedContext, "OC.CONTRACT.IDENTITY");
        Check(c.SelectionBinding.PayloadSha256 == TradeSelection.TradeSelectionContracts.BindingHash(c.SelectionBinding)
            && c.CompositionBinding.BindingSha256 == CompositionHash.Binding(c.CompositionBinding), "OC.CONTRACT.HASH");
        Check(c.MarketSnapshot.Digest == CompositionSemanticHash.Compute(c.MarketSnapshot with { Digest = "" })
            && c.MarketSnapshot.EvaluatedAtUtc.UtcDateTime == c.EvaluatedAtUtc, "OC.CONTRACT.HASH");
        if (selected is not null)
        {
            Check(selected.WorkflowId == c.WorkflowId && selected.EntityId == c.WorkflowEntityId
                && selected.DecisionHorizon == c.CompositionBinding.Rules.SupportedHorizon
                && selected.DecisionContext.SelectionBinding.PayloadSha256 == c.SelectionBinding.PayloadSha256
                && CompositionHash.Compute(selected.SelectedCandidate) == CompositionHash.Compute(c.CompositionBinding.Selected)
                && c.AcceptedSelectionEnvelope.HasSameContent(view.TradeSelection.Result), "OC.CONTRACT.UPSTREAM_INVALID");
            Check(c.Reservation.Order is { OrderId: > 0 } o && o.WorkflowId == c.WorkflowId.Value
                && o.PortfolioId == selected.PortfolioId && o.FundId == selected.FundId
                && o.TradeSelectionResultId == selected.ResultId && o.TradeSelectionResultHash == c.AcceptedSelectionEnvelope.PayloadSha256
                && c.Reservation.Trades.Length == 1 && c.Reservation.Trades[0].OrderId == o.OrderId && c.Reservation.Trades[0].TradeId > 0,
                "OC.CONTRACT.RESERVATION_INVALID");
        }
        Check(c.ExpiresAtUtc > c.RequestedAtUtc && c.EvaluatedAtUtc <= c.RequestedAtUtc && c.ExpiresAtUtc <= view.ExpiresAtUtc
            && c.ExpiresAtUtc <= c.SelectionBinding.ValidUntilUtc && c.ExpiresAtUtc <= c.Reservation.Order.ExpiresAtUtc, "OC.CONTRACT.VALUE_RANGE");
        Check(MessagePackBinarySerializer.MeasureContent(c.MarketSnapshot) <= 524288
            && MessagePackBinarySerializer.MeasureContent(c.CompositionBinding) <= 262144
            && MessagePackBinarySerializer.MeasureContent(c) <= 1048576 && MessagePackBinarySerializer.MeasureEncoded(c) <= 1048576,
            "OC.CONTRACT.PAYLOAD_SIZE");
        Check(c.InputSha256 == c.Fingerprint(), "OC.CONTRACT.HASH");
        return errors;
    }
    sealed class FieldRules : BaseValidationRules
    {
        public ValidationError[] Execute(ExecuteOrderCompositionPipelineCommand c) => Validate(c, new Rules());
        sealed class Rules : AbstractValidator<ExecuteOrderCompositionPipelineCommand>
        {
            public Rules()
            {
                RuleFor(x => x.SchemaVersion).Equal((short)1);
                RuleFor(x => x.CommandId).NotEmpty(); RuleFor(x => x.PostEvents).Equal(false);
                RuleFor(x => x.WorkflowId.Value).NotEmpty(); RuleFor(x => x.InputWorkflowRevision).GreaterThan(0);
                RuleFor(x => x.CorrelationId).NotEmpty(); RuleFor(x => x.CausationId).NotEmpty();
                RuleFor(x => x.RequestedAtUtc).Must(TradeSelection.TradeSelectionContracts.Utc);
                RuleFor(x => x.EvaluatedAtUtc).Must(TradeSelection.TradeSelectionContracts.Utc);
                RuleFor(x => x.ExpiresAtUtc).Must(TradeSelection.TradeSelectionContracts.Utc);
                RuleFor(x => x.WorkflowView).NotNull(); RuleFor(x => x.TriggerEvent).NotNull();
                RuleFor(x => x.AcceptedSelectionEnvelope).NotNull(); RuleFor(x => x.SelectionBinding).NotNull();
                RuleFor(x => x.CompositionBinding).NotNull(); RuleFor(x => x.MarketSnapshot).NotNull();
                RuleFor(x => x.Reservation).NotNull(); RuleFor(x => x.InputSha256).Length(64);
                When(x => x.CompositionBinding is not null, () =>
                {
                    RuleFor(x => x.CompositionBinding.SchemaVersion).Equal((short)1);
                    RuleFor(x => x.CompositionBinding.Selected).NotNull();
                    RuleFor(x => x.CompositionBinding.Rules).NotNull();
                    RuleFor(x => x.CompositionBinding.RulesDefinition).NotNull();
                    RuleFor(x => x.CompositionBinding.RulesSchema).NotNull();
                });
                When(x => x.WorkflowView is not null, () =>
                {
                    RuleFor(x => x.WorkflowView.TriggerEvent).NotNull();
                    RuleFor(x => x.WorkflowView.TradeSelection).NotNull();
                });
                When(x => x.Reservation is not null, () =>
                {
                    RuleFor(x => x.Reservation.Order).NotNull();
                    RuleFor(x => x.Reservation.Trades).NotNull();
                });
                When(x => x.MarketSnapshot is not null, () =>
                {
                    RuleFor(x => x.MarketSnapshot.SchemaVersion).Equal(1);
                    RuleFor(x => x.MarketSnapshot.SnapshotId).NotEmpty();
                    RuleFor(x => x.MarketSnapshot.GenerationId).NotEmpty();
                    RuleFor(x => x.MarketSnapshot.ScopeId).NotEmpty();
                    RuleFor(x => x.MarketSnapshot.ScopeToken).NotEmpty();
                    RuleFor(x => x.MarketSnapshot.Instruments).Must(x => !x.IsDefault && x.Length <= 512
                        && x.All(i => i is not null && i.Instrument is not null && i.Instrument.Quote is not null));
                });
            }
        }
    }
}
