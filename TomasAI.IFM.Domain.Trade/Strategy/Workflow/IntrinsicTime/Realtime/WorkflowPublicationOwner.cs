using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Application.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Recovery-only owner evidence. Never reads configuration or market inputs and never redoes eligibility.</summary>
public sealed class WorkflowPublicationOwner(IPostgresEventTransaction transactions):IWorkflowPublicationOwner
{
    public async Task<IntrinsicTimeStrategyWorkflowView?> ReadLegacySnapshotAsync(string streamId,string sourceHash,CancellationToken token)
    {
        // Migration only. Compare the full original routed source, never a user supplied
        // same-key alias, before promoting an old frame into the canonical journal.
        long before=long.MaxValue;
        for(var page=0;page<16;page++)
        {
            var rows=await transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT l.streamversion,l.eventversion,l.eventpayload FROM event_log l JOIN event_stream_id s USING(eventstreamid) JOIN event_name_id n USING(eventnameid) WHERE s.eventstream=$1 AND l.streamversion<$2 AND n.eventtypename LIKE '%WorkflowStrategyStateUpdatedEvent%' ORDER BY l.streamversion DESC LIMIT 256;",[streamId,before],row=>(Revision:row.GetInt64(0),EventId:row.GetInt64(1),Payload:row.GetFieldValue<byte[]>(2)),ct),token).ConfigureAwait(false);
            if(rows.Count==0)return null;
            foreach(var row in rows)
            {
                var source=EventLogMessagePackCodec.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(row.EventId,row.Payload);
                var routed=source with{Subject=new(ActorType.Realtime,"IntrinsicTimeStrategyWorkflowRealtime",WorkflowStrategyStateUpdatedEvent.Verb,source.EntityId.Format())};
                if(WorkflowRequestHash.Compute(routed)==sourceHash||WorkflowRequestHash.Compute(routed with{EventId=row.EventId})==sourceHash
                    ||source.State.SelectionDispatch is{} selection&&WorkflowRequestHash.Compute(selection)==sourceHash
                    ||source.State.CompositionExecution is{} composition&&WorkflowRequestHash.Compute(composition)==sourceHash)return source.State;
            }
            before=rows[^1].Revision;
        }
        throw new IOException("Legacy workflow source proof exceeds the bounded migration window; frame remains pending.");
    }
    public Task<IntrinsicTimeStrategyWorkflowView?> ReadCurrentAsync(Guid workflowId,CancellationToken token)=>transactions.ExecuteOnceAsync(async(db,ct)=>
    {
        var rows=await db.QueryAsync(TradeDbWorkflowRuntimeSql.ReadDecisionOutcome,[workflowId],row=>row.GetFieldValue<byte[]>(0),ct).ConfigureAwait(false);
        return rows.Count==1?MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(rows[0]):null;
    },token);
    public async Task<WorkflowPublicationDisposition?> ReconcileAsync(WorkflowPublication item,IIntrinsicTimeStrategyWorkflowRealtimeContext context,CancellationToken token)
    {
        var expected=new CommandAuditMessagePackCodec().Serialize(item.Command);
        var observed=await transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.ExecuteAsync(TradeDbWorkflowRuntimeSql.LockDecisionStream,[item.Command.StreamId],ct).ConfigureAwait(false);
            var audited=await db.QueryAsync("SELECT commandpayloadsha256,commandpayload,commandpayloadformat,commandpayloadversion FROM command_log WHERE commandid=$1 AND streamid=$2;",[item.Command.CommandId,item.Command.StreamId],row=>(Hash:row.GetFieldValue<byte[]>(0),Payload:row.GetFieldValue<byte[]>(1),Format:row.GetInt16(2),Version:row.GetInt32(3)),ct).ConfigureAwait(false);
            if(audited.Count>0)
            {
                var row=audited[0];
                if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(row.Payload),row.Hash))throw new InvalidDataException("Authoritative workflow receipt checksum is invalid.");
                if(!CryptographicOperations.FixedTimeEquals(expected.Sha256,row.Hash)&&!LegacyReservationPresentationMatches(item,row.Payload,row.Format,row.Version))throw new InvalidDataException("Authoritative workflow command hash conflicts with captured output.");
                return (Accepted:true,View:(IntrinsicTimeStrategyWorkflowView?)null);
            }
            var rows=await db.QueryAsync(TradeDbWorkflowRuntimeSql.ReadDecisionOutcome,[item.Intent.WorkflowId.Value],row=>row.GetFieldValue<byte[]>(0),ct).ConfigureAwait(false);
            return (Accepted:false,View:rows.Count==1?MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(rows[0]):null);
        },token).ConfigureAwait(false);
        if(observed.Accepted)return WorkflowPublicationDisposition.Accepted;
        var current=observed.View;
        if(current is null)throw new IOException("Authoritative workflow source is unavailable; publication remains owned.");
        if(current.WorkflowRevision!=item.Intent.WorkflowRevision||current.CurrentStage!=item.Intent.CurrentStage||current.Status!=WorkflowStrategyMachineStatus.Started)
            return WorkflowPublicationDisposition.Superseded;
        var financial=WorkflowPublicationDelivery.IsFinancial(item);
        var now=context.TimeProvider.GetUtcNow().UtcDateTime;
        var originalSession=item.Intent.DecisionRuntimeSessionId!=Guid.Empty?item.Intent.DecisionRuntimeSessionId:item.Session;
        var changedSession=item.Intent.DecisionRuntimeVersion>0&&originalSession!=Guid.Empty&&originalSession!=context.DecisionRuntime?.SessionId;
        var expired=now>=WorkflowCalculationRuntime.EffectiveDeadline(current);
        if(!financial&&(changedSession||expired))
        {
            var reason=changedSession?WorkflowLocalDispatcher.InterruptedSource:WorkflowLocalDispatcher.DeadlineSource;
            var stop=new RedispatchCurrentStrategyPipelineCommand{CommandId=WorkflowPublicationDelivery.Key(item.Intent,"retire/"+reason),EntityId=current.EntityId,WorkflowId=current.WorkflowId,
                ExpectedWorkflowRevision=current.WorkflowRevision,ExpectedStage=current.CurrentStage,RequestedAtUtc=item.Intent.UpdatedAtUtc,RequestedBy=reason,
                Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,RedispatchCurrentStrategyPipelineCommand.Verb,current.EntityId.Format())};
            // The authoritative owner chooses interrupted/expired No Trade; proof/deadline checks are never bypassed.
            var stopping=context.SendAsync<RedispatchCurrentStrategyPipelineCommand,IntrinsicTimeStrategyWorkflowEntityId>(stop,stop.EntityId).AsTask();
            try{await stopping.WaitAsync(token).ConfigureAwait(false);}
            catch(OperationCanceledException){_=ObserveLateAsync(stopping);throw;}
            // A successful transport reply is not a durable owner receipt. Re-read the exact
            // stopping command under the same serialization lock before relinquishing ownership.
            var stopped=new CommandAuditMessagePackCodec().Serialize(stop);
            var confirmed=await transactions.ExecuteOnceAsync(async(db,ct)=>
            {
                await db.ExecuteAsync(TradeDbWorkflowRuntimeSql.LockDecisionStream,[stop.StreamId],ct).ConfigureAwait(false);
                var rows=await db.QueryAsync("SELECT commandpayloadsha256 FROM command_log WHERE commandid=$1 AND streamid=$2;",[stop.CommandId,stop.StreamId],row=>row.GetFieldValue<byte[]>(0),ct).ConfigureAwait(false);
                if(rows.Count!=1)return false;
                if(!CryptographicOperations.FixedTimeEquals(stopped.Sha256,rows[0]))throw new InvalidDataException("Workflow interruption receipt conflicts with captured intent.");
                return true;
            },token).ConfigureAwait(false);
            if(!confirmed)throw new IOException("Workflow interruption is not durably accepted; output remains owned.");
            return changedSession?WorkflowPublicationDisposition.Interrupted:WorkflowPublicationDisposition.Expired;
        }
        return null;
    }
    static bool LegacyReservationPresentationMatches(WorkflowPublication item,byte[] payload,short format,int version)
    {
        if(item.Command is not CompleteTradeSelectionReservationCommand current)return false;
        var codec=new CommandAuditMessagePackCodec();
        var original=(CompleteTradeSelectionReservationCommand)codec.Deserialize(typeof(CompleteTradeSelectionReservationCommand),payload,format,version);
        if(original.Reservation.Disposition is not(ReservationDisposition.IdempotentReplay or ReservationDisposition.Committed)
            ||current.Reservation.Disposition!=ReservationDisposition.Committed||!codec.MatchesStoredEncoding(original,payload))return false;
        var normalized=original with{Reservation=original.Reservation with{Disposition=ReservationDisposition.Committed}};
        // Only the known legacy presentation field can differ. IDs, hash, times and financial receipt all stay exact.
        return CryptographicOperations.FixedTimeEquals(codec.Serialize(normalized).Sha256,codec.Serialize(current).Sha256);
    }
    static async Task ObserveLateAsync(Task pending){try{await pending.ConfigureAwait(false);}catch(Exception){/* The exact pending record remains authoritative until recovery proves acceptance. */}}
}
