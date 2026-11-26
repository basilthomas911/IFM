using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

public interface IWorkflowPublicationOwner
{
    Task<IntrinsicTimeStrategyWorkflowView?> ReadLegacySnapshotAsync(string streamId,string sourceHash,CancellationToken token)=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(null);
    Task<IntrinsicTimeStrategyWorkflowView?> ReadCurrentAsync(Guid workflowId,CancellationToken token)=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(null);
    Task<WorkflowPublicationDisposition?> ReconcileAsync(WorkflowPublication publication,IIntrinsicTimeStrategyWorkflowRealtimeContext context,CancellationToken token);
}

/// <summary>One immutable output boundary for all workflow stages. Pure atomic batch contexts leave the journal null.</summary>
public static class WorkflowPublicationDelivery
{
    public static Guid Key(IntrinsicTimeStrategyWorkflowView intent,string purpose="terminal")=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"WorkflowOutput/v1/{intent.EntityId.Format()}/{intent.WorkflowId}/{intent.CurrentStage}/{intent.WorkflowRevision}/{purpose}")).AsSpan(0,16));
    public static string Digest(IntrinsicTimeStrategyWorkflowView intent)=>WorkflowRequestHash.Compute(intent);
    internal static bool IsFinancial(WorkflowPublication item)=>item.Intent.EntryCommit is not null
        ||item.Command is CompleteRiskManagementCommand{EntryCommit:not null} or CompleteRiskManagementCommand{PortfolioDecision:not null}
            or CompleteTradeSelectionReservationCommand or AdvanceRiskFinancialHandoffCommand;
    public static async ValueTask<bool> RecoverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,IntrinsicTimeStrategyWorkflowView intent,string purpose="terminal")
    {
        if(context.Publications?.Find(Key(intent,purpose))is not{} saved)return false;
        if(saved.IntentHash!=Digest(intent))throw new InvalidDataException("Accepted workflow input changed under its stage identity.");
        await DeliverAsync(context,saved).ConfigureAwait(false);return true;
    }
    public static ValueTask PublishAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent source,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command,string purpose="terminal")
        =>PublishAsync(context,source.State,command,purpose,MessageOrigins.Get(source));
    public static async ValueTask PublishAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,IntrinsicTimeStrategyWorkflowView intent,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command,string purpose="terminal",MessageOrigin? origin=null)
    {
        if(context.Publications is not{} journal){await SendAsync(context,command).ConfigureAwait(false);return;}
        origin??=MessageOrigins.Get(intent.TriggerEvent);
        var cause=command.GetType().GetProperty("CausationId")?.GetValue(command) is Guid id&&id!=Guid.Empty?id:intent.CausationId;
        _=MessageOrigins.ForPublication(command,context.TimeProvider,origin?.ClockDomainId??"system",origin?.RunId,origin?.Generation,cause!=Guid.Empty?cause:null);
        var item=journal.Prepare(Key(intent,purpose),Digest(intent),intent,context.DecisionRuntime?.SessionId??Guid.Empty,command);
        await DeliverAsync(context,item).ConfigureAwait(false);
    }
    public static async ValueTask RecoverPendingAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,CancellationToken token=default)
    {
        if(context.Publications is not{} journal)return;
        List<Exception>? failures=null;
        foreach(var saved in journal.Pending().Where(item=>!WorkflowReservationRecovery.IsWork(item)))
        {
            token.ThrowIfCancellationRequested();
            try{await DeliverAsync(context,saved,token).ConfigureAwait(false);}
            catch(Exception error)when(!token.IsCancellationRequested){(failures??=[]).Add(error);}
        }
        if(failures is not null)throw new AggregateException("One or more workflow outputs remain pending.",failures);
    }
    public static async ValueTask<bool> InterruptOldSessionAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,IntrinsicTimeStrategyWorkflowView intent)
    {
        if(context.Publications is null||intent.DecisionRuntimeVersion==0||intent.DecisionRuntimeSessionId==Guid.Empty||intent.EntryCommit is not null
            ||intent.DecisionRuntimeSessionId==context.DecisionRuntime?.SessionId)return false;
        var stop=new RedispatchCurrentStrategyPipelineCommand{CommandId=Key(intent,"session-interruption"),EntityId=intent.EntityId,WorkflowId=intent.WorkflowId,
            ExpectedWorkflowRevision=intent.WorkflowRevision,ExpectedStage=intent.CurrentStage,RequestedAtUtc=intent.UpdatedAtUtc,RequestedBy=Runtime.WorkflowLocalDispatcher.InterruptedSource,
            Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,RedispatchCurrentStrategyPipelineCommand.Verb,intent.EntityId.Format())};
        await PublishAsync(context,intent,stop,"session-interruption").ConfigureAwait(false);return true;
    }
    static async ValueTask DeliverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowPublication item,CancellationToken token=default)
    {
        if(item.Disposition!=WorkflowPublicationDisposition.Pending)return;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        if(context.PublicationOwner is{} owner)
        {
            var resolved=await owner.ReconcileAsync(item,context,deadline.Token).ConfigureAwait(false);
            if(resolved is{} disposition){context.Publications!.Confirm(item.Key,disposition);return;}
        }
        // The original live command is retained by the journal for reference-identity guarded approvals.
        var financial=IsFinancial(item);
        var remaining=Runtime.WorkflowCalculationRuntime.EffectiveDeadline(item.Intent)-context.TimeProvider.GetUtcNow().UtcDateTime;
        var wait=financial?TimeSpan.FromSeconds(5):remaining>TimeSpan.Zero&&remaining<TimeSpan.FromSeconds(2)?remaining:TimeSpan.FromSeconds(2);
        var sending=SendAsync(context,item.Command).AsTask();
        try{await sending.WaitAsync(wait,deadline.Token).ConfigureAwait(false);}
        catch(TimeoutException){_=ObserveUncertainAsync(sending);throw new IOException("Workflow owner reply exceeded the delivery budget; captured output remains pending.");}
        catch(OperationCanceledException){_=ObserveUncertainAsync(sending);throw;}
        if(context.PublicationOwner is{} confirmation)
        {
            var result=await confirmation.ReconcileAsync(item,context,deadline.Token).ConfigureAwait(false);
            if(result is not{} disposition)throw new IOException("Workflow owner acceptance is unconfirmed; output remains pending.");
            context.Publications!.Confirm(item.Key,disposition);
        }
        else context.Publications!.Confirm(item.Key,WorkflowPublicationDisposition.Accepted); // Explicit isolated test receiver.
    }
    static async Task ObserveUncertainAsync(Task sending){try{await sending.ConfigureAwait(false);}catch(Exception){/* The captured output is recovered from authoritative owner evidence. */}}
    internal static ValueTask SendAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)=>command switch
    {
        CompleteRegimeDiscoveryCommand c=>context.SendAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        FailRegimeDiscoveryCommand c=>context.SendAsync<FailRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        CompleteMarketConditionCommand c=>context.SendAsync<CompleteMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        FailMarketConditionCommand c=>context.SendAsync<FailMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        CompleteTradeSelectionCommand c=>context.SendAsync<CompleteTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        FailTradeSelectionCommand c=>context.SendAsync<FailTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        CompleteTradeSelectionReservationCommand c=>context.SendAsync<CompleteTradeSelectionReservationCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        TimeoutTradeSelectionCommand c=>context.SendAsync<TimeoutTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        CompleteOrderCompositionCommand c=>context.SendAsync<CompleteOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        FailOrderCompositionCommand c=>context.SendAsync<FailOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        TimeoutOrderCompositionCommand c=>context.SendAsync<TimeoutOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        AcceptOrderCompositionPreparationCommand c=>context.SendAsync<AcceptOrderCompositionPreparationCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        PrepareRiskManagementCommand c=>context.SendAsync<PrepareRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        CompleteRiskManagementCommand c=>context.SendAsync<CompleteRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        FailRiskManagementCommand c=>context.SendAsync<FailRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        AdvanceRiskFinancialHandoffCommand c=>context.SendAsync<AdvanceRiskFinancialHandoffCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        RedispatchCurrentStrategyPipelineCommand c=>context.SendAsync<RedispatchCurrentStrategyPipelineCommand,IntrinsicTimeStrategyWorkflowEntityId>(c,c.EntityId),
        _=>throw new InvalidDataException("Unsupported workflow publication command.")
    };
}
