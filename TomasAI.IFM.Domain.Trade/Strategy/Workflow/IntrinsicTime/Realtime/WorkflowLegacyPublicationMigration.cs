using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Promotes a legacy full-notification hash only after its original accepted source is proven.</summary>
public static class WorkflowLegacyPublicationMigration
{
    public static async ValueTask<bool> TryMigrateAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context,
        string inputHash,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command,WorkflowStrategyStateUpdatedEvent? incoming=null)
    {
        if(context.Publications is null)return false;
        IntrinsicTimeStrategyWorkflowView? intent=null;
        if(incoming is not null&&WorkflowRequestHash.Compute(incoming)==inputHash)intent=incoming.State;
        else if(context.PublicationOwner is{} owner)
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            intent=await owner.ReadLegacySnapshotAsync(command.StreamId,inputHash,deadline.Token).ConfigureAwait(false);
        }
        if(intent is null)throw new IOException("Original legacy workflow source cannot be proven; retain terminal.");
        if(incoming is not null&&WorkflowPublicationDelivery.Digest(incoming.State)!=WorkflowPublicationDelivery.Digest(intent))
            throw new InvalidDataException("Redispatch changed accepted workflow intent.");
        await WorkflowPublicationDelivery.PublishAsync(context,intent,command,origin:MessageOrigins.Get(command)).ConfigureAwait(false);
        return true;
    }
}
