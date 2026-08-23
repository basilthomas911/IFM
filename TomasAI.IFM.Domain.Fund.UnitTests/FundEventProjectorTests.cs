using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.EventProjector;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;

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
        projector.Readiness.IsReady.Should().BeTrue();
        await durableReplayQueue.Received(1).PrepareAsync(
            projector.ProjectorName,
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
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
    public async Task StartAsync_propagates_token_through_recovery_and_queue_startup()
    {
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        using var cancellation = new CancellationTokenSource();
        dbEventSource.GetUncompletedEventProjectorEventsAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                cancellation.Token)
            .Returns(Array.Empty<EventLogReadModel>());
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());

        await projector.StartAsync(Substitute.For<ICommandActorContext>(), cancellation.Token);

        await durableReplayQueue.Received(1).PrepareAsync(
            projector.ProjectorName,
            TimeSpan.FromSeconds(30),
            cancellation.Token);
        await durableReplayQueue.Received(1).DequeueAsync(
            projector.ProjectorName,
            Arg.Any<Func<IEvent, Task>>(),
            cancellation.Token);
        await dbEventSource.Received(1).GetUncompletedEventProjectorEventsAsync(
            projector.ProjectorName,
            Arg.Any<IReadOnlyCollection<string>>(),
            cancellation.Token);
        await durableReplayQueue.Received(1).StartAsync(
            projector.ProjectorName,
            TimeSpan.FromSeconds(30),
            cancellation.Token);
    }

    [Fact]
    public async Task StartAsync_cancellation_during_recovery_prevents_worker_start()
    {
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        using var cancellation = new CancellationTokenSource();
        dbEventSource.GetUncompletedEventProjectorEventsAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<ICollection<EventLogReadModel>>(cancellation.Token);
            });
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());

        Func<Task> start = () => projector
            .StartAsync(Substitute.For<ICommandActorContext>(), cancellation.Token)
            .AsTask();

        await start.Should().ThrowAsync<OperationCanceledException>();
        projector.Readiness.IsReady.Should().BeFalse();
        projector.Readiness.FailureReason.Should().NotBeEmpty();
        Action context = () => _ = projector.Context;
        context.Should().Throw<InvalidOperationException>();
        await durableReplayQueue.Received(1).StopAsync(
            projector.ProjectorName,
            CancellationToken.None);
        await durableReplayQueue.DidNotReceive().StartAsync(
            projector.ProjectorName,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
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
        durableReplayQueue.When(queue => queue.EnqueueAsync(
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
                Arg.Any<IReadOnlyCollection<string>>(),
                CancellationToken.None)
            .Returns([eventLog]);
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());
        dbEventSource.GetEventProjectorStateAsync(
                eventId,
                projector.ProjectorName,
                CancellationToken.None)
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
            Arg.Is<IReadOnlyCollection<string>>(names => names.Contains(nameof(FundCreatedEvent))),
            CancellationToken.None);
        await dbEventSource.Received().InsertEventProjectorStateAsync(
            Arg.Is<EventProjectorStateReadModel>(state =>
                state.EventId == eventId
                && state.ProjectorName == projector.ProjectorName),
            CancellationToken.None);
        await durableReplayQueue.Received(1).EnqueueAsync(
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
                Arg.Any<IReadOnlyCollection<string>>(),
                CancellationToken.None)
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
        dbEventSource.GetEventProjectorStateAsync(
                eventId,
                Arg.Any<string>(),
                CancellationToken.None)
            .Returns((EventProjectorStateReadModel?)null);
        var projector = CreateProjector(durableReplayQueue, dbEventSource, CreateBlackboard());

        await projector.StartAsync(Substitute.For<ICommandActorContext>());

        await durableReplayQueue.DidNotReceive().EnqueueAsync(
            projector.ProjectorName,
            Arg.Any<IEvent>(),
            Arg.Any<CancellationToken>());
        await dbEventSource.DidNotReceive().InsertEventProjectorStateAsync(
            Arg.Is<EventProjectorStateReadModel>(state => state.EventId == eventId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bounded_recovery_uses_joined_keyset_page_and_publishes_readiness_after_start()
    {
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.GetEventProjectorRecoveryPageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<long>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EventProjectorRecoveryItemReadModel>());
        var projector = CreateProjector(
            durableReplayQueue,
            dbEventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions { BoundedRecoveryEnabled = true });

        await projector.StartAsync(Substitute.For<ICommandActorContext>());

        projector.Readiness.IsReady.Should().BeTrue();
        projector.Readiness.RecoveryEventsDiscovered.Should().Be(0);
        await dbEventSource.Received(1).GetEventProjectorRecoveryPageAsync(
            projector.ProjectorName,
            Arg.Any<IReadOnlyCollection<string>>(),
            0,
            Arg.Any<DateTime>(),
            256,
            CancellationToken.None);
        await dbEventSource.DidNotReceive().GetUncompletedEventProjectorEventsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
        await durableReplayQueue.Received(1).StartAsync(
            projector.ProjectorName,
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
    }

    [Fact]
    public async Task Bounded_recovery_failure_rolls_back_workers_and_keeps_readiness_false()
    {
        const long eventId = 904L;
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var item = RecoveryItem(eventId, 44L);
        dbEventSource.GetEventProjectorRecoveryPageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<long>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([item]);
        dbEventSource.TryClaimEventProjectorExecutionAsync(
                eventId,
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call => item.State with
            {
                Revision = 1,
                ExecutionToken = call.ArgAt<Guid>(2),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(2)
            });
        durableReplayQueue.EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));
        var projector = CreateProjector(
            durableReplayQueue,
            dbEventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions { BoundedRecoveryEnabled = true });

        var start = () => projector.StartAsync(Substitute.For<ICommandActorContext>()).AsTask();

        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("publish failed");
        projector.Readiness.IsReady.Should().BeFalse();
        projector.Readiness.FailureReason.Should().Be("publish failed");
        await durableReplayQueue.Received(1).StopAsync(projector.ProjectorName, CancellationToken.None);
        await durableReplayQueue.DidNotReceive().StartAsync(
            projector.ProjectorName,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Projection_descriptors_are_complete_unique_and_explicitly_idempotent()
    {
        var projector = CreateProjector(Substitute.For<IDurableReplayQueue>());

        projector.ProjectionDescriptors.Should().HaveCount(8);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(projector.ProjectedEventTypes);
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor =>
            descriptor.IdempotencyStrategy == EventProjectionIdempotencyStrategy.NaturalKeyMutation);
    }

    [Fact]
    public async Task Fenced_execution_terminalizes_unregistered_event_for_manual_resolution()
    {
        const long eventId = 949;
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var projector = CreateProjector(
            Substitute.For<IDurableReplayQueue>(),
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions { FencedExecutionEnabled = true });
        var state = RecoveryItem(eventId, 76).State;
        eventSource.GetEventProjectorExecutionStateAsync(
                eventId,
                projector.ProjectorName,
                Arg.Any<CancellationToken>())
            .Returns(state);
        eventSource.TryClaimEventProjectorExecutionAsync(
                eventId,
                projector.ProjectorName,
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call => state with
            {
                ExecutionToken = call.ArgAt<Guid>(2),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
                Revision = 1
            });
        eventSource.TryTerminalizeEventProjectorExecutionAsync(
                Arg.Any<EventProjectorStateTransition>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(call => state with
            {
                Stage = EventProjectorStageType.Completed,
                Outcome = EventProjectorOutcomeType.Failed,
                BlockedReason = call.ArgAt<EventProjectorStateTransition>(0).BlockedReason
            });
        var unknown = new UnknownEvent { EventId = eventId };

        await projector.ProcessDomainEventAsync(unknown);

        await eventSource.Received(1).TryTerminalizeEventProjectorExecutionAsync(
            Arg.Is<EventProjectorStateTransition>(transition =>
                transition.Outcome == EventProjectorOutcomeType.Failed
                && transition.BlockedReason == "unregistered-source-event"),
            Arg.Any<DateTime>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task Fenced_execution_reapplies_target_after_checkpoint_loss_and_finishes_once()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(fundDb);
        var context = Substitute.For<ICommandActorContext>();
        var projector = CreateProjector(
            queue,
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions
            {
                FencedExecutionEnabled = true,
                InitialReplayDelay = TimeSpan.FromMilliseconds(1)
            },
            dbFactory);
        var nowUtc = DateTime.UtcNow;
        var fund = SampleData.Fund;
        var domainEvent = new FundCreatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = 950,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1",
            NewFund = fund
        };
        var state = new EventProjectorExecutionStateReadModel(
            domainEvent.EventId,
            projector.ActorName,
            projector.ProjectorName,
            false,
            0,
            EventProjectorOutcomeType.Processing,
            EventProjectorStageType.ApplyProjection,
            string.Empty,
            nowUtc,
            nowUtc,
            77,
            nameof(FundCreatedEvent),
            0,
            null,
            null,
            0,
            null,
            null,
            string.Empty,
            EventProjectorStageType.PublishProcessingEvent,
            nowUtc);
        var loseFirstCheckpoint = true;
        eventSource.GetEventProjectorExecutionStateAsync(
                domainEvent.EventId,
                projector.ProjectorName,
                Arg.Any<CancellationToken>())
            .Returns(_ => state);
        eventSource.TryClaimEventProjectorExecutionAsync(
                domainEvent.EventId,
                projector.ProjectorName,
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (state.ExecutionToken.HasValue)
                    return null;
                state = state with
                {
                    ExecutionToken = call.ArgAt<Guid>(2),
                    LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
                    Revision = state.Revision + 1,
                    Outcome = EventProjectorOutcomeType.Processing
                };
                return state;
            });
        eventSource.TryTransitionEventProjectorExecutionAsync(
                Arg.Any<EventProjectorStateTransition>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transition = call.ArgAt<EventProjectorStateTransition>(0);
                if (loseFirstCheckpoint)
                {
                    loseFirstCheckpoint = false;
                    return null;
                }
                state = state with
                {
                    Stage = transition.NextStage,
                    Outcome = transition.Outcome,
                    LastCompletedStage = transition.LastCompletedStage,
                    ErrorMessage = transition.ErrorMessage,
                    Revision = state.Revision + 1
                };
                return state;
            });
        eventSource.TryReleaseEventProjectorExecutionAsync(
                Arg.Any<EventProjectorStateTransition>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transition = call.ArgAt<EventProjectorStateTransition>(0);
                state = state with
                {
                    ExecutionToken = null,
                    LeaseExpiresAtUtc = null,
                    Outcome = EventProjectorOutcomeType.Retrying,
                    RetryCount = transition.RetryCount,
                    NextAttemptAtUtc = transition.NextAttemptAtUtc,
                    LastErrorAtUtc = transition.LastErrorAtUtc,
                    ErrorMessage = transition.ErrorMessage,
                    Revision = state.Revision + 1
                };
                return state;
            });
        eventSource.TryTerminalizeEventProjectorExecutionAsync(
                Arg.Any<EventProjectorStateTransition>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transition = call.ArgAt<EventProjectorStateTransition>(0);
                state = state with
                {
                    Stage = EventProjectorStageType.Completed,
                    Outcome = transition.Outcome,
                    LastCompletedStage = transition.LastCompletedStage,
                    ExecutionToken = null,
                    LeaseExpiresAtUtc = null,
                    Revision = state.Revision + 1
                };
                return state;
            });

        await projector.StartAsync(context);
        var firstAttempt = () => projector.ProcessDomainEventAsync(domainEvent).AsTask();

        await firstAttempt.Should().ThrowAsync<InvalidOperationException>().WithMessage("*fence was lost*");
        await projector.ProcessDomainEventAsync(domainEvent);

        await fundDb.Received(2).InsertFundAsync(fund);
        await eventSource.Received(1).TryReleaseEventProjectorExecutionAsync(
            Arg.Any<EventProjectorStateTransition>(),
            Arg.Any<DateTime>(),
            CancellationToken.None);
        state.Stage.Should().Be(EventProjectorStageType.Completed);
        state.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
        state.ExecutionToken.Should().BeNull();
        await context.Received(1).SendAsync<FundCreatedCompleteEvent, FundId>(
            Arg.Any<FundCreatedCompleteEvent>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task Transactional_outbox_terminalizes_completion_with_one_deterministic_typed_publication()
    {
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(fundDb);
        var projector = CreateProjector(
            Substitute.For<IDurableReplayQueue>(),
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions
            {
                FencedExecutionEnabled = true,
                TransactionalOutboxEnabled = true,
                InitialReplayDelay = TimeSpan.FromMilliseconds(1)
            },
            dbFactory);
        var nowUtc = DateTime.UtcNow;
        var domainEvent = new FundCreatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = 951,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1",
            NewFund = SampleData.Fund
        };
        var state = new EventProjectorExecutionStateReadModel(
            domainEvent.EventId, projector.ActorName, projector.ProjectorName, false, 0,
            EventProjectorOutcomeType.Processing, EventProjectorStageType.ApplyProjection, string.Empty,
            nowUtc, nowUtc, 77, nameof(FundCreatedEvent), 0, null, null, 0, null, null,
            string.Empty, EventProjectorStageType.PublishProcessingEvent, nowUtc);
        eventSource.GetEventProjectorExecutionStateAsync(
                domainEvent.EventId, projector.ProjectorName, Arg.Any<CancellationToken>())
            .Returns(_ => state);
        eventSource.TryClaimEventProjectorExecutionAsync(
                domainEvent.EventId, projector.ProjectorName, Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => state = state with
            {
                ExecutionToken = call.ArgAt<Guid>(2),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
                Revision = state.Revision + 1
            });
        eventSource.TryTransitionEventProjectorExecutionAsync(
                Arg.Any<EventProjectorStateTransition>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transition = call.ArgAt<EventProjectorStateTransition>(0);
                return state = state with
                {
                    Stage = transition.NextStage,
                    LastCompletedStage = transition.LastCompletedStage,
                    Revision = state.Revision + 1
                };
            });
        EventProjectorOutboxMessage? staged = null;
        eventSource.TryTerminalizeEventProjectorExecutionWithOutboxAsync(
                Arg.Any<EventProjectorStateTransition>(), Arg.Any<EventProjectorOutboxMessage>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transition = call.ArgAt<EventProjectorStateTransition>(0);
                staged = call.ArgAt<EventProjectorOutboxMessage>(1);
                return state = state with
                {
                    Stage = EventProjectorStageType.Completed,
                    Outcome = transition.Outcome,
                    LastCompletedStage = transition.LastCompletedStage,
                    ExecutionToken = null,
                    LeaseExpiresAtUtc = null,
                    Revision = state.Revision + 1
                };
            });

        await projector.ProcessDomainEventAsync(domainEvent);

        state.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
        staged.Should().NotBeNull();
        staged!.Identity.EffectKind.Should().Be(EventProjectorEffectKind.CompletedPublication);
        staged.Identity.EventId.Should().Be(domainEvent.EventId);
        staged.EventTypeName.Should().Contain(nameof(FundCreatedCompleteEvent));
        staged.EventPayload.Should().NotBeEmpty();
        staged.MessageId.Should().Be(new EventProjectorEffectIdentity(
            projector.ProjectorName,
            domainEvent.EventId,
            EventProjectorEffectKind.CompletedPublication).MessageId);
    }

    [Fact]
    public async Task Maximum_attempt_handler_atomically_stages_the_registered_typed_failure_event()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        Func<IEvent, Task>? maximumAttempts = null;
        queue.When(instance => instance.SetMaxAttemptsReachedAction(
                Arg.Any<string>(), Arg.Any<Func<IEvent, Task>>(), Arg.Any<bool>()))
            .Do(call => maximumAttempts = call.ArgAt<Func<IEvent, Task>>(1));
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.GetUncompletedEventProjectorEventsAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ICollection<EventLogReadModel>>([]));
        eventSource.ClaimEventProjectorOutboxAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EventProjectorOutboxReadModel>>([]));
        var projector = CreateProjector(
            queue,
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions
            {
                FencedExecutionEnabled = true,
                TransactionalOutboxEnabled = true,
                InitialReplayDelay = TimeSpan.FromMilliseconds(1),
                OutboxPollingInterval = TimeSpan.FromMilliseconds(10)
            });
        var nowUtc = DateTime.UtcNow;
        var domainEvent = new FundCreatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = 952,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1",
            NewFund = SampleData.Fund
        };
        var state = new EventProjectorExecutionStateReadModel(
            domainEvent.EventId, projector.ActorName, projector.ProjectorName, true, 3,
            EventProjectorOutcomeType.Retrying, EventProjectorStageType.ApplyProjection, "target failed",
            nowUtc, nowUtc, 77, nameof(FundCreatedEvent), 3, null, null, 3, nowUtc, nowUtc,
            string.Empty, EventProjectorStageType.PublishProcessingEvent, nowUtc);
        eventSource.GetEventProjectorExecutionStateAsync(
                domainEvent.EventId, projector.ProjectorName, Arg.Any<CancellationToken>())
            .Returns(_ => state);
        eventSource.TryClaimEventProjectorExecutionAsync(
                domainEvent.EventId, projector.ProjectorName, Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => state = state with
            {
                ExecutionToken = call.ArgAt<Guid>(2),
                LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
                Revision = state.Revision + 1
            });
        EventProjectorStateTransition? terminal = null;
        EventProjectorOutboxMessage? staged = null;
        eventSource.TryTerminalizeEventProjectorExecutionWithOutboxAsync(
                Arg.Any<EventProjectorStateTransition>(), Arg.Any<EventProjectorOutboxMessage>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                terminal = call.ArgAt<EventProjectorStateTransition>(0);
                staged = call.ArgAt<EventProjectorOutboxMessage>(1);
                return state = state with
                {
                    Stage = EventProjectorStageType.Completed,
                    Outcome = terminal.Outcome,
                    BlockedReason = terminal.BlockedReason,
                    ExecutionToken = null,
                    LeaseExpiresAtUtc = null,
                    Revision = state.Revision + 1
                };
            });

        await projector.StartAsync(Substitute.For<ICommandActorContext>());
        try
        {
            maximumAttempts.Should().NotBeNull();
            state = state with { RetryCount = 2 };
            Func<Task> prematureTerminalization = () => maximumAttempts!(domainEvent);
            (await prematureTerminalization.Should().ThrowAsync<EventProjectorDeliveryDeferredException>())
                .Which.Reason.Should().Be("failure-budget-not-exhausted");
            state = state with { RetryCount = 3 };
            await maximumAttempts!(domainEvent);
        }
        finally
        {
            await projector.StopAsync();
        }

        terminal.Should().NotBeNull();
        terminal!.Outcome.Should().Be(EventProjectorOutcomeType.Failed);
        terminal.BlockedReason.Should().Be("maximum-attempts-reached");
        staged.Should().NotBeNull();
        staged!.Identity.EffectKind.Should().Be(EventProjectorEffectKind.FailedPublication);
        staged.EventTypeName.Should().Contain(nameof(FundCreatedFailEvent));
        staged.EventPayload.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Retry_exact_reopens_the_durable_stage_and_requeues_the_original_source_event()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var projector = CreateProjector(queue, eventSource);
        var source = new FundCreatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = 953,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1",
            NewFund = SampleData.Fund
        };
        eventSource.GetEventLogByEventIdAsync(source.EventId, Arg.Any<CancellationToken>())
            .Returns(new EventLogReadModel(
                77,
                nameof(FundCreatedEvent),
                typeof(FundCreatedEvent).AssemblyQualifiedName!,
                source.EventId,
                source.ToEventData(),
                source.CommandId,
                $"{DateTime.UtcNow:o}"));
        eventSource.TryRetryEventProjectorExecutionAsync(
                source.EventId, projector.ProjectorName, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new EventProjectorExecutionStateReadModel(
                source.EventId, projector.ActorName, projector.ProjectorName, true, 0,
                EventProjectorOutcomeType.Retrying, EventProjectorStageType.ApplyProjection, string.Empty,
                DateTime.UtcNow, DateTime.UtcNow, 77, nameof(FundCreatedEvent), 5, null, null,
                0, null, null, string.Empty, EventProjectorStageType.PublishProcessingEvent, DateTime.UtcNow));

        (await projector.RetryExactAsync(source.EventId)).Should().BeTrue();

        await queue.Received(1).EnqueueAsync(
            projector.ProjectorName,
            Arg.Is<IEvent>(value => value is FundCreatedEvent && value.EventId == source.EventId),
            CancellationToken.None);
    }

    static FundEventProjector CreateProjector(
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext? dbEventSource = null,
        IBlackboardService? blackboardService = null,
        EventProjectorReliabilityOptions? reliabilityOptions = null,
        IDbContextFactory? dbFactory = null) =>
        new(
            CreateContext(
                dbFactory ?? Substitute.For<IDbContextFactory>(),
                durableReplayQueue,
                dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
                blackboardService ?? Substitute.For<IBlackboardService>()),
            reliabilityOptions);

    static ICommandActorContext<FundCommandActor> CreateContext(
        IDbContextFactory dbFactory,
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext dbEventSource,
        IBlackboardService blackboardService)
    {
        var context = Substitute.For<IFundCommandContext>();
        var container = Substitute.For<IContainerInstance>();
        context.Container.Returns(container);
        context.DbFactory.Returns(dbFactory);
        context.BlackboardService.Returns(blackboardService);
        context.Logger.Returns(Substitute.For<ILogger<FundCommandActor>>());
        context.DurableReplayQueue.Returns(durableReplayQueue);
        context.DbEventSource.Returns(dbEventSource);
        container.Resolve<IDurableReplayQueue>().Returns(durableReplayQueue);
        container.Resolve<IEventSourceActorDbContext>().Returns(dbEventSource);
        return context;
    }

    static EventProjectorRecoveryItemReadModel RecoveryItem(long eventId, long streamId)
    {
        var nowUtc = DateTime.UtcNow;
        var sourceEvent = new FundCreatedEvent { NewFund = SampleData.Fund };
        return new EventProjectorRecoveryItemReadModel(
            new EventLogReadModel(
                streamId,
                nameof(FundCreatedEvent),
                typeof(FundCreatedEvent).AssemblyQualifiedName!,
                eventId,
                Newtonsoft.Json.JsonConvert.SerializeObject(sourceEvent),
                Guid.NewGuid(),
                $"{nowUtc:o}"),
            new EventProjectorExecutionStateReadModel(
                eventId,
                "FundCommandActor",
                "FundEventProjector",
                true,
                0,
                TomasAI.IFM.Shared.EventProjector.EventProjectorOutcomeType.Processing,
                TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent,
                string.Empty,
                nowUtc,
                nowUtc,
                streamId,
                nameof(FundCreatedEvent),
                0,
                null,
                null,
                0,
                null,
                null,
                string.Empty,
                TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.None,
                nowUtc));
    }

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
