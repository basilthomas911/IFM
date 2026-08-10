using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class EventProjectorOutboxDispatcherTests
{
    [Fact]
    public async Task Failed_publication_releases_for_retry_and_reuses_the_same_event_identity()
    {
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var rowAttempt = 0;
        var durableRow = CreateRow(Guid.NewGuid(), 1);
        eventSource.ClaimEventProjectorOutboxAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<EventProjectorOutboxReadModel>>(
                [durableRow with { DispatchToken = call.ArgAt<Guid>(1), AttemptCount = ++rowAttempt }]));
        eventSource.ReleaseEventProjectorOutboxAsync(
                Arg.Any<EventProjectorOutboxReadModel>(), Arg.Any<EventProjectorOutboxStatus>(),
                Arg.Any<DateTime?>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        eventSource.MarkEventProjectorOutboxPublishedAsync(
                Arg.Any<EventProjectorOutboxReadModel>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var publishedIds = new List<Guid>();
        var failFirst = true;
        async ValueTask PublishAsync(IEvent domainEvent, CancellationToken cancellationToken)
        {
            await Task.Yield();
            publishedIds.Add(domainEvent.Id);
            if (failFirst)
            {
                failFirst = false;
                throw new InvalidOperationException("injected transport failure");
            }
        }
        await using var dispatcher = CreateDispatcher(eventSource, PublishAsync);

        (await dispatcher.DispatchBatchAsync()).Should().Be(1);
        (await dispatcher.DispatchBatchAsync()).Should().Be(1);

        publishedIds.Should().HaveCount(2);
        // The two deserialized instances carry the same deterministic publication ID.
        publishedIds[0].Should().Be(publishedIds[1]);
        await eventSource.Received(1).ReleaseEventProjectorOutboxAsync(
            Arg.Any<EventProjectorOutboxReadModel>(),
            EventProjectorOutboxStatus.Retrying,
            Arg.Is<DateTime?>(value => value.HasValue),
            Arg.Is<string>(value => value.Contains("injected transport failure", StringComparison.Ordinal)),
            Arg.Any<DateTime>(),
            CancellationToken.None);
        await eventSource.Received(1).MarkEventProjectorOutboxPublishedAsync(
            Arg.Any<EventProjectorOutboxReadModel>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ambiguous_delivery_marker_republishes_the_same_payload_identity()
    {
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var rowAttempt = 0;
        var durableRow = CreateRow(Guid.NewGuid(), 1);
        eventSource.ClaimEventProjectorOutboxAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<EventProjectorOutboxReadModel>>(
                [durableRow with { DispatchToken = call.ArgAt<Guid>(1), AttemptCount = ++rowAttempt }]));
        eventSource.MarkEventProjectorOutboxPublishedAsync(
                Arg.Any<EventProjectorOutboxReadModel>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false, true);
        var publishedIds = new List<Guid>();
        ValueTask PublishAsync(IEvent domainEvent, CancellationToken cancellationToken)
        {
            publishedIds.Add(domainEvent.Id);
            return ValueTask.CompletedTask;
        }
        await using var dispatcher = CreateDispatcher(eventSource, PublishAsync);

        await dispatcher.DispatchBatchAsync();
        await dispatcher.DispatchBatchAsync();

        publishedIds.Should().HaveCount(2);
        publishedIds[0].Should().Be(publishedIds[1]);
        await eventSource.Received(2).MarkEventProjectorOutboxPublishedAsync(
            Arg.Any<EventProjectorOutboxReadModel>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    static EventProjectorOutboxDispatcher CreateDispatcher(
        IEventSourceActorDbContext eventSource,
        Func<IEvent, CancellationToken, ValueTask> publishAsync)
        => new(
            eventSource,
            new EventProjectorReliabilityOptions
            {
                FencedExecutionEnabled = true,
                TransactionalOutboxEnabled = true,
                InitialReplayDelay = TimeSpan.FromMilliseconds(1),
                OutboxBatchSize = 8
            },
            "FundEventProjector",
            publishAsync,
            Substitute.For<ILogger<EventProjectorOutboxDispatcherTests>>());

    static EventProjectorOutboxReadModel CreateRow(Guid dispatchToken, int attemptCount)
    {
        var identity = new EventProjectorEffectIdentity(
            "FundEventProjector",
            1_001,
            EventProjectorEffectKind.CompletedPublication);
        var completed = new FundCreatedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedCompleteEvent.Actor, FundCreatedCompleteEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = identity.EventId,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1"
        };
        var message = EventProjectorOutboxSerializer.Serialize(completed, identity);
        var nowUtc = DateTime.UtcNow;
        return new EventProjectorOutboxReadModel(
            identity.ProjectorName,
            identity.EventId,
            identity.EffectKind,
            identity.MessageId,
            message.EventTypeName,
            message.EventPayload,
            EventProjectorOutboxStatus.Publishing,
            attemptCount,
            null,
            nowUtc,
            null,
            string.Empty,
            dispatchToken,
            nowUtc.AddMinutes(2));
    }
}
