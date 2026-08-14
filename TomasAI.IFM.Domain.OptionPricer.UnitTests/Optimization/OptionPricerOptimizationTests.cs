using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Actor;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.EventProjector;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Optimization;

public class OptionPricerOptimizationTests
{
    [Fact]
    public void SpreadValuesAreEquivalentAndMaterializedOnce()
    {
        var result = CreateSpreadResult(
            [double.NaN, 4, 1, 3],
            [1, double.PositiveInfinity, 2, 0.0000001]);
        var collection = new ProbabilityValueCollection([result]);

        var first = collection.Values;
        var second = collection.Values;

        first.Should().BeSameAs(second);
        first.Should().Equal(0, 1, 2, 2);
        collection.SpreadValues.Should().Equal(first);
    }

    [Fact]
    public void FusedLossProbabilityMatchesLegacyPipeline()
    {
        var put = new List<double> { 12, 14, 16, 19, 23 };
        var call = new List<double> { 10, 11, 15, 18, 20 };
        var calculator = new LossProbability(put, call, -100_000);

        var putPnl = calculator.GetExpectedPnlValues(OptionType.Put, 2, 50, 9).ToList();
        var callPnl = calculator.GetExpectedPnlValues(OptionType.Call, 2, 50, 8).ToList();
        var legacy = calculator.ToViewModel(putPnl, callPnl);
        var optimized = calculator.Calculate(2, 50, 9, 8);

        optimized.Value.Should().BeApproximately(legacy.Value, 1e-12);
    }

    [Fact]
    public async Task OptionTradeRequestUsesTheOptionTradeActorRoute()
    {
        var context = Substitute.For<IEventActorContext>();
        GetOptionTradeQuery? captured = null;
        context.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(Arg.Do<GetOptionTradeQuery>(query => captured = query))
            .Returns(new ValueTask<ServiceResult<OptionTradeReadModel>>(
                new ServiceFailed<OptionTradeReadModel>(GetOptionTradeQuery.ErrorId, "not found")));

        _ = await context.GetOptionTradeAsync(10, 20);

        captured.Should().NotBeNull();
        captured!.Subject.ActorType.Should().Be(ActorType.Query);
        captured.Subject.Name.Should().Be(GetOptionTradeQuery.Actor);
        captured.Subject.Verb.Should().Be(GetOptionTradeQuery.Verb);
        captured.ErrorCode.Should().Be(GetOptionTradeQuery.ErrorId);
    }

    [Fact]
    public async Task SubmittedJobIsInsertedBeforeItIsPublished()
    {
        var order = new List<string>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IOptionPricerDbContext>();
        dbFactory.OptionPricerDb.Returns(db);
        db.InsertSpreadDistributionJobAsync(Arg.Any<SpreadDistributionJobReadModel>())
            .Returns(_ =>
            {
                order.Add("insert");
                return Task.CompletedTask;
            });

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobEntityId>(Arg.Any<SpreadDistributionJobSubmittedEvent>())
            .Returns(_ =>
            {
                order.Add("submitted");
                return ValueTask.CompletedTask;
            });
        context.SendAsync<SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobEntityId>(Arg.Any<SpreadDistributionJobSubmittedCompleteEvent>())
            .Returns(_ =>
            {
                order.Add("completed");
                return ValueTask.CompletedTask;
            });

        var projector = new SpreadDistributionJobEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<SpreadDistributionJobEventProjector>>());
        var job = new SpreadDistributionJobReadModel
        {
            OrderId = 10,
            TradeId = 20,
            ValueDate = new DateOnly(2026, 8, 5)
        };
        var submitted = new SpreadDistributionJobSubmittedEvent
        {
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobSubmittedEvent.Actor, SpreadDistributionJobSubmittedEvent.Verb, job.EntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EventId = 1_001,
            EntityId = job.EntityId,
            SpreadDistributionJob = job
        };

        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(SpreadDistributionJobSubmittedEvent));
        descriptor.PublishProcessingAfterApply.Should().BeTrue();
        await descriptor.ApplyAsync(
            submitted,
            new ProjectionExecutionContext(
                projector.ProjectorName,
                submitted.EventId,
                1,
                new EventProjectorEffectIdentity(
                    projector.ProjectorName,
                    submitted.EventId,
                    EventProjectorEffectKind.TargetProjection),
                Guid.NewGuid(),
                descriptor.IdempotencyStrategy,
                CancellationToken.None));
        await context.SendAsync<SpreadDistributionJobSubmittedEvent, SpreadDistributionJobEntityId>(submitted);
        await context.SendAsync<SpreadDistributionJobSubmittedCompleteEvent, SpreadDistributionJobEntityId>(
            (SpreadDistributionJobSubmittedCompleteEvent)descriptor.CompletedEventFactory(submitted)!);

        order.Should().Equal("insert", "submitted", "completed");
    }

    static OptionSpreadResult CreateSpreadResult(double[] shortValues, double[] longValues)
    {
        var result = new OptionSpreadResult(0, 30, 5000, 0.04, 0.04, 4900, 0.2, 4850, 0.18);
        result.ShortValues.Add(shortValues);
        result.LongValues.Add(longValues);
        return result;
    }

}
