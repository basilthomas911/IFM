using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.TickAggregation;

public sealed class TickAggregationRealtimeArchitectureTests
{
    [Fact]
    public void Realtime_projector_has_one_storage_descriptor_per_normalized_tick_kind()
    {
        var projector = new TickAggregationRealtimeProjector(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<TickAggregationRealtimeProjector>>(),
            new LivePipelineEvidence(TimeProvider.System));

        projector.Should().BeAssignableTo<IRealtimeProjector<TickAggregationRealtimeActor>>();
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().BeEquivalentTo([
                typeof(FuturesTickTradeDataInsertedEvent),
                typeof(FuturesTickQuoteDataInsertedEvent)]);
    }

    [Fact]
    public async Task Realtime_projector_records_ES_trade_and_VX_quote_storage_after_database_success()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var evidence = new LivePipelineEvidence(TimeProvider.System);
        var projector = new TickAggregationRealtimeProjector(
            dbFactory,
            Substitute.For<ILogger<TickAggregationRealtimeProjector>>(),
            evidence);
        var date = new DateOnly(2026, 9, 10);
        var es = new FuturesTickTradeDataInsertedEvent
        {
            EntityId = new TickDataEntityId("ES20260918", date, AssetTypeId.Futures)
        };
        var vx = new FuturesTickQuoteDataInsertedEvent
        {
            EntityId = new TickDataEntityId("VX20260916", date, AssetTypeId.Futures)
        };

        await projector.ProjectionDescriptors.Single(x =>
            x.SourceEventType == typeof(FuturesTickTradeDataInsertedEvent))
            .ApplyAsync(es, CancellationToken.None);
        await projector.ProjectionDescriptors.Single(x =>
            x.SourceEventType == typeof(FuturesTickQuoteDataInsertedEvent))
            .ApplyAsync(vx, CancellationToken.None);

        await marketDataDb.Received(1).InsertTickTradeDataAsync(es);
        await marketDataDb.Received(1).InsertTickQuoteDataAsync(vx);
        var esEvidence = evidence.Get("Tick storage", "ES20260918");
        esEvidence.Should().NotBeNull();
        esEvidence!.Status.Should().Be("Healthy");
        esEvidence.LastProgressUtc.Should().NotBeNull();
        esEvidence.Reason.Should().Be("Durable trade tick write completed.");
        var vxEvidence = evidence.Get("Tick storage", "VX20260916");
        vxEvidence.Should().NotBeNull();
        vxEvidence!.Status.Should().Be("Healthy");
        vxEvidence.LastProgressUtc.Should().NotBeNull();
        vxEvidence.Reason.Should().Be("Durable quote tick write completed.");
    }

    [Fact]
    public async Task Realtime_projector_records_storage_failure_and_does_not_report_success()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var evidence = new LivePipelineEvidence(TimeProvider.System);
        var projector = new TickAggregationRealtimeProjector(
            dbFactory,
            Substitute.For<ILogger<TickAggregationRealtimeProjector>>(),
            evidence);
        var source = new FuturesTickTradeDataInsertedEvent
        {
            EntityId = new TickDataEntityId(
                "ES20260918",
                new DateOnly(2026, 9, 10),
                AssetTypeId.Futures)
        };
        marketDataDb.InsertTickTradeDataAsync(source)
            .Returns(Task.FromException(new InvalidOperationException("storage unavailable")));
        var descriptor = projector.ProjectionDescriptors.Single(x =>
            x.SourceEventType == typeof(FuturesTickTradeDataInsertedEvent));

        var apply = async () => await descriptor.ApplyAsync(source, CancellationToken.None);

        await apply.Should().ThrowAsync<InvalidOperationException>();
        var failure = evidence.Get("Tick storage", "ES20260918");
        failure.Should().NotBeNull();
        failure!.Status.Should().Be("Unhealthy");
        failure.LastProgressUtc.Should().BeNull();
        failure.Reason.Should().Contain(nameof(InvalidOperationException));
    }

    [Fact]
    public void Tick_contracts_target_only_the_realtime_primary_actor()
    {
        FuturesTickTradeDataChangedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickQuoteDataChangedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickTradeDataInsertedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickQuoteDataInsertedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesSessionStatisticsUpdatedRealtimeEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
    }

    [Fact]
    public void Realtime_actor_has_no_durable_projection_dependencies()
    {
        var parameterTypes = typeof(TickAggregationRealtimeActor)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        parameterTypes.Should().NotContain(typeof(IEventSourceActorDbContext));
        parameterTypes.Should().NotContain(typeof(IDurableReplayQueue));
    }

    [Fact]
    public void Rolling_eod_realtime_projector_covers_futures_and_vix_without_durable_dependencies()
    {
        var projector = new FuturesEodDataRealtimeProjector(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesEodDataRealtimeProjector>>());

        projector.Should().BeAssignableTo<IRealtimeProjector<FuturesEodDataRealtimeActor>>();
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().BeEquivalentTo([
                typeof(FuturesEodDataInsertedEvent),
                typeof(FuturesEodSessionStatisticsUpdatedEvent),
                typeof(VixFuturesEodDataInsertedEvent)]);

        var parameterTypes = typeof(FuturesEodDataRealtimeProjector)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType);
        parameterTypes.Should().NotContain(typeof(IEventSourceActorDbContext));
        parameterTypes.Should().NotContain(typeof(IDurableReplayQueue));
    }
}
