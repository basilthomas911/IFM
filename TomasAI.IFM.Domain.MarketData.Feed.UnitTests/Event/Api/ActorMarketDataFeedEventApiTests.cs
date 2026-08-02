using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Api;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Event.Api;

public class ActorMarketDataFeedEventApiTests
{
    [Fact]
    public async Task FactoryBindsTheContextAndSendsTypedCompleteEvent()
    {
        var context = Substitute.For<IEventActorContext>();
        var source = CreateResetEvent();
        var api = new ActorMarketDataFeedEventApiFactory().Create(context);

        await api.MarketDataFeedResetCompleteAsync(source);

        api.Should().BeAssignableTo<IActorMarketDataFeedEventApi>();
        await context.Received(1).SendAsync<MarketDataFeedResetCompleteEvent, MarketDataFeedId>(
            Arg.Is<MarketDataFeedResetCompleteEvent>(sent =>
                sent.EntityId == source.EntityId &&
                sent.CommandId == source.CommandId &&
                sent.Subject.Is(
                    ActorType.Event,
                    MarketDataFeedResetCompleteEvent.Actor,
                    MarketDataFeedResetCompleteEvent.Verb)));
    }

    [Fact]
    public async Task FailureMethodConvertsExceptionToTypedFailEvent()
    {
        var context = Substitute.For<IEventActorContext>();
        var source = CreateResetEvent();
        var api = new ActorMarketDataFeedEventApi(context);

        await api.MarketDataFeedResetFailAsync(source, new InvalidOperationException("reset failed"));

        await context.Received(1).SendAsync<MarketDataFeedResetFailEvent, MarketDataFeedId>(
            Arg.Is<MarketDataFeedResetFailEvent>(sent =>
                sent.EntityId == source.EntityId &&
                sent.CommandId == source.CommandId &&
                sent.ErrorMessage == "reset failed"));
    }

    [Fact]
    public async Task ResetStreamingBuildsTheExpectedRoutedEvent()
    {
        var context = Substitute.For<IEventActorContext>();
        var source = CreateResetEvent()
            .ToCompleteEvent<MarketDataFeedResetCompleteEvent, MarketDataFeedId>()
            as MarketDataFeedResetCompleteEvent;
        var api = new ActorMarketDataFeedEventApi(context);

        await api.SendResetStreamingEventAsync(source!);

        await context.Received(1).SendAsync<MarketDataFeedResetStreamingEvent, MarketDataFeedId>(
            Arg.Is<MarketDataFeedResetStreamingEvent>(sent =>
                sent.EntityId == source!.EntityId &&
                sent.CommandId == source.CommandId &&
                sent.Subject.Is(
                    ActorType.Event,
                    MarketDataFeedResetStreamingEvent.Actor,
                    MarketDataFeedResetStreamingEvent.Verb)));
    }

    static MarketDataFeedResetEvent CreateResetEvent()
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedResetEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                MarketDataFeedResetEvent.Actor,
                MarketDataFeedResetEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            AggregateId = "market-data-feed",
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            FuturesContracts = [],
            ValueDate = SampleData.ValueDate,
            ResetOn = DateTime.UtcNow,
            ResetBy = "unit-test"
        };
    }
}
