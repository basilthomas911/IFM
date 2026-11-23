using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using S=TomasAI.IFM.Tests.Support.DailyAtrFuturesScenario;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class PostgresDailyAtrFuturesTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(1,104,100)]
    [InlineData(-1,96,100)]
    public async Task Restart_retains_ATR_watermark_tightening_and_stop_then_flattens_without_fabricating_ticks(int sign,int extreme,int stopped)
    {
        var p=S.Position(sign,extreme) with{Id=StrategyPositionId.Create(new(801,701,1601,Random.Shared.Next(100000,900000)),TradeStrategyKind.FuturesOutright)};
        var algorithm=new FuturesTradePlanAlgorithm();
        var first=algorithm.Calculate(p,new(),null,S.Date,S.At.AddSeconds(1)).Snapshot;
        Assert.True(first.FuturesTrailTightened);
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{await fixture.Plans.ProjectMaterialAsync(tx,first,ct);return true;});
        var restored=await fixture.Plans.GetCurrentAsync(p.Id,p.StrategyKind,S.Date);
        Assert.NotNull(restored);Assert.Equal(first.ContentHash,restored.ContentHash);Assert.Equal(8,restored.Position.DailyFuturesEntry!.Threshold);
        Assert.Equal(first.Position.DailyFuturesEntry.Atr,restored.Position.DailyFuturesEntry.Atr);
        Assert.Equal(100,restored.FuturesStopPrice);Assert.True(restored.FuturesTrailTightened);
        var update=p with{PositionSequence=2,Legs=[p.Legs[0] with{CurrentPrice=stopped}]};
        var exit=algorithm.Calculate(update,new(),restored,S.Date,S.At.AddSeconds(1)).Snapshot;
        Assert.True(exit.RequiresExit);Assert.Equal(TradePlanAction.ExitAtMarket,exit.Action);
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{await fixture.Plans.ProjectMaterialAsync(tx,exit,ct);return true;});
        var after=await fixture.Plans.GetCurrentAsync(p.Id,p.StrategyKind,S.Date);
        Assert.Equal(exit.ContentHash,after!.ContentHash);Assert.Equal("PLAN.FUTURES.TRAILING_STOP",after.ReasonCode);
        var clock=DailyFuturesFlattenService.Create(restored.Position,DailyFuturesSession.FlattenUtc(S.Date))!;
        Assert.Equal(restored.Position.LastMarketObservationUtc,clock.State.LastMarketObservationUtc);
        Assert.Equal(restored.Position.Legs[0].LastPriceAtUtc,clock.State.Legs[0].LastPriceAtUtc);
        var flattened=algorithm.Calculate(clock.State,new(),restored,S.Date,clock.ReceivedOn).Snapshot;
        Assert.Equal("PLAN.DAILY.SESSION_FLATTEN",flattened.ReasonCode);
    }

    [Theory]
    [InlineData(IntrinsicTimeModeType.Trending)]
    [InlineData(IntrinsicTimeModeType.TrendExtremeChanged)]
    [InlineData(IntrinsicTimeModeType.TrendOvershootExtended)]
    public async Task Workflow_projection_preserves_frozen_DC_arm_and_consumed_opportunity(IntrinsicTimeModeType mode)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var db=new TradeDbContext(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres"),
            Mock.Of<IDbContextFactory>(),Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        var v=S.View(mode:mode) with{WorkflowRevision=1,Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.RegimeDiscovery};
        var bytes=MessagePackBinarySerializer.Shared.Serialize(v);
        var row=new IntrinsicTimeStrategyWorkflowReadModel(v.WorkflowId,v.EntityId.Format(),"DailyAtr",1,S.Contract,S.Date,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,v.TriggerEventId,v.CorrelationId,StrategyWorkflowStatus.Running,
            StrategyWorkflowOutcome.None,v.CurrentStage,1,1,1,bytes,"",v.StartedAtUtc,null,v.StartedAtUtc)
            {AssignmentKey=v.EntityId.Assignment!.QueueSuffix()};
        await db.UpsertIntrinsicTimeStrategyWorkflowAsync(row);
        var read=await db.GetIntrinsicTimeStrategyWorkflowAsync(v.WorkflowId);
        var restored=MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(read!.StatePayload);
        Assert.Equal(v.DailyFuturesEntry,restored.DailyFuturesEntry);
        Assert.Null(DailyFuturesEntryRules.Rejection(restored,RegimeDirection.Up,S.At.AddSeconds(1)));
        restored=restored with{DailyFuturesEntry=restored.DailyFuturesEntry! with{Consumed=true},WorkflowRevision=2};
        await db.UpsertIntrinsicTimeStrategyWorkflowAsync(row with{WorkflowRevision=2,StatePayload=MessagePackBinarySerializer.Shared.Serialize(restored)});
        var consumed=MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>((await db.GetIntrinsicTimeStrategyWorkflowAsync(v.WorkflowId))!.StatePayload);
        Assert.Equal("TS.DAILY.DC_CONSUMED",DailyFuturesEntryRules.Rejection(consumed,RegimeDirection.Up,S.At.AddSeconds(2)));
    }
}
