using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Checks market evidence against the exact committed selection/reservation and freezes dispatch identity.</summary>
public static class CompositionPreparationAcceptance
{
    public static CompositionPreparationKey Key(IntrinsicTimeStrategyWorkflowView view)
    {
        var input = view.SelectionBinding?.SchemaVersion == 2
            ? new { view.EntityId, view.WorkflowId, view.WorkflowRevision, view.SelectionBinding, view.TradeSelection.Result, CompositionHandoff = (object?)null }
            : new { view.EntityId, view.WorkflowId, view.WorkflowRevision, view.SelectionBinding, view.TradeSelection.Result, CompositionHandoff = (object?)view.CompositionHandoff };
        return new(view.WorkflowId.Value, view.WorkflowRevision, PricingSemanticHash.Compute(input));
    }

    public static CompositionEvidenceReference Reference(CompositionPreparation value) => new(value.Key.WorkflowId,
        value.Key.InputRevision, value.Key.InputSha256, value.Snapshot.SnapshotId, value.Digest, value.Snapshot.ValidUntilUtc);

    public static StartOrderCompositionPipelineCommand Accept(IntrinsicTimeStrategyWorkflowView view,
        CompositionPreparation prepared, CompositionEvidenceReference expected, Guid commandId, DateTime now)
    {
        CompositionPreparationService.Validate(prepared);
        var key = Key(view);
        var selection = TradeSelectionContracts.ReadResult(view.TradeSelection.Result!);
        var neutral = view.SelectionBinding?.SchemaVersion == 2;
        var handoff = view.CompositionHandoff;
        var compositionDeadline = neutral ? view.OrderComposition.ExpiresAtUtc : handoff?.Request.ExpiresAtUtc;
        if (prepared.Key != key || Reference(prepared) != expected || expected.ValidUntilUtc <= new DateTimeOffset(now)
            || prepared.Snapshot.Horizon != view.TriggerEvent.EntityId.TimePeriod.ToString()
            || neutral && view.CurrentStage != StrategyWorkflowStage.OrderComposition
            || !neutral && handoff is not { Status: CompositionHandoffStatus.Reserved }
            || compositionDeadline is null || prepared.Snapshot.ValidUntilUtc.UtcDateTime > compositionDeadline
            || prepared.Snapshot.Instruments.Any(x => (x.Instrument.Pricing?.Contract.Root ?? x.Instrument.FutureDefinition?.Root) != selection.SelectedCandidate?.Product.Symbol
                || (x.Instrument.Pricing?.Contract.Currency ?? x.Instrument.FutureDefinition?.Currency) != selection.SelectedCandidate.Product.Currency
                || (x.Instrument.Pricing?.Contract.Exchange ?? x.Instrument.FutureDefinition?.Exchange) != selection.SelectedCandidate.Product.Exchange))
            throw new InvalidDataException("Composition evidence is expired or belongs to different workflow inputs.");
        var revision = checked(view.WorkflowRevision + 1);
        var legacy = new IntrinsicTimeStrategyWorkflowState
        {
            EntityId = view.EntityId, WorkflowId = view.WorkflowId, TriggerEventId = view.TriggerEventId,
            CorrelationId = view.CorrelationId, WorkflowDefinitionVersion = view.WorkflowDefinitionVersion,
            Status = StrategyWorkflowStatus.Running, CurrentStage = StrategyWorkflowStage.OrderComposition,
            WorkflowRevision = revision, StartedAtUtc = view.StartedAtUtc, Outcome = view.Outcome,
            RegimeDiscovery = view.RegimeDiscovery, MarketCondition = view.MarketCondition, TradeSelection = view.TradeSelection,
            OrderComposition = view.OrderComposition with { InputWorkflowRevision = revision }, RiskManagement = view.RiskManagement,
            SelectionBinding = view.SelectionBinding, SelectionDispatch = view.SelectionDispatch,
            CompositionHandoff = neutral ? null : handoff,
            AssessmentBinding = view.AssessmentBinding, FundId = neutral ? 0 : view.FundId,
            MarketConditionParameterSet = view.MarketConditionParameterSet,
            MarketConditionParameterPayloadSha256 = view.MarketConditionParameterPayloadSha256,
            RegimeDiscoveryParameterSet = view.RegimeDiscoveryParameterSet,
            RegimeDiscoveryParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256
        };
        var dispatch = new StartOrderCompositionPipelineCommand
        {
            CommandId = commandId, Subject = new(ActorType.Command, StartOrderCompositionPipelineCommand.Actor,
                StartOrderCompositionPipelineCommand.Verb, view.EntityId.Format()),
            EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = revision,
            WorkflowState = legacy, TriggerEvent = view.TriggerEvent, CorrelationId = view.CorrelationId,
            CausationId = commandId, RequestedAtUtc = now, ExpectedCompletionAtUtc = compositionDeadline,
            AcceptedSelection = view.TradeSelection.Result, SelectionBinding = view.SelectionBinding,
            Reservation = neutral ? null : handoff!.Reservation, MarketEvidence = expected
        };
        TradeSelectionHandoff.ValidateStart(dispatch, now);
        return dispatch;
    }
}
