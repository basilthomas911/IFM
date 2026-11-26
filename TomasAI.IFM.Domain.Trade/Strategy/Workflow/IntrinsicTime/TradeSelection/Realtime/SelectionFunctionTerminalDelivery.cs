using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Realtime;

/// <summary>Captures an original Function failure before dispatch and confirms only owner success.</summary>
public static class SelectionFunctionTerminalDelivery
{
    static string Hash(ExecuteTradeSelectionPipelineCommand request)=>WorkflowRequestHash.Compute(request);
    public static async ValueTask<bool> RecoverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,ExecuteTradeSelectionPipelineCommand request)
    {
        var journal=context.SelectionFunctionFailures;
        if(journal?.Find(request.CommandId) is not {} saved)return false;
        if(!string.Equals(saved.InputHash,Hash(request),StringComparison.Ordinal))throw new InvalidDataException("Selector Function request changed under its original identity.");
        await SendSavedAsync(context,journal,saved).ConfigureAwait(false);return true;
    }
    public static async ValueTask CaptureAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,ExecuteTradeSelectionPipelineCommand request,FailTradeSelectionCommand command)
    {
        if(context.Publications is not null){await WorkflowPublicationDelivery.PublishAsync(context,request.WorkflowView,command).ConfigureAwait(false);return;}
        if(context.SelectionFunctionFailures is {} journal)
        {
            var origin=MessageOrigins.Get(request);
            _=MessageOrigins.ForPublication(command,context.TimeProvider,origin?.ClockDomainId??"system",origin?.RunId,origin?.Generation,request.CommandId);
            await SendSavedAsync(context,journal,journal.Prepare(request.CommandId,Hash(request),()=>command)).ConfigureAwait(false);
        }
        else await SendCommandAsync(context,command).ConfigureAwait(false);
    }
    public static async ValueTask RecoverPendingAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        if(context.SelectionFunctionFailures is {} journal)
            foreach(var saved in journal.Pending())await SendSavedAsync(context,journal,saved).ConfigureAwait(false);
    }
    static async ValueTask SendSavedAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowSelectionFunctionFailures journal,WorkflowSelectionFunctionFailure saved)
    {
        if(!saved.Sent&&await WorkflowLegacyPublicationMigration.TryMigrateAsync(context,saved.InputHash,saved.Command).ConfigureAwait(false)){journal.MarkSent(saved.Key);return;}
        if(!saved.Sent)await SendCommandAsync(context,saved.Command).ConfigureAwait(false);
        journal.MarkSent(saved.Key);
    }
    static ValueTask SendCommandAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,FailTradeSelectionCommand command)
        =>context.SendAsync<FailTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(command,command.EntityId);
}
