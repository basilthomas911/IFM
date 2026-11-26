using System.Globalization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using IronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Restartable execution descriptions and final-plan-before-feed-cleanup ordering.</summary>
public sealed class IronCondorCloseProgress(IDbContextFactory databases, IPostgresEventTransaction transactions,
    IActorProducer producer, IMarketDataApi marketData, TradeRealtimeCacheRegistry cache, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken token)
    {
        var pending = await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync(
            "SELECT payload,lastprogress,lasterror FROM trade_plan.iron_condor_exit_intent WHERE NOT finalized ORDER BY positionid LIMIT 128;",
            [], r=>(Source:MessagePackBinarySerializer.Shared.Deserialize<IronCondorTradePlanUpdatedEvent>(r.GetFieldValue<byte[]>(0)),Last:r.GetString(1),Error:r.GetString(2)),ct),token).ConfigureAwait(false);
        List<Exception> failures = [];
        foreach (var item in pending)
            try { await UpdateAsync(item.Source, item.Last, item.Error, token).ConfigureAwait(false); }
            catch (Exception error) when (!token.IsCancellationRequested) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Some iron-condor close updates remain pending.", failures);
    }

    async Task UpdateAsync(IronCondorTradePlanUpdatedEvent source, string last, string dispatchError, CancellationToken token)
    {
        var decision = source.Plan;
        var position = await databases.TradeDb.GetStrategyPositionAsync(decision.Position.Id,token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Close progress position is missing.");
        var workflowId = IronCondorAutomaticExitDispatcher.Command(source).EntityId;
        var workflow = await databases.TradePlanDb.GetCurrentExitWorkflowAsync(workflowId.Position,workflowId.ValueDate,token).ConfigureAwait(false);
        var order = workflow?.RiskDecision?.TradeOrder;
        if (order is not null) order = await databases.TradeDb.GetTradeOrderAsync(order.Id,token).ConfigureAwait(false) ?? order;
        var execution = order?.BoundExecutionAttemptId is { } attempt ?
            await databases.TradeDb.GetOrderExecutionAsync(order.Id,attempt,token).ConfigureAwait(false) : null;
        var trade = await databases.TradeDb.GetEstablishedTradeAsync(position.Id.Trade).ConfigureAwait(false);
        if (IronCondorClosePlan.IsSettled(position) && order?.Status != TradeOrderStatus.Completed)
        {
            // A cancelled/rejected automatic attempt may have been completed by an explicit replacement close.
            // Follow the authentic final fill's execution, rather than treating the abandoned workflow as settlement.
            var finalAttempt=position.ClosingFills.OrderByDescending(fill=>fill.FilledAtUtc)
                .Select(fill=>fill.ExecutionAttemptId).FirstOrDefault(id=>id!=Guid.Empty);
            if (finalAttempt!=Guid.Empty)
            {
                var settled=await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync(
                    "SELECT payload FROM trade.order_execution WHERE executionattemptid=$1;",[finalAttempt],
                    r=>MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(r.GetFieldValue<byte[]>(0)),ct),token).ConfigureAwait(false);
                var actual=settled.SingleOrDefault(item=>item.PositionType==TradeOrderPositionType.Closing && item.TargetPositionId==position.Id);
                if(actual is not null) {
                    execution=actual;
                    order=await databases.TradeDb.GetTradeOrderAsync(actual.TradeOrderId,token).ConfigureAwait(false);
                }
            }
        }
        // Route control carries the authoritative position revision, not optional trade-read enrichment.
        // A later commission update on the trade must not rewrite this revision's captured control body.
        var routePosition = position;
        if (trade is not null) position = position with { OpeningCommission=position.OpeningCommission ?? trade.OpeningCommission,
            ContractCashMultiplier=position.ContractCashMultiplier ?? (trade.Legs.Length>0 && trade.Legs.All(l=>l.CashMultiplier>0)
                && trade.Legs.Select(l=>l.CashMultiplier).Distinct().Count()==1 ? trade.Legs[0].CashMultiplier : null) };
        // Workflow completion is only an order handoff. Settlement owns the position/trade close boundary.
        // CompleteTradeOrder is emitted only after confirmed accounting and Fund close handoffs have succeeded.
        var closed = IronCondorClosePlan.IsSettled(position) && trade?.Status == EstablishedTradeStatus.Closed
            && order?.Status == TradeOrderStatus.Completed;
        if (IronCondorClosePlan.IsSettled(position) && !closed) return;
        var progress = closed ? string.Create(CultureInfo.InvariantCulture,
                $"all closing fills and position settlement confirmed; order {order?.Id.Format() ?? "unavailable"}; {position.ClosingFills.Length} fill records")
            : execution is not null ? string.Create(CultureInfo.InvariantCulture,
                $"order {execution.TradeOrderId.Format()}, attempt {execution.ExecutionAttemptId:N}, {execution.Status}; filled {execution.CumulativeFilledQuantity}/{execution.OrderQuantity}; {execution.RejectionDetail ?? ""}")
            : $"workflow {workflow?.State.ToString() ?? "start pending"}; order {(order is null ? "not submitted" : order.Id.Format()+" "+order.Status)}{(dispatchError.Length==0 ? "" : "; dispatch retry pending: "+dispatchError)}";
        var stamp = $"{position.RouteGeneration}|{(closed ? position.PositionSequence : 0)}|{progress}";
        StrategyTradePlanSnapshot? plan = null;
        if (stamp != last || closed)
        {
            var id = new IronCondorTradePlanId(position.Id, decision.ValueDate);
            var proposed = new UpdateIronCondorTradePlanCommand {
                EntityId=id, Subject=new(ActorType.Function,UpdateIronCondorTradePlanCommand.Actor,UpdateIronCondorTradePlanCommand.Verb,id.Format()),
                CommandId=TradePlanContractIdentity.DeterministicId($"{source.Id:N}|close-progress|{stamp}"),
                Position=position, Parameters=decision.Parameters, SourceEventId=source.Id,
                RequestedAtUtc=clock.GetUtcNow().UtcDateTime, ClosingDecision=decision, ClosingProgress=progress
            };
            // The retry payload must be byte-for-byte stable even if a new market mark arrives after a timeout.
            var prepared = await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync("""
                UPDATE trade_plan.iron_condor_exit_intent SET pendingprogress=COALESCE(pendingprogress,$1),
                    pendingstamp=COALESCE(pendingstamp,$2) WHERE positionid=$3
                RETURNING pendingprogress,pendingstamp;
                """,[MessagePackBinarySerializer.Shared.Serialize(proposed),stamp,position.Id.PositionId],
                r=>(Command:MessagePackBinarySerializer.Shared.Deserialize<UpdateIronCondorTradePlanCommand>(r.GetFieldValue<byte[]>(0)),Stamp:r.GetString(1)),ct),token).ConfigureAwait(false);
            var command=prepared.Single().Command;
            var response = await producer.RequestFunctionAsync<UpdateIronCondorTradePlanCommand,IronCondorTradePlanId,
                FunctionResult<IronCondorTradePlanUpdatedEvent,TradePlanFailedEvent<IronCondorTradePlanId>>>(command.Subject,command,id,token).ConfigureAwait(false);
            plan = response.Value?.Completed?.Plan ?? throw new InvalidOperationException($"Close progress plan was not committed: {response.ErrorMessage}");
            await PublishAsync(trade,position,plan,closed ? "Closed — stopping live feed" : "Closing",token).ConfigureAwait(false);
            await transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync(
                "UPDATE trade_plan.iron_condor_exit_intent SET lastprogress=$1,pendingprogress=NULL,pendingstamp=NULL WHERE positionid=$2;",[prepared.Single().Stamp,position.Id.PositionId],ct),token).ConfigureAwait(false);
            // A recovered earlier progress command must complete before a later final close is published.
            if (prepared.Single().Stamp != stamp) return;
        }
        if (!closed || plan is null) return;
        await CompleteClosedAsync(source, routePosition, position, trade, plan, token).ConfigureAwait(false);
    }

    /// <summary>Finishes the already-settled close; route capture precedes feed shutdown and terminal finalization.</summary>
    internal async Task CompleteClosedAsync(IronCondorTradePlanUpdatedEvent source, StrategyPositionSnapshot routePosition,
        StrategyPositionSnapshot position, EstablishedTradeDefinition? trade, StrategyTradePlanSnapshot plan, CancellationToken token)
    {
        // Persisted final plan and its publication precede all trade-scoped subscription cleanup.
        var routes = ClosedRoutes(source, routePosition);
        await producer.SendAsync<OpenPositionRoutesChangedEvent,StrategyPositionId>(routes.Subject,routes,token).ConfigureAwait(false);
        // Disable all retained dates for this trade, leaving owners for other positions untouched.
        var retainedDates=await transactions.ExecuteOnceAsync((tx,ct)=>tx.QueryAsync(
            "SELECT DISTINCT value_date FROM trade.live_feed_recovery WHERE trade_id=$1;",[position.Id.Trade.Format()],r=>r.GetFieldValue<DateOnly>(0),ct),token).ConfigureAwait(false);
        foreach (var date in retainedDates.Concat([source.Plan.ValueDate,position.ValueDate]).Distinct())
            if (!await marketData.DisableTradeFeedRecoveryAsync(position.Id.Trade.Format(),date,token).ConfigureAwait(false))
                throw new InvalidOperationException("Trade closing-feed cleanup was not confirmed.");
        await PublishAsync(trade,position,plan,"Closed — live feed stopped",token).ConfigureAwait(false);
        cache.Remove(position.Id.Trade);
        await transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync(
            "UPDATE trade_plan.iron_condor_exit_intent SET finalized=true WHERE positionid=$1;",[position.Id.PositionId],ct),token).ConfigureAwait(false);
    }

    /// <summary>Retains the original close provenance while giving each authoritative closed revision its own control identity.</summary>
    internal static OpenPositionRoutesChangedEvent ClosedRoutes(IronCondorTradePlanUpdatedEvent source, StrategyPositionSnapshot position)
        => MessageOrigins.Restore(new OpenPositionRoutesChangedEvent {
            Subject = new(ActorType.Realtime,FuturesOptionRealtimeActor.ActorName,OpenPositionRoutesChangedEvent.Verb,position.Id.Format()),
            EntityId = position.Id,
            Id = TradePlanContractIdentity.DeterministicId($"{source.Id:N}|closed-routes/v2|{position.Id.Format()}|{position.RouteGeneration}|{position.PositionSequence}"),
            Position = position, ReceivedOn = source.ReceivedOn, CommandId = source.CommandId,
            EventId = source.EventId, AggregateId = source.AggregateId, EventSource = source.EventSource
        }, MessageOrigins.Get(source));

    async Task PublishAsync(EstablishedTradeDefinition? trade, StrategyPositionSnapshot position, StrategyTradePlanSnapshot plan,
        string status, CancellationToken token)
    {
        if (trade is null) throw new InvalidOperationException("Close progress trade is missing.");
        var current = cache.Find(position.Id.Trade) ?? cache.Register(trade,position.ValueDate);
        current.Apply(position); current.Apply(plan);
        var notification = TradeRealtimeSnapshotChangedEvent.From(current.PublishLifecycleStatus(status));
        await producer.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(notification.Subject,notification,token).ConfigureAwait(false);
    }
}
