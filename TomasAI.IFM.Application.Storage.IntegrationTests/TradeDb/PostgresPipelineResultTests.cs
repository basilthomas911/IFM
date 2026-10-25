using System;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresPipelineResultTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_assessment_repository_commits_source_audit_and_both_read_models_together(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var request=AssessmentFixture.Command();
        var snapshot=new MarketConditionAssessmentSnapshot
        {
            SnapshotId=Guid.NewGuid(),MarketProfileId=request.MarketProfileId,InstrumentRoot=request.InstrumentRoot,
            TargetHorizon=request.TargetHorizon,ReferenceInstrumentId="ES.TEST",EvaluatedAtUtc=request.RequestedAtUtc,
            Quote=new(5000,5000.25m,10,10),SessionState=MarketSessionStatus.Open,EventContext=AssessmentEventContext.Clear,
            Observations=request.ParameterSet.Sources.Select(x=>new AssessmentObservation
            { SourceId=x.SourceId,ObservedAtUtc=request.RequestedAtUtc.AddSeconds(-1),ReceivedAtUtc=request.RequestedAtUtc,
              Sequence=10,Availability=MarketSourceAvailability.Available,Validity=MarketSourceValidity.Valid,Value=0,Unit="ratio" }).ToArray(),
            CalendarEvidence=new(){CheckedAtUtc=request.RequestedAtUtc,CoverageConfirmed=true,ValidUntilUtc=request.RequestedAtUtc.AddHours(1)}
        }.Seal();
        var result=new MarketConditionAssessmentCalculator().Calculate(request,snapshot,request.CommandId);
        var candidate=new MarketConditionAssessmentCompletedEvent
        {
            Subject=new(ActorType.Function,MarketConditionAssessmentCompletedEvent.Actor,MarketConditionAssessmentCompletedEvent.Verb,request.EntityId.Format()),
            Id=result.ResultId,EntityId=request.WorkflowEntityId,CommandId=request.CommandId,AggregateId=request.EntityId.Format(),
            EventSource="MarketConditionPipelineFunctionActor",ReceivedOn=request.RequestedAtUtc,WorkflowId=request.WorkflowId,
            InputWorkflowRevision=request.InputWorkflowRevision,Result=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope.CreateAssessment(result),
            Snapshot=snapshot,CompletedAtUtc=request.RequestedAtUtc,RequestFingerprint=new string('A',64)
        };
        var settings=new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();
        var context=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(context);
        var source=new Mock<IEventSourceActorDbContext>();source.SetupGet(x=>x.ProviderName).Returns("System.Data.Postgres");
        source.SetupGet(x=>x.ConnectionString).Returns(fixture.Connection);
        var owner=new Mock<IPostgresEventTransaction>();
        owner.Setup(x=>x.ExecuteOnceAsync(It.IsAny<Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>>>(),It.IsAny<CancellationToken>()))
            .Returns((Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>> operation,CancellationToken ct)=>
                fixture.Transactions.ExecuteOnceAsync(async(db,token)=>{var completion=await operation(db,token);if(fail)throw new InvalidOperationException("Injected before commit");return completion;},ct));
        var repository=new MarketConditionAssessmentStateRepository(Mock.Of<IEventSourceActorStateFactory>(),source.Object,
            Mock.Of<IActorService>(),NullLogger<MarketConditionAssessmentStateRepository>.Instance,factory.Object,owner.Object);
        repository.UsesAtomicPersistence.Should().BeTrue();
        async Task Save()=>await repository.CommitAsync(Mock.Of<IFunctionActorContext>(),request,candidate);
        if(fail)await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();else await Save();
        (await fixture.EventCountAsync(request.StreamId)).Should().Be(fail?0:1);
        var exact=await context.GetMarketConditionAssessmentAsync(request.WorkflowId);
        if(fail)exact.Should().BeNull();else exact!.Id.Should().Be(candidate.Id);
        var history=await context.GetMarketConditionAssessmentHistoryAsync(request.MarketProfileId,request.InstrumentRoot,
            request.TargetHorizon,request.RequestedAtUtc.AddSeconds(1),100);
        history.Any(x=>x.WorkflowId==request.WorkflowId).Should().Be(!fail);
        var audits=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM command_log WHERE commandid=$1;",[request.CommandId],ct));
        Convert.ToInt64(audits).Should().Be(fail?0:1);
    }
    sealed class FixedClock(DateTime now):TimeProvider {public override DateTimeOffset GetUtcNow()=>new(now);}
}
