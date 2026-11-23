using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresIronCondorAutomaticExitTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Close_description_is_committed_before_feed_cleanup_and_restart_retries_cleanup_without_repricing()
    {
        await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync(TradeSchemaDb.CreateStatement+TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb.CreateStatement,[],ct));
        var now=DateTime.UtcNow;var date=DateOnly.FromDateTime(now);
        var position=new StrategyPositionSnapshot {Id=StrategyPositionId.Create(new(Random.Shared.Next(70000000,80000000),703,1,1),TradeStrategyKind.IronCondor),
            StrategyKind=TradeStrategyKind.IronCondor,Phase=StrategyPositionPhase.Close,IsOpen=false,ValueDate=date,PositionSequence=2,RouteGeneration=1,AsOfUtc=now,
            ContractCashMultiplier=50,OpeningCommission=2,RealizedPnl=-2,ClosingFills=[new() {ExecutionAttemptId=Guid.NewGuid(),Commission=3}],
            Legs=Enumerable.Range(0,4).Select(i=>new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId="CLOSE"+i,SignedQuantity=0,CurrentPrice=1}).ToArray()};
        var source=new IronCondorTradePlanUpdatedEvent {Id=Guid.NewGuid(),Plan=new() {Position=position with {IsOpen=true,Phase=StrategyPositionPhase.MarkToMarket},ValueDate=date,PlanRevision=1,
            State=TradePlanState.ExitRequired,Action=TradePlanAction.ExitAtMarket,RequiresExit=true,CalculatedAtUtc=now.AddMinutes(-1),ReasonCode="SHORT_PUT_DELTA_LIMIT_REACHED",
            Explanation="Short put delta -0.3265 reached 0.30",IronCondorTradePlanSnapshot=new() {Position=position,CalculationStatus="Complete"}}};
        var definition=new EstablishedTradeDefinition {Id=position.Id.Trade,Status=EstablishedTradeStatus.Closed,StrategyKind=TradeStrategyKind.IronCondor,
            Legs=position.Legs.Select(l=>new TradeLegDefinition {TradeLegId=l.TradeLegId,ContractId=l.ContractId,SignedQuantity=1,CashMultiplier=50}).ToArray()};
        var db=Substitute.For<ITradeDbContext>();
        db.GetStrategyPositionAsync(position.Id,Arg.Any<CancellationToken>()).Returns(Task.FromResult<StrategyPositionSnapshot?>(position));
        db.GetEstablishedTradeAsync(position.Id.Trade,Arg.Any<CancellationToken>()).Returns(Task.FromResult<EstablishedTradeDefinition?>(definition));
        var orderStatus=TradeOrderStatus.Executing;
        var closeOrder=new TradeOrderDefinition {Id=new(position.Id.Trade.PortfolioId,703,999),Status=TradeOrderStatus.Completed};
        var actualExecution=new OrderExecutionDefinition {TradeOrderId=closeOrder.Id,ExecutionAttemptId=position.ClosingFills[0].ExecutionAttemptId,
            PositionType=TradeOrderPositionType.Closing,TargetPositionId=position.Id,Status=OrderExecutionStatus.Filled,OrderQuantity=1,CumulativeFilledQuantity=1};
        await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("INSERT INTO trade.order_execution(portfolioid,fundid,orderid,executionattemptid,payload) VALUES($1,$2,$3,$4,$5);",
            [closeOrder.Id.PortfolioId,closeOrder.Id.FundId,closeOrder.Id.OrderId,actualExecution.ExecutionAttemptId,MessagePackBinarySerializer.Shared.Serialize(actualExecution)],ct));
        db.GetTradeOrderAsync(closeOrder.Id,Arg.Any<CancellationToken>()).Returns(_=>Task.FromResult<TradeOrderDefinition?>(closeOrder with {Status=orderStatus}));
        var plans=Substitute.For<TomasAI.IFM.Application.Storage.TradePlanDb.ITradePlanDbContext>();
        plans.GetCurrentExitWorkflowAsync(position.Id,date,Arg.Any<CancellationToken>()).Returns(Task.FromResult<ExitPositionWorkflowProjection?>(new() {
            WorkflowId=IronCondorAutomaticExitDispatcher.Command(source).EntityId,RiskDecision=new() {TradeOrder=null,ExecuteTradeOrder=false}}));
        var factory=Substitute.For<IDbContextFactory>();factory.TradeDb.Returns(db);factory.TradePlanDb.Returns(plans);
        await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("INSERT INTO trade_plan.iron_condor_exit_intent(positionid,payload,state) VALUES($1,$2,'Dispatched');",[position.Id.PositionId,MessagePackBinarySerializer.Shared.Serialize(source)],ct));
        var calls=new List<string>();var committed=new Dictionary<Guid,IronCondorTradePlanUpdatedEvent>();
        var producer=Substitute.For<IActorProducer>();
        producer.RequestFunctionAsync<UpdateIronCondorTradePlanCommand,TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId,FunctionResult<IronCondorTradePlanUpdatedEvent,TradePlanFailedEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>>>(Arg.Any<TomasAI.IFM.Shared.EventModelActor.ActorSubject>(),Arg.Any<UpdateIronCondorTradePlanCommand>(),Arg.Any<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>(),Arg.Any<CancellationToken>())
            .Returns(info=>new ValueTask<ServiceResult<FunctionResult<IronCondorTradePlanUpdatedEvent,TradePlanFailedEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>>>>(CommitAsync(info.Arg<UpdateIronCondorTradePlanCommand>())));
        async Task<ServiceResult<FunctionResult<IronCondorTradePlanUpdatedEvent,TradePlanFailedEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>>>> CommitAsync(UpdateIronCondorTradePlanCommand command)
        {
            if(!committed.TryGetValue(command.CommandId,out var fact)) {
                var plan=TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model.IronCondorClosePlan.Create(command.ClosingDecision!,position,command.ClosingProgress!,2,now);
                fact=new() {Id=command.CommandId,Plan=plan};
                await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{await TradePlanProjectionWriter.WriteAcceptedAsync(fixture.Plans.DbWriter,tx,plan,ct);return true;});
                committed[command.CommandId]=fact;calls.Add("commit");
            }
            return new(FunctionResult<IronCondorTradePlanUpdatedEvent,TradePlanFailedEvent<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId>>.Complete(fact));
        }
        var market=Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();var stops=0;
        market.DisableTradeFeedRecoveryAsync(position.Id.Trade.Format(),date,Arg.Any<CancellationToken>()).Returns(async _=>{
            var saved=await fixture.Plans.GetCurrentAsync(position.Id,TradeStrategyKind.IronCondor,date);
            Assert.Equal(TradePlanState.Closed,saved!.State);Assert.Contains("-0.3265",saved.Explanation);
            Assert.Contains("all closing fills",saved.Explanation);Assert.Equal(-105,saved.IronCondorTradePlanSnapshot!.TradePnl);
            calls.Add("stop");if(++stops==1)throw new IOException("Injected feed release interruption");return true;
        });
        var cache=new TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.TradeRealtimeCacheRegistry();
        var worker=new IronCondorCloseProgress(factory,fixture.Transactions,producer,market,cache,TimeProvider.System);
        await worker.RunAsync(default);
        Assert.Empty(calls); // The zero-quantity position alone must not race ahead of financial settlement.
        orderStatus=TradeOrderStatus.Completed;
        await Assert.ThrowsAsync<AggregateException>(()=>worker.RunAsync(default));
        await new IronCondorCloseProgress(factory,fixture.Transactions,producer,market,cache,TimeProvider.System).RunAsync(default);
        Assert.Equal(new[]{"commit","stop","stop"},calls);Assert.Single(committed);
        Assert.Null(cache.Find(position.Id.Trade));
        var done=await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ScalarAsync("SELECT finalized FROM trade_plan.iron_condor_exit_intent WHERE positionid=$1;",[position.Id.PositionId],ct));
        Assert.Equal(true,done);
        await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync(TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb.CreateStatement,[],ct));
        var restored=await fixture.Plans.GetCurrentAsync(position.Id,TradeStrategyKind.IronCondor,date);
        Assert.Equal(TradePlanContractIdentity.PlanHash(restored!),restored!.ContentHash);
    }
    [Fact]
    public async Task One_immutable_intent_survives_repeated_plans_concurrent_claims_restart_and_lost_start_reply()
    {
        await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync(TradeSchemaDb.CreateStatement+TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb.CreateStatement,[],ct));
        var db=new TradeDbContext(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres"),Moq.Mock.Of<IDbContextFactory>(),Moq.Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        var p=new StrategyPositionSnapshot {Id=StrategyPositionId.Create(new(Random.Shared.Next(60000000,70000000),703,1,1),TradeStrategyKind.IronCondor),StrategyKind=TradeStrategyKind.IronCondor,IsOpen=true,Phase=StrategyPositionPhase.MarkToMarket,PositionSequence=1,AsOfUtc=DateTime.UtcNow,
            Legs=[new() {TradeLegId=Guid.NewGuid(),ContractId="AUTO-TEST",SignedQuantity=-1,OpeningPrice=10,CurrentPrice=10}]};
        await db.UpsertStrategyPositionAsync(p);
        var source=new IronCondorTradePlanUpdatedEvent {Id=Guid.NewGuid(),Plan=new() {Position=p,ValueDate=DateOnly.FromDateTime(DateTime.UtcNow),State=TradePlanState.ExitRequired,Action=TradePlanAction.ExitAtMarket,RequiresExit=true}};
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{
            await IronCondorAutomaticExitDispatcher.EnqueueAsync(tx,source,ct);
            await IronCondorAutomaticExitDispatcher.EnqueueAsync(tx,source with {Id=Guid.NewGuid(),Plan=source.Plan with {PlanRevision=2}},ct);return true;
        });
        var queued=await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ScalarAsync("SELECT payload FROM trade_plan.iron_condor_exit_intent WHERE positionid=$1;",[p.Id.PositionId],ct));
        Assert.Equal(source.Id,MessagePackBinarySerializer.Shared.Deserialize<IronCondorTradePlanUpdatedEvent>((byte[])queued!).Id);
        var factory=Substitute.For<IDbContextFactory>();factory.TradeDb.Returns(db);factory.TradePlanDb.Returns(fixture.Plans);
        var eventDb=Substitute.For<IEventSourceActorDbContext>();eventDb.ConnectionString.Returns(fixture.Connection);factory.ActorEventSourceDb.Returns(eventDb);
        var producer=Substitute.For<IActorProducer>();var starts=0;
        producer.RequestAsync<StartIronCondorExitPositionWorkflowCommand,ExitPositionWorkflowId,GuidResult>(Arg.Any<TomasAI.IFM.Shared.EventModelActor.ActorSubject>(),Arg.Any<StartIronCondorExitPositionWorkflowCommand>(),Arg.Any<ExitPositionWorkflowId>(),Arg.Any<CancellationToken>())
            .Returns(info=>new ValueTask<ServiceResult<GuidResult>>(ConfirmStartAsync(info.Arg<StartIronCondorExitPositionWorkflowCommand>())));
        async Task<ServiceResult<GuidResult>> ConfirmStartAsync(StartIronCondorExitPositionWorkflowCommand cmd)
        {
            starts++;
            await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{await fixture.Plans.ProjectExitWorkflowAsync(tx,new ExitPositionWorkflowProjection {WorkflowId=cmd.EntityId,StrategyKind=TradeStrategyKind.IronCondor,State=ExitPositionWorkflowState.Started,StageRevision=1,UpdatedAtUtc=DateTime.UtcNow,SourcePlanEventId=source.Id,ExitPlan=source.Plan},ct);return true;});
            throw new TimeoutException("Injected lost reply after committed workflow start");
        }
        var clock=new Clock(DateTimeOffset.UtcNow.AddMinutes(1));
        try {
            var worker=new IronCondorAutomaticExitDispatcher(factory,fixture.Transactions,producer,clock);
            await Task.WhenAll(Enumerable.Range(0,4).Select(_=>worker.RunOneAsync(default)));
            Assert.Equal(1,starts);
            clock.Now=clock.Now.AddMinutes(1);
            await new IronCondorAutomaticExitDispatcher(factory,fixture.Transactions,producer,clock).RunOneAsync(default);
            Assert.Equal(1,starts);
            var state=await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ScalarAsync("SELECT state FROM trade_plan.iron_condor_exit_intent WHERE positionid=$1;",[p.Id.PositionId],ct));
            Assert.Equal("Dispatched",state);
            Assert.Equal(IronCondorAutomaticExitDispatcher.Command(source).CommandId,IronCondorAutomaticExitDispatcher.Command(source).CommandId);
        } finally {
            await db.UpsertStrategyPositionAsync(p with {IsOpen=false,PositionSequence=2});
            await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("DELETE FROM trade_plan.iron_condor_exit_intent WHERE positionid=$1;",[p.Id.PositionId],ct));
        }
    }
    sealed class Clock(DateTimeOffset now):TimeProvider {public DateTimeOffset Now{get;set;}=now;public override DateTimeOffset GetUtcNow()=>Now;}
}
