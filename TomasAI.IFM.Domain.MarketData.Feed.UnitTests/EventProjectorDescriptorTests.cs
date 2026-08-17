using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public sealed class EventProjectorDescriptorTests
{
    [Fact]
    public void All_feed_events_have_one_descriptor_and_only_start_stop_events_are_non_durable()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var blackboard = Substitute.For<IBlackboardService>();
        IEventProjector[] projectors =
        [
            new MarketDataFeedEventProjector(Substitute.For<IMarketDataDbContext>(), queue, eventSource, blackboard, Substitute.For<ILogger<MarketDataFeedEventProjector>>()),
            new FuturesBarDataEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesBarDataEventProjector>>()),
            new FuturesTickDataEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesTickDataEventProjector>>()),
            new FuturesOptionTickDataEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesOptionTickDataEventProjector>>()),
            new FuturesClosingPriceEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesClosingPriceEventProjector>>()),
            new FuturesEodDataEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesEodDataEventProjector>>())
        ];

        projectors.SelectMany(projector => projector.ProjectionDescriptors).Should().HaveCount(22);
        foreach (var projector in projectors)
        {
            projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
                .Should().OnlyHaveUniqueItems();
            foreach (var descriptor in projector.ProjectionDescriptors)
            {
                var isLifecycle = descriptor.SourceEventType.Name.Contains("Started", StringComparison.Ordinal)
                    || descriptor.SourceEventType.Name.Contains("Stopped", StringComparison.Ordinal);
                descriptor.UseDurableReplay.Should().Be(!isLifecycle);
            }
        }
    }

    [Fact]
    public async Task Tick_projector_replay_uses_the_persisted_event_id_when_the_payload_has_no_id()
    {
        const long persistedEventId = 918273;
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var projector = CreateTickProjector(marketDataDb);
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesTickDataInsertedEvent));
        var source = new FuturesTickDataInsertedEvent
        {
            TickData = new FuturesTickDataV2ReadModel
            {
                ContractId = "ESU6",
                ValueDate = new DateOnly(2026, 8, 13),
                TickId = 0
            }
        };
        var context = CreateContext(projector, persistedEventId);

        await descriptor.ApplyAsync(source, context);
        await descriptor.ApplyAsync(source, context);

        await marketDataDb.Received(2).InsertFuturesTickDataAsync(
            Arg.Is<FuturesTickDataV2ReadModel>(row => row.TickId == persistedEventId));
    }

    [Fact]
    public async Task Option_tick_projector_replay_uses_the_persisted_event_id_when_the_payload_has_no_id()
    {
        const long persistedEventId = 918274;
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var projector = CreateOptionTickProjector(marketDataDb);
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesOptionTickDataInsertedEvent));
        var source = new FuturesOptionTickDataInsertedEvent
        {
            TickData = new FuturesOptionTickDataV2ReadModel
            {
                ContractId = "ESU6 C5000",
                ValueDate = new DateOnly(2026, 8, 13),
                TickId = 0
            }
        };
        var context = CreateContext(projector, persistedEventId);

        await descriptor.ApplyAsync(source, context);
        await descriptor.ApplyAsync(source, context);

        await marketDataDb.Received(2).InsertFuturesOptionTickDataAsync(
            Arg.Is<FuturesOptionTickDataV2ReadModel>(row => row.TickId == persistedEventId));
    }

    static FuturesTickDataEventProjector CreateTickProjector(IMarketDataDbContext marketDataDb)
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return new FuturesTickDataEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FuturesTickDataEventProjector>>());
    }

    static FuturesOptionTickDataEventProjector CreateOptionTickProjector(IMarketDataDbContext marketDataDb)
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return new FuturesOptionTickDataEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FuturesOptionTickDataEventProjector>>());
    }

    static ProjectionExecutionContext CreateContext(IEventProjector projector, long eventId) => new(
        projector.ProjectorName,
        eventId,
        eventStreamId: 42,
        new EventProjectorEffectIdentity(
            projector.ProjectorName,
            eventId,
            EventProjectorEffectKind.TargetProjection),
        Guid.NewGuid(),
        EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        CancellationToken.None,
        streamVersion: 7);
}
