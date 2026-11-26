using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime;

/// <summary>Captures an original Function terminal before dispatch and confirms only owner success.</summary>
public static class RegimeFunctionTerminalDelivery
{
    static Guid Key(WorkflowStrategyStateUpdatedEvent source)=>IntrinsicTimeStrategyWorkflowRealtimeActor.DeterministicPipelineCommandId(source.State.WorkflowId,source.State.CurrentStage,source.State.WorkflowRevision);
    static string Hash(WorkflowStrategyStateUpdatedEvent request)=>WorkflowRequestHash.Compute(request);
    public static async ValueTask<bool> RecoverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent request)
    {
        var journal=context.RegimeFunctionFailures;
        if(journal?.Find(Key(request)) is not {} saved)return false;
        if(await WorkflowLegacyPublicationMigration.TryMigrateAsync(context,saved.InputHash,saved.Command,request).ConfigureAwait(false)){journal.MarkSent(saved.Key);return true;}
        if(!string.Equals(saved.InputHash,Hash(request),StringComparison.Ordinal))throw new InvalidDataException("Regime Function request changed under its original identity.");
        await SendSavedAsync(context,journal,saved).ConfigureAwait(false);return true;
    }
    public static async ValueTask CaptureAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent request,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)
    {
        if(context.Publications is not null){await WorkflowPublicationDelivery.PublishAsync(context,request.State,command).ConfigureAwait(false);return;}
        if(context.RegimeFunctionFailures is {} journal)
        {
            var origin=MessageOrigins.Get(request);
            _=MessageOrigins.ForPublication(command,context.TimeProvider,origin?.ClockDomainId??"system",origin?.RunId,origin?.Generation,command switch { FailRegimeDiscoveryCommand c=>c.CausationId,CompleteRegimeDiscoveryCommand c=>c.CausationId,_=>throw new InvalidDataException("Unsupported Regime terminal.") });
            await SendSavedAsync(context,journal,journal.Prepare(Key(request),Hash(request),()=>command)).ConfigureAwait(false);
        }
        else await SendCommandAsync(context,command).ConfigureAwait(false);
    }
    public static async ValueTask RecoverPendingAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        if(context.RegimeFunctionFailures is {} journal)
            foreach(var saved in journal.Pending())await SendSavedAsync(context,journal,saved).ConfigureAwait(false);
    }
    static async ValueTask SendSavedAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowRegimeFunctionFailures journal,WorkflowRegimeFunctionFailure saved)
    {
        if(!saved.Sent&&await WorkflowLegacyPublicationMigration.TryMigrateAsync(context,saved.InputHash,saved.Command).ConfigureAwait(false)){journal.MarkSent(saved.Key);return;}
        if(!saved.Sent)await SendCommandAsync(context,saved.Command).ConfigureAwait(false);
        journal.MarkSent(saved.Key);
    }
    static ValueTask SendCommandAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)=>command switch
    {
        FailRegimeDiscoveryCommand failure=>context.SendAsync<FailRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(failure,failure.EntityId),
        CompleteRegimeDiscoveryCommand completion=>context.SendAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(completion,completion.EntityId),
        _=>throw new InvalidDataException("Unsupported Regime Function terminal.")
    };
}
