using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Owns legacy reservation uncertainty beyond callback lifetime and process restart.</summary>
public static class WorkflowReservationRecovery
{
    const string Marker="WorkflowReservationRecovery";
    public static bool IsWork(WorkflowPublication item)=>item.Command is RedispatchCurrentStrategyPipelineCommand{RequestedBy:Marker};
    public static void Track(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowStrategyStateUpdatedEvent source)
    {
        if(context.Publications is not{} journal)return;var view=source.State;var key=WorkflowPublicationDelivery.Key(view,"reservation-work");
        var command=new RedispatchCurrentStrategyPipelineCommand{CommandId=key,EntityId=view.EntityId,WorkflowId=view.WorkflowId,ExpectedWorkflowRevision=view.WorkflowRevision,
            ExpectedStage=view.CurrentStage,RequestedAtUtc=view.UpdatedAtUtc,RequestedBy=Marker,
            Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,RedispatchCurrentStrategyPipelineCommand.Verb,view.EntityId.Format())};
        journal.Prepare(key,WorkflowPublicationDelivery.Digest(view),view,context.DecisionRuntime?.SessionId??Guid.Empty,command);
    }
    public static void Complete(IIntrinsicTimeStrategyWorkflowRealtimeContext context,IntrinsicTimeStrategyWorkflowView intent)=>context.Publications?.Confirm(WorkflowPublicationDelivery.Key(intent,"reservation-work"),WorkflowPublicationDisposition.Accepted);
    public static async Task RecoverAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,CancellationToken token)
    {
        if(context.Publications is not{} journal)return;
        List<Exception>? failures=null;
        foreach(var work in journal.Pending().Where(IsWork))
        {
            token.ThrowIfCancellationRequested();
            try{await RecoverOneAsync(context,journal,work,token).ConfigureAwait(false);}
            catch(Exception error)when(!token.IsCancellationRequested){(failures??=[]).Add(error);}
        }
        if(failures is not null)throw new AggregateException("Reservation owners remain unresolved.",failures);
    }
    static async Task RecoverOneAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,WorkflowPublicationJournal journal,WorkflowPublication work,CancellationToken token)
    {
            var view=work.Intent;var handoff=view.CompositionHandoff??throw new InvalidDataException("Reservation recovery lost its durable request.");
            var reply=await context.PortfolioCommands.ReconcileCompositionAsync(handoff.Request,view.SelectionBinding!.PortfolioSnapshot,token).ConfigureAwait(false);
            if(reply is null)throw new IOException("Reservation owner outcome is unavailable.");
            if(!reply.Success)
            {
                if(reply.ErrorCode==PortfolioErrorCodes.ReservationExpiredWithoutEffects){journal.Confirm(work.Key,WorkflowPublicationDisposition.Expired);return;}
                throw new IOException(reply.ErrorMessage??"Reservation owner has not resolved the original operation.");
            }
            if(reply.Value is null)throw new IOException("Reservation owner returned no receipt.");
            var source=new WorkflowStrategyStateUpdatedEvent{Id=work.Key,EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,State=view};
            await IntrinsicTimeStrategyWorkflowRealtimeActor.CompleteReservationAsync(context,source,reply.Value).ConfigureAwait(false);
            var current=context.PublicationOwner is{} owner?await owner.ReadCurrentAsync(view.WorkflowId.Value,token).ConfigureAwait(false):await context.ReadActiveWorkflowAsync(view.EntityId,token).ConfigureAwait(false);
            if(current is null)throw new IOException("Reservation cleanup requires an authoritative workflow disposition.");
            if(current.Status!=WorkflowStrategyMachineStatus.Started)
            {
                var stopped=view with{Status=current.Status,CompositionHandoff=handoff with{Status=CompositionHandoffStatus.Stopped,Reservation=reply.Value}};
                await IntrinsicTimeStrategyWorkflowRealtimeActor.ReconcileStoppedSelectionAsync(context,source with{State=stopped}).ConfigureAwait(false);
            }
            journal.Confirm(work.Key,WorkflowPublicationDisposition.Accepted);
    }
}

public sealed class WorkflowPublicationRecoveryService(Func<IIntrinsicTimeStrategyWorkflowRealtimeContext> contextFactory,ILogger<WorkflowPublicationRecoveryService> logger):BackgroundService
{
    int legacyCursor;
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var context=contextFactory();using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            if(!context.IsReady)continue;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(10));
            try{await WorkflowPublicationDelivery.RecoverPendingAsync(context,deadline.Token).ConfigureAwait(false);}
            catch(Exception error)when(!token.IsCancellationRequested){logger.LogWarning(error,"Workflow publication or reservation recovery remains pending.");}
            using var reservations=CancellationTokenSource.CreateLinkedTokenSource(token);reservations.CancelAfter(TimeSpan.FromSeconds(10));
            try{await WorkflowReservationRecovery.RecoverAsync(context,reservations.Token).ConfigureAwait(false);}
            catch(Exception error)when(!token.IsCancellationRequested){logger.LogWarning(error,"Workflow reservation recovery remains pending.");}
            using var migration=CancellationTokenSource.CreateLinkedTokenSource(token);migration.CancelAfter(TimeSpan.FromSeconds(10));
            try{await IntrinsicTimeStrategyWorkflowRealtimeActor.RecoverLegacyPendingAsync(context,migration.Token,legacyCursor).ConfigureAwait(false);}
            catch(Exception error)when(!token.IsCancellationRequested){logger.LogWarning(error,"Legacy workflow migration remains pending.");}
            legacyCursor=(legacyCursor+64)%4096;
        }while(await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }
}
