using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
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
public sealed class DailyFuturesGatePostgresTests(PostgresTradePlanFixture fixture)
{
    public static IEnumerable<object[]> Cases() => DailyFuturesGateTestScenario.Cases().Concat(new object[][] {
        ["v1-shallow", "LIQUIDITY_POOR"], ["v1-no-baseline", "ATR_NOT_READY"],
        ["v1-invalid-baseline", "ATR_NOT_READY"], ["v1-stale-baseline", "ATR_STALE"],
        ["v2-wide", "LIQUIDITY_POOR"], ["v2-blackout", "EVENT_BLACKOUT"],
        ["v2-not-dc", "NOT_DC_EVENT"], ["v2-premarket", "OUTSIDE_ENTRY_WINDOW"], ["v2-opposed", "DIRECTION_MISMATCH"], ["v2-range", "RANGEBOUND"] });
    [Theory, MemberData(nameof(Cases))]
    public async Task Atomic_results_recover_with_eligibility_reasons_hashes_and_original_indicator_times(string scenario,string? reason)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var (request,snapshot) = DailyFuturesGateTestScenario.Create(scenario.StartsWith("v1-",StringComparison.Ordinal) || scenario.StartsWith("v2-",StringComparison.Ordinal) ? scenario[3..] : scenario);
        if (scenario.StartsWith("v1-",StringComparison.Ordinal)) request = request with { CalculationPolicy = DailyFuturesGatePolicy.LegacyVersion };
        if (scenario.StartsWith("v2-",StringComparison.Ordinal)) request = request with { CalculationPolicy = DailyFuturesGatePolicy.PreviousVersion };
        var result = new MarketConditionAssessmentCalculator().Calculate(request,snapshot,request.CommandId);
        var candidate = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent),request,
            new MarketConditionExecutionCompleted(result,snapshot)),new Clock(request.RequestedAtUtc)).Completed!;
        var factory = new Mock<TomasAI.IFM.Application.Storage.IDbContextFactory>();
        var context = new TradeDbContext(new DbConnectionSettings().Add("TradeDbConnection",fixture.Connection,"System.Data.Postgres"),factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x => x.TradeDb).Returns(context);
        var source = new Mock<IEventSourceActorDbContext>(); source.SetupGet(x => x.ProviderName).Returns("System.Data.Postgres");
        source.SetupGet(x => x.ConnectionString).Returns(fixture.Connection);
        var owner = new Mock<IPostgresEventTransaction>();
        owner.Setup(x => x.ExecuteOnceAsync(It.IsAny<Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>>>(),It.IsAny<CancellationToken>()))
            .Returns((Func<EnlistedEventTransaction,CancellationToken,Task<MarketConditionAssessmentCompletedEvent>> operation,CancellationToken ct) => fixture.Transactions.ExecuteOnceAsync(operation,ct));
        var repository = new MarketConditionAssessmentStateRepository(Mock.Of<IEventSourceActorStateFactory>(),source.Object,
            Mock.Of<IActorService>(),NullLogger<MarketConditionAssessmentStateRepository>.Instance,factory.Object,owner.Object);
        await repository.CommitAsync(Mock.Of<IFunctionActorContext>(),request,candidate);
        var recovered = await context.GetMarketConditionAssessmentAsync(request.WorkflowId);
        Assert.NotNull(recovered); Assert.True(recovered.Result.HasValidPayloadSha256());
        Assert.Equal(candidate.Result.PayloadSha256,recovered.Result.PayloadSha256);
        Assert.Equal(snapshot.PayloadSha256,recovered.Snapshot.ComputeHash());
        Assert.Equal(snapshot.Observations.Single(o => o.SourceId == "Atr14").ObservedAtUtc,
            recovered.Snapshot.Observations.Single(o => o.SourceId == "Atr14").ObservedAtUtc);
        var restored = recovered.Result.ReadAssessmentResult();
        if (scenario.StartsWith("v1-",StringComparison.Ordinal) || scenario.StartsWith("v2-",StringComparison.Ordinal))
            Assert.StartsWith("Daily futures: No Trade; ",restored.SummaryText);
        Assert.Equal(reason is null ? AssessmentAvailability.Available : AssessmentAvailability.Unavailable,restored.Assessment.Availability);
        if (reason is null) Assert.Empty(restored.Assessment.LimitationReasons);
        if (reason is not null) Assert.Contains(DailyFuturesGatePolicy.Prefix + reason,restored.Assessment.LimitationReasons);
        var replay = new MarketConditionAssessmentState();
        Assert.True(replay.Apply(recovered,addEvent:false));
        Assert.True(replay.Matches(request));
        Assert.False(replay.TryComplete(recovered,request));
        // Function replay returns CompletedEvent from the restored state. An accidental
        // second raw commit is also rejected by the durable command-audit barrier.
        await Assert.ThrowsAsync<TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditDuplicateException>(
            async () => await repository.CommitAsync(Mock.Of<IFunctionActorContext>(),request,candidate));
        Assert.Equal(1,await fixture.EventCountAsync(request.StreamId));
    }
    sealed class Clock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(at); }
}
