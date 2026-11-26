using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Realtime;

/// <summary>Captures an original Function failure before dispatch and confirms only owner success.</summary>
public static class AssessmentFunctionTerminalDelivery
{
    static Guid Key(WorkflowStrategyStateUpdatedEvent source)=>IntrinsicTimeStrategyWorkflowRealtimeActor.DeterministicPipelineCommandId(source.State.WorkflowId,source.State.CurrentStage,source.State.WorkflowRevision);
    static string Hash(WorkflowStrategyStateUpdatedEvent request)=>WorkflowRequestHash.Compute(request);
    public static async ValueTask<bool> RecoverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent request)
    {
        var journal=context.AssessmentFunctionFailures;
        if(journal?.Find(Key(request)) is not {} saved)return false;
        if(await WorkflowLegacyPublicationMigration.TryMigrateAsync(context,saved.InputHash,saved.Command,request).ConfigureAwait(false)){journal.MarkSent(saved.Key);return true;}
        if(!string.Equals(saved.InputHash,Hash(request),StringComparison.Ordinal))throw new InvalidDataException("Assessment Function request changed under its original identity.");
        await SendSavedAsync(context,journal,saved).ConfigureAwait(false);return true;
    }
    public static async ValueTask CaptureAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent request,FailMarketConditionCommand command)
    {
        if(context.Publications is not null){await WorkflowPublicationDelivery.PublishAsync(context,request.State,command).ConfigureAwait(false);return;}
        if(context.AssessmentFunctionFailures is {} journal)
        {
            var origin=MessageOrigins.Get(request);
            _=MessageOrigins.ForPublication(command,context.TimeProvider,origin?.ClockDomainId??"system",origin?.RunId,origin?.Generation,command.CausationId);
            await SendSavedAsync(context,journal,journal.Prepare(Key(request),Hash(request),()=>command)).ConfigureAwait(false);
        }
        else await SendCommandAsync(context,command).ConfigureAwait(false);
    }
    public static async ValueTask RecoverPendingAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        if(context.AssessmentFunctionFailures is {} journal)
            foreach(var saved in journal.Pending())await SendSavedAsync(context,journal,saved).ConfigureAwait(false);
    }
    static async ValueTask SendSavedAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowAssessmentFunctionFailures journal,WorkflowAssessmentFunctionFailure saved)
    {
        if(!saved.Sent&&await WorkflowLegacyPublicationMigration.TryMigrateAsync(context,saved.InputHash,saved.Command).ConfigureAwait(false)){journal.MarkSent(saved.Key);return;}
        if(!saved.Sent)await SendCommandAsync(context,saved.Command).ConfigureAwait(false);
        journal.MarkSent(saved.Key);
    }
    static ValueTask SendCommandAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,FailMarketConditionCommand command)
        =>context.SendAsync<FailMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(command,command.EntityId);
}
