using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
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
            Substitute.For<ILogger<TickAggregationRealtimeProjector>>());

        projector.Should().BeAssignableTo<IRealtimeProjector<TickAggregationRealtimeActor>>();
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().BeEquivalentTo([
                typeof(FuturesTickTradeDataInsertedEvent),
                typeof(FuturesTickQuoteDataInsertedEvent)]);
    }

    [Fact]
    public void Tick_contracts_target_only_the_realtime_primary_actor()
    {
        FuturesTickTradeDataChangedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickQuoteDataChangedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickTradeDataInsertedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
        FuturesTickQuoteDataInsertedEvent.Actor.Should().Be(TickAggregationRealtimeActor.ActorName);
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
