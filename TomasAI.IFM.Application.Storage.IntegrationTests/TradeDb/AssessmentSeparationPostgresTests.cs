using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Tests.Support;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;
[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class AssessmentSeparationPostgresTests(PostgresTradePlanFixture fixture)
{
    public static IEnumerable<object[]> Cases()=>AssessmentSeparationScenario.Cases();
    [Theory,MemberData(nameof(Cases))]
    public async Task Native_assessment_and_selection_preserve_separate_outcomes_and_recover_once(string variant,TimeFrameType frame,string scenario,string? reason)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var x=await AssessmentSeparationScenario.Create(variant,frame,scenario);
        var factory=new Mock<IDbContextFactory>();
        var context=new TradeDbContext(new DbConnectionSettings().Add("TradeDbConnection",fixture.Connection,"System.Data.Postgres"),factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(f=>f.TradeDb).Returns(context);
        var source=new Mock<IEventSourceActorDbContext>();source.SetupGet(s=>s.ProviderName).Returns("System.Data.Postgres");source.SetupGet(s=>s.ConnectionString).Returns(fixture.Connection);
        var transactions=new Mock<IPostgresEventTransaction>();
        transactions.Setup(t=>t.ExecuteOnceAsync(It.IsAny<Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>>>(),It.IsAny<CancellationToken>()))
            .Returns((Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>> op,CancellationToken ct)=>fixture.Transactions.ExecuteOnceAsync(op,ct));
        transactions.Setup(t=>t.ExecuteOnceAsync(It.IsAny<Func<EnlistedEventTransaction,CancellationToken,Task<TradeSelectionFunctionCompletedEvent>>>(),It.IsAny<CancellationToken>()))
            .Returns((Func<EnlistedEventTransaction,CancellationToken,Task<TradeSelectionFunctionCompletedEvent>> op,CancellationToken ct)=>fixture.Transactions.ExecuteOnceAsync(op,ct));
        var assessmentEvent=MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent),x.Request,
            new MarketConditionExecutionCompleted(x.Assessment,x.Snapshot)),new Clock(x.Request.RequestedAtUtc)).Completed!;
        var assessmentRepository=new MarketConditionAssessmentStateRepository(Mock.Of<IEventSourceActorStateFactory>(),source.Object,Mock.Of<IActorService>(),NullLogger<MarketConditionAssessmentStateRepository>.Instance,factory.Object,transactions.Object);
        await assessmentRepository.CommitAsync(Mock.Of<IFunctionActorContext>(),x.Request,assessmentEvent);
        var restoredAssessment=await context.GetMarketConditionAssessmentAsync(x.Request.WorkflowId);
        Assert.NotNull(restoredAssessment);Assert.True(restoredAssessment.Result.HasValidPayloadSha256());
        Assert.Equal(AssessmentAvailability.Available,restoredAssessment.Result.ReadAssessmentResult().Assessment.Availability);
        Assert.Equal(x.Snapshot.ComputeHash(),restoredAssessment.Snapshot.ComputeHash());
        var selectionEvent=TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionCompletedEvent),x.Selection,x.Result),new Clock(x.Selection.EvaluatedAtUtc)).Completed!;
        var selectionRepository=new TradeSelectionFunctionStateRepository(Mock.Of<IEventSourceActorStateFactory>(),source.Object,Mock.Of<IActorService>(),NullLogger<TradeSelectionFunctionStateRepository>.Instance,factory.Object,transactions.Object);
        await selectionRepository.CommitAsync(Mock.Of<IFunctionActorContext>(),x.Selection,selectionEvent);
        var restoredSelection=await context.GetTradeSelectionInvocationAsync(x.Selection.WorkflowId,x.Result.InvocationId);
        Assert.NotNull(restoredSelection);Assert.True(restoredSelection.Result.HasValidPayloadSha256());
        var result=TradeSelectionContracts.ReadResult(restoredSelection.Result);
        Assert.Equal(reason is null?SelectionOutcome.Selected:SelectionOutcome.NoTrade,result.Outcome);
        if(reason is not null)Assert.Contains(result.GlobalEvidence,e=>e.ReasonCode==reason);
        var replay=new TradeSelectionFunctionState();Assert.True(replay.Apply(restoredSelection,addEvent:false));
        Assert.True(replay.Matches(x.Selection));Assert.False(replay.TryComplete(restoredSelection,x.Selection));
        await Assert.ThrowsAsync<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditDuplicateException>(
            async()=>await selectionRepository.CommitAsync(Mock.Of<IFunctionActorContext>(),x.Selection,selectionEvent));
        Assert.Equal(1,await fixture.EventCountAsync(x.Selection.StreamId));
    }
    sealed class Clock(DateTime at):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(at); }
}
