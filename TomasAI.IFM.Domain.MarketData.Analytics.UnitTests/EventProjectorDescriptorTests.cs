using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class EventProjectorDescriptorTests
{
    [Fact]
    public void All_analytics_events_have_one_descriptor_and_only_lifecycle_events_are_non_durable()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var blackboard = Substitute.For<IBlackboardService>();
        IEventProjector[] projectors =
        [
            new FuturesAdxSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesAdxSignalEventProjector>>()),
            new FuturesAtrSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesAtrSignalEventProjector>>()),
            new FuturesMacdSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesMacdSignalEventProjector>>()),
            new FuturesRsiSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesRsiSignalEventProjector>>()),
            new FuturesItiSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesItiSignalEventProjector>>()),
            new FuturesTdiSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesTdiSignalEventProjector>>()),
            new FuturesTradeSignalEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesTradeSignalEventProjector>>())
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
    public async Task Futures_trade_signal_replay_uses_the_persisted_event_id_as_its_stable_sequence_key()
    {
        const long persistedEventId = 918273;
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var projector = new FuturesTradeSignalEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FuturesTradeSignalEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesTradeSignalUpdatedEvent));
        var source = new FuturesTradeSignalUpdatedEvent
        {
            FuturesTradeSignal = new FuturesTradeSignalV2ReadModel
            {
                ContractId = "ESU6",
                ValueDate = new DateOnly(2026, 8, 13),
                TimePeriod = TimeFrameType.Daily,
                SequenceId = 0
            }
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

        await marketDataDb.Received(2).InsertFuturesTradeSignalAsync(
            Arg.Is<FuturesTradeSignalV2ReadModel>(signal => signal.SequenceId == persistedEventId));
    }

    [Fact]
    public async Task Futures_iti_signal_replay_uses_the_persisted_event_id_as_its_stable_sequence_key()
    {
        const long persistedEventId = 918274;
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var projector = new FuturesItiSignalEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FuturesItiSignalEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesItiSignalGeneratedEvent));
        var source = new FuturesItiSignalGeneratedEvent
        {
            FuturesItiSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU6",
                ValueDate = new DateOnly(2026, 8, 13),
                TimePeriod = TimeFrameType.Daily,
                SequenceId = 0,
                IntrinsicTimeMode = IntrinsicTimeModeType.Trending
            }
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

        await marketDataDb.Received(2).InsertFuturesItiSignalAsync(
            Arg.Is<FuturesItiSignalV2ReadModel>(signal => signal.SequenceId == persistedEventId));
        await marketDataDb.DidNotReceiveWithAnyArgs().GetLastFuturesItiSignalAsync(default!, default);
        source.FuturesItiSignal!.SequenceId.Should().Be(0,
            "a projector must not mutate the event-log source payload after persistence");
    }

    [Fact]
    public async Task Futures_tdi_projector_writes_only_the_version_2_traders_dynamic_index_contract()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var projector = new FuturesTdiSignalEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FuturesTdiSignalEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesTdiSignalGeneratedEvent));
        var version2 = SampleData.CreateTdiSignalGeneratedEvent();
        var legacy = version2 with
        {
            FuturesTdiSignal = new FuturesTdiSignalReadModel(
                version2.EntityId.ContractId,
                version2.EntityId.ValueDate,
                version2.EntityId.TimePeriod,
                new TimeOnly(10, 0),
                2,
                0,
                FuturesTrendDirectionType.UpTrending,
                FuturesTrendDirectionStrengthType.Medium)
        };
        var context = new ProjectionExecutionContext(
            projector.ProjectorName,
            101,
            42,
            new EventProjectorEffectIdentity(
                projector.ProjectorName,
                101,
                EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None,
            streamVersion: 8);

        await descriptor.ApplyAsync(version2, context);
        await descriptor.ApplyAsync(legacy, context);

        await marketDataDb.Received(1).InsertFuturesTdiSignalAsync(
            Arg.Is<FuturesTdiSignalReadModel>(signal =>
                signal.SchemaVersion == FuturesTdiConfiguration.CurrentSchemaVersion
                && signal.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId));
    }
}
