using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>One immutable exit intent per position, committed with its authorizing plan and retried across restarts.</summary>
public sealed class IronCondorAutomaticExitDispatcher(IDbContextFactory databases, IPostgresEventTransaction transactions,
    IActorProducer producer, TimeProvider clock)
{
    internal static Task EnqueueAsync(IEnlistedPostgresTransaction tx,IronCondorTradePlanUpdatedEvent source,CancellationToken token)
    {
        var plan=source.Plan;
        if(!plan.RequiresExit || plan.State!=TradePlanState.ExitRequired || !plan.Position.IsOpen
            || plan.Position.Phase!=StrategyPositionPhase.MarkToMarket || plan.Action is not (TradePlanAction.ExitAtMarket or TradePlanAction.ExitAtLimit))return Task.CompletedTask;
        return tx.ExecuteAsync("INSERT INTO trade_plan.iron_condor_exit_intent(positionid,payload) VALUES($1,$2) ON CONFLICT(positionid) DO NOTHING;",
            [plan.Position.Id.PositionId,MessagePackBinarySerializer.Shared.Serialize(source)],token);
    }

    public static StartIronCondorExitPositionWorkflowCommand Command(IronCondorTradePlanUpdatedEvent source)
    {
        var id=new ExitPositionWorkflowId(source.Plan.Position.Id,source.Plan.ValueDate,
            TradePlanContractIdentity.DeterministicId($"{source.Plan.Position.Id.Format()}|automatic-iron-condor-close-v1"));
        return new() {EntityId=id,CommandId=TradePlanContractIdentity.DeterministicId($"{id.Format()}|{StartIronCondorExitPositionWorkflowCommand.Verb}"),
            Subject=new(ActorType.Command,StartIronCondorExitPositionWorkflowCommand.Actor,StartIronCondorExitPositionWorkflowCommand.Verb,id.Format()),ExitPlan=source};
    }

    internal async Task<bool> HasIntentAsync(StrategyPositionId position, CancellationToken token) =>
        (await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync(
            "SELECT 1 FROM trade_plan.iron_condor_exit_intent WHERE positionid=$1;",
            [position.PositionId],r=>r.GetInt32(0),ct),token).ConfigureAwait(false)).Count != 0;

    public async Task<bool> RunOneAsync(CancellationToken token)
    {
        if(PostgresDatabaseIdentity.Parse(databases.ActorEventSourceDb.ConnectionString)!=PostgresDatabaseIdentity.Parse(databases.TradePlanDb.ConnectionString))
            throw new InvalidOperationException("Automatic exit intents require the plan and source log to share PostgreSQL.");
        var now=clock.GetUtcNow();var lease=Guid.NewGuid();
        var rows=await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync("""
            WITH next AS (SELECT positionid FROM trade_plan.iron_condor_exit_intent
                WHERE nextattempt<=$1 AND (state='Pending' OR (state='Working' AND leaseuntil<=$1))
                ORDER BY nextattempt,positionid FOR UPDATE SKIP LOCKED LIMIT 1)
            UPDATE trade_plan.iron_condor_exit_intent i SET state='Working',leaseid=$2,leaseuntil=$3,attempts=attempts+1
            FROM next WHERE next.positionid=i.positionid RETURNING i.positionid,i.payload,i.attempts;
            """,[now,lease,now.AddMinutes(2)],r=>(Id:r.GetGuid(0),Source:MessagePackBinarySerializer.Shared.Deserialize<IronCondorTradePlanUpdatedEvent>(r.GetFieldValue<byte[]>(1)),Attempts:r.GetInt32(2)),ct),token).ConfigureAwait(false);
        if(rows.Count==0)return false;var item=rows.Single();string? failure=null;
        try
        {
            var current=await databases.TradeDb.GetStrategyPositionAsync(item.Source.Plan.Position.Id,token).ConfigureAwait(false);
            if(current is null)throw new InvalidOperationException("EXIT.AUTO.POSITION_MISSING");
            if(current.IsOpen)
            {
                var command=Command(item.Source);
                var existing=await databases.TradePlanDb.GetCurrentExitWorkflowAsync(command.EntityId.Position,command.EntityId.ValueDate,token).ConfigureAwait(false);
                if(existing?.WorkflowId!=command.EntityId)
                {
                    var reply=await producer.RequestAsync<StartIronCondorExitPositionWorkflowCommand,ExitPositionWorkflowId,GuidResult>(command.Subject,command,command.EntityId,token).ConfigureAwait(false);
                    existing=await databases.TradePlanDb.GetCurrentExitWorkflowAsync(command.EntityId.Position,command.EntityId.ValueDate,token).ConfigureAwait(false);
                    if(existing?.WorkflowId!=command.EntityId)throw new InvalidOperationException($"EXIT.AUTO.START_UNCONFIRMED:{reply.ErrorMessage}");
                }
            }
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(Exception error){failure=error.Message;}
        await transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("""
            UPDATE trade_plan.iron_condor_exit_intent SET state=$1,leaseid=NULL,leaseuntil=NULL,lasterror=$2,nextattempt=$3
            WHERE positionid=$4 AND leaseid=$5;
            """,[failure is null?"Dispatched":"Pending",failure is null?string.Empty:failure[..Math.Min(1500,failure.Length)],
                clock.GetUtcNow().AddSeconds(Math.Min(60,Math.Max(5,item.Attempts*5))),item.Id,lease],ct),token).ConfigureAwait(false);
        return true;
    }
}
