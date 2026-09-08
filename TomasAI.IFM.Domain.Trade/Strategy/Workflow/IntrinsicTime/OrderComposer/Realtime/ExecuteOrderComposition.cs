using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;

/// <summary>Requests acceptance first; only the committed dispatch may cross the composition pipeline boundary.</summary>
public static class ExecuteOrderComposition
{
    public static async ValueTask ExecuteAsync(this WorkflowStrategyStateUpdatedEvent snapshot,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        try { await PrepareOrDispatchAsync(snapshot, context).ConfigureAwait(false); }
        catch (CompositionMarketSourceException failure)
        {
            var view = snapshot.State; var now = context.TimeProvider.GetUtcNow().UtcDateTime;
            var command = new FailOrderCompositionCommand
            {
                CommandId = snapshot.Id,
                Subject = new(ActorType.Command, FailOrderCompositionCommand.Actor, FailOrderCompositionCommand.Verb, view.EntityId.Format()),
                EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = snapshot.Id, CorrelationId = view.CorrelationId, CausationId = snapshot.Id, FailedAtUtc = now,
                Failure = new() { ErrorCode = StartOrderCompositionPipelineCommand.ErrorId, ErrorType = "OrderCompositionPreparationFailed",
                    ErrorMessage = failure.Code, ErrorData = failure.Code, FailedAtUtc = now }
            };
            await context.SendAsync<FailOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, view.EntityId).ConfigureAwait(false);
        }
    }

    static async ValueTask PrepareOrDispatchAsync(WorkflowStrategyStateUpdatedEvent snapshot,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        var view = snapshot.State;
        if (view.CompositionDispatch is { } dispatch)
        {
            var now = context.TimeProvider.GetUtcNow();
            TradeSelectionHandoff.ValidateStart(dispatch, now.UtcDateTime);
            if (dispatch.MarketEvidence is not { } reference || reference.ValidUntilUtc <= now)
                throw new CompositionMarketSourceException("AcceptedSnapshotExpired");
            // Verify immutable storage on redispatch. Never substitute a new market capture or new IDs.
            var saved = await context.CompositionPreparations.ReadAsync(new(reference.WorkflowId,
                reference.PreparationRevision, reference.InputSha256), default).ConfigureAwait(false)
                ?? throw new InvalidDataException("Accepted market preparation is missing.");
            CompositionPreparationService.Validate(saved);
            if (CompositionPreparationAcceptance.Reference(saved) != reference)
                throw new InvalidDataException("Accepted market evidence changed.");
            await context.SendAsync<StartOrderCompositionPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(dispatch, view.EntityId).ConfigureAwait(false);
            return;
        }
        var key = CompositionPreparationAcceptance.Key(view);
        var prepared = await context.CompositionPreparations.ReadAsync(key, default).ConfigureAwait(false);
        if (prepared is null)
        {
            var selection = TradeSelectionContracts.ReadResult(view.TradeSelection.Result!);
            var policy = TradeSelectionContracts.Policy(view.SelectionBinding!, selection.SelectedCandidate!.CompositionPolicyReference);
            var construction = SelectionConstructionPolicy.Read(policy.PayloadJson);
            if (construction.MarketData is not { } marketData)
                throw new CompositionMarketSourceException("CompositionUniverseUnqualified");
            CompositionMarketDataPlan plan;
            try
            {
                plan = marketData.Deserialize<CompositionMarketDataPlan>(new JsonSerializerOptions
                    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
                    ?? throw new CompositionMarketSourceException("CompositionUniverseUnqualified");
            }
            catch (JsonException) { throw new CompositionMarketSourceException("CompositionUniverseUnqualified"); }
            if (plan.Root != selection.SelectedCandidate.Product.Symbol)
                throw new CompositionMarketSourceException("CompositionUniverseUnqualified");
            var result = await context.CompositionMarketPreparation.PrepareAsync(key, plan,
                view.TriggerEvent.EntityId.TimePeriod.ToString(), new(view.CompositionHandoff!.Request.ExpiresAtUtc), default).ConfigureAwait(false);
            prepared = result.Preparation ?? throw new CompositionMarketSourceException(result.Failure?.Code ?? "CompositionPreparationUnavailable");
        }
        CompositionPreparationService.Validate(prepared);
        var command = new AcceptOrderCompositionPreparationCommand
        {
            // Snapshot identity was durably chosen before this notification; retries retain one command identity.
            CommandId = prepared.Snapshot.SnapshotId,
            Subject = new(ActorType.Command, AcceptOrderCompositionPreparationCommand.Actor,
                AcceptOrderCompositionPreparationCommand.Verb, view.EntityId.Format()),
            EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
            Evidence = CompositionPreparationAcceptance.Reference(prepared)
        };
        await context.SendAsync<AcceptOrderCompositionPreparationCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, view.EntityId).ConfigureAwait(false);
    }
}
