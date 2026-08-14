using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.EventProjector;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.EventProjector;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public sealed class SpreadDistributionEventProjectorTests
{
    [Fact]
    public void Spread_projectors_have_unique_durable_descriptors_and_submit_publishes_after_insert()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var blackboard = Substitute.For<IBlackboardService>();
        IEventProjector[] projectors =
        [
            new SpreadDistributionEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<SpreadDistributionEventProjector>>()),
            new SpreadDistributionJobEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<SpreadDistributionJobEventProjector>>())
        ];

        projectors.SelectMany(projector => projector.ProjectionDescriptors).Should().HaveCount(5);
        projectors.SelectMany(projector => projector.ProjectionDescriptors)
            .Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
        foreach (var projector in projectors)
            projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType).Should().OnlyHaveUniqueItems();

        projectors.SelectMany(projector => projector.ProjectionDescriptors)
            .Single(descriptor => descriptor.SourceEventType == typeof(SpreadDistributionJobSubmittedEvent))
            .PublishProcessingAfterApply.Should().BeTrue();
    }

    [Fact]
    public async Task Spread_distribution_replay_uses_stable_event_derived_ids_for_both_rows()
    {
        const long persistedEventId = 918273;
        var optionPricerDb = Substitute.For<IOptionPricerDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.OptionPricerDb.Returns(optionPricerDb);
        var projector = new SpreadDistributionEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<SpreadDistributionEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(SpreadDistributionInsertedEvent));
        var source = new SpreadDistributionInsertedEvent
        {
            PutSpreadDistribution = CreateDistribution(TradeType.ShortPut),
            CallSpreadDistribution = CreateDistribution(TradeType.ShortCall)
        };
        var context = new ProjectionExecutionContext(
            projector.ProjectorName,
            persistedEventId,
            eventStreamId: 42,
            new EventProjectorEffectIdentity(
                projector.ProjectorName,
                persistedEventId,
                EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None,
            streamVersion: 7);

        await descriptor.ApplyAsync(source, context);
        await descriptor.ApplyAsync(source, context);

        await optionPricerDb.Received(2).InsertSpreadDistributionsAsync(
            Arg.Is<SpreadDistributionReadModel>(row => row.Id == -(persistedEventId * 2)),
            Arg.Is<SpreadDistributionReadModel>(row => row.Id == -(persistedEventId * 2 + 1)));
    }

    static SpreadDistributionReadModel CreateDistribution(TradeType tradeType) => new(
        id: 0,
        tradeId: 123,
        valueDate: new DateOnly(2026, 8, 13),
        tradeType: tradeType,
        tradeStatus: TradeStatus.Open,
        daysToExpiry: 10,
        forwardPrice: 100,
        lossProbability: 0.2,
        lossThreshold: 1,
        lossThresholdCount: 2,
        shortVolatility: 0.1,
        longVolatility: 0.2,
        forwardLossRatio: 0.3,
        createdOn: DateTime.UtcNow);
}
