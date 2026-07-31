using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Command.EventProjector;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class FundEventProjectorTests
{
    [Fact]
    public void Context_throws_before_projector_is_started()
    {
        var projector = CreateProjector(Substitute.For<IDurableReplayQueue>());

        Action action = () => _ = projector.Context;

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*has not been started*");
    }

    [Fact]
    public async Task StartAsync_captures_actor_context_and_initializes_durable_queue()
    {
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var projector = CreateProjector(durableReplayQueue);
        var context = Substitute.For<ICommandActorContext>();

        await projector.StartAsync(context);

        projector.Context.Should().BeSameAs(context);
        await durableReplayQueue.Received(1).DequeueAsync(
            projector.ProjectorName,
            Arg.Any<Func<IEvent, Task>>(),
            CancellationToken.None);
        await durableReplayQueue.Received(1).StartAsync(
            projector.ProjectorName,
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
    }

    [Fact]
    public async Task DomainEventsProjectionAsync_persists_recovery_state_before_enqueue()
    {
        var calls = new List<string>();
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertEventProjectorStateAsync(Arg.Any<EventProjectorStateReadModel>())
            .Returns(_ =>
            {
                calls.Add("persist");
                return Task.CompletedTask;
            });
        durableReplayQueue.When(queue => queue.Enqueue(
                Arg.Any<string>(),
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("enqueue"));
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());
        var domainEvent = new FundCreatedEvent { NewFund = SampleData.Fund };
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), 901L);

        await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent]));

        calls.Should().Equal("persist", "enqueue");
        await dbEventSource.Received(1).InsertEventProjectorStateAsync(
            Arg.Is<EventProjectorStateReadModel>(state =>
                state.EventId == 901L
                && state.ProjectorName == projector.ProjectorName));
    }

    [Fact]
    public async Task StartAsync_recovers_supported_uncompleted_events_from_event_log()
    {
        const long eventId = 902L;
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var sourceEvent = new FundCreatedEvent { NewFund = SampleData.Fund };
        var eventLog = new EventLogReadModel(
            EventStreamId: 33L,
            EventName: nameof(FundCreatedEvent),
            EventTypeName: typeof(FundCreatedEvent).AssemblyQualifiedName!,
            EventVersion: eventId,
            EventData: Newtonsoft.Json.JsonConvert.SerializeObject(sourceEvent),
            CommandId: Guid.NewGuid(),
            EventTimestamp: $"{DateTime.UtcNow:o}");
        dbEventSource.GetUncompletedEventProjectorEventsAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>())
            .Returns([eventLog]);
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());
        dbEventSource.GetEventProjectorStateAsync(eventId, projector.ProjectorName)
            .Returns(new EventProjectorStateReadModel(
                eventId,
                projector.ActorName,
                projector.ProjectorName,
                isReplay: false,
                attemptNumber: 0,
                outcome: TomasAI.IFM.Shared.EventProjector.EventProjectorOutcomeType.Processing,
                stage: TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent));

        await projector.StartAsync(Substitute.For<ICommandActorContext>());

        await dbEventSource.Received(1).GetUncompletedEventProjectorEventsAsync(
            projector.ProjectorName,
            Arg.Is<IReadOnlyCollection<string>>(names => names.Contains(nameof(FundCreatedEvent))));
        await dbEventSource.Received().InsertEventProjectorStateAsync(
            Arg.Is<EventProjectorStateReadModel>(state =>
                state.EventId == eventId
                && state.ProjectorName == projector.ProjectorName));
        durableReplayQueue.Received(1).Enqueue(
            projector.ProjectorName,
            Arg.Is<IEvent>(domainEvent => domainEvent.EventId == eventId),
            CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_does_not_infer_pending_state_when_projector_state_is_missing()
    {
        const long eventId = 903L;
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var sourceEvent = new FundCreatedEvent { NewFund = SampleData.Fund };
        dbEventSource.GetUncompletedEventProjectorEventsAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>())
            .Returns([
                new EventLogReadModel(
                    EventStreamId: 34L,
                    EventName: nameof(FundCreatedEvent),
                    EventTypeName: typeof(FundCreatedEvent).AssemblyQualifiedName!,
                    EventVersion: eventId,
                    EventData: Newtonsoft.Json.JsonConvert.SerializeObject(sourceEvent),
                    CommandId: Guid.NewGuid(),
                    EventTimestamp: $"{DateTime.UtcNow:o}")
            ]);
        dbEventSource.GetEventProjectorStateAsync(eventId, Arg.Any<string>())
            .Returns((EventProjectorStateReadModel?)null);
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());

        await projector.StartAsync(Substitute.For<ICommandActorContext>());

        durableReplayQueue.DidNotReceive().Enqueue(
            projector.ProjectorName,
            Arg.Any<IEvent>(),
            Arg.Any<CancellationToken>());
        await dbEventSource.DidNotReceive().InsertEventProjectorStateAsync(
            Arg.Is<EventProjectorStateReadModel>(state => state.EventId == eventId));
    }

    static FundEventProjector CreateProjector(
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext? dbEventSource = null,
        IBlackboardService? blackboardService = null) =>
        new(
            Substitute.For<IDbContextFactory>(),
            durableReplayQueue,
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            blackboardService ?? Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FundEventProjector>>());

    static IBlackboardService CreateBlackboard()
    {
        var redisCache = Substitute.For<IRedisCache>();
        var values = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(call =>
            values.TryGetValue(call.Arg<string>(), out var value) ? value : string.Empty);
        redisCache.When(cache => cache.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        redisCache.When(cache => cache.Remove(Arg.Any<string>()))
            .Do(call => values.Remove(call.Arg<string>()));
        return new BlackboardService(redisCache, new SystemTextJsonSerializer());
    }
}
