using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.EventProjector;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class EventProjectorTransientFlowTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Descriptor_defaults_to_durable_replay()
    {
        var descriptor = new EventProjectionDescriptor(
            typeof(FundCreatedEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            static (_, _) => ValueTask.FromResult(
                new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied)),
            static _ => new FundCreatedCompleteEvent(),
            static (_, _) => new FundCreatedFailEvent());

        descriptor.UseDurableReplay.Should().BeTrue();
    }

    [Fact]
    public async Task Non_durable_success_runs_apply_and_completion_without_durable_infrastructure()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var context = Substitute.For<ICommandActorContext>();
        var completed = new TaskCompletionSource<FundCreatedCompleteEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dbFactory.FundDb.Returns(fundDb);
        fundDb.InsertFundAsync(Arg.Any<FundReadModel>()).Returns(Task.CompletedTask);
        eventSource.GetEventStreamIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(77L);
        context.SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                completed.TrySetResult(call.ArgAt<FundCreatedCompleteEvent>(0));
                return ValueTask.CompletedTask;
            });
        var projector = CreateTransientProjector(dbFactory, queue, eventSource);
        var domainEvent = CreateEvent(1_001);

        await projector.StartAsync(context);
        try
        {
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent]));
            var completedEvent = await completed.Task.WaitAsync(TestTimeout);

            completedEvent.EventId.Should().Be(domainEvent.EventId);
            await fundDb.Received(1).InsertFundAsync(domainEvent.NewFund);
            await context.Received(1).SendAsync<FundCreatedEvent, FundId>(
                domainEvent,
                Arg.Any<CancellationToken>());
            await context.Received(1).SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>());
            await context.DidNotReceive().SendAsync<FundCreatedFailEvent, FundId>(
                Arg.Any<FundCreatedFailEvent>(),
                Arg.Any<CancellationToken>());
            queue.ReceivedCalls().Should().BeEmpty();
            ProjectorStateCalls(eventSource).Should().BeEmpty();
        }
        finally
        {
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task Non_durable_apply_exception_publishes_failure_once_without_replay()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var context = Substitute.For<ICommandActorContext>();
        var failed = new TaskCompletionSource<FundCreatedFailEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dbFactory.FundDb.Returns(fundDb);
        fundDb.InsertFundAsync(Arg.Any<FundReadModel>())
            .Returns(_ => Task.FromException(new InvalidOperationException("projection failed")));
        eventSource.GetEventStreamIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(78L);
        context.SendAsync<FundCreatedFailEvent, FundId>(
                Arg.Any<FundCreatedFailEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                failed.TrySetResult(call.ArgAt<FundCreatedFailEvent>(0));
                return ValueTask.CompletedTask;
            });
        var projector = CreateTransientProjector(dbFactory, queue, eventSource);
        var domainEvent = CreateEvent(1_002);

        await projector.StartAsync(context);
        try
        {
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent]));
            var failedEvent = await failed.Task.WaitAsync(TestTimeout);

            failedEvent.EventId.Should().Be(domainEvent.EventId);
            failedEvent.ErrorMessage.Should().Contain("projection failed");
            await fundDb.Received(1).InsertFundAsync(domainEvent.NewFund);
            await context.Received(1).SendAsync<FundCreatedFailEvent, FundId>(
                Arg.Any<FundCreatedFailEvent>(),
                Arg.Any<CancellationToken>());
            await context.DidNotReceive().SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>());
            queue.ReceivedCalls().Should().BeEmpty();
            ProjectorStateCalls(eventSource).Should().BeEmpty();
        }
        finally
        {
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task Non_durable_processing_publication_failure_does_not_suppress_apply_or_completion()
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var context = Substitute.For<ICommandActorContext>();
        var completed = new TaskCompletionSource<FundCreatedCompleteEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dbFactory.FundDb.Returns(fundDb);
        fundDb.InsertFundAsync(Arg.Any<FundReadModel>()).Returns(Task.CompletedTask);
        eventSource.GetEventStreamIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(79L);
        context.SendAsync<FundCreatedEvent, FundId>(
                Arg.Any<FundCreatedEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("processing publish failed")));
        context.SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                completed.TrySetResult(call.ArgAt<FundCreatedCompleteEvent>(0));
                return ValueTask.CompletedTask;
            });
        var projector = CreateTransientProjector(dbFactory, queue, eventSource);
        var domainEvent = CreateEvent(1_006);

        await projector.StartAsync(context);
        try
        {
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent]));
            await completed.Task.WaitAsync(TestTimeout);

            await fundDb.Received(1).InsertFundAsync(domainEvent.NewFund);
            await context.Received(1).SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>());
            queue.ReceivedCalls().Should().BeEmpty();
            ProjectorStateCalls(eventSource).Should().BeEmpty();
        }
        finally
        {
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task Mixed_descriptors_route_each_event_to_exactly_one_lane()
    {
        var durableQueue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var context = Substitute.For<ICommandActorContext>();
        var transientCompleted = new TaskCompletionSource<FundCreatedCompleteEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dbFactory.FundDb.Returns(fundDb);
        fundDb.InsertFundAsync(Arg.Any<FundReadModel>()).Returns(Task.CompletedTask);
        eventSource.GetEventStreamIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(80L);
        context.SendAsync<FundCreatedCompleteEvent, FundId>(
                Arg.Any<FundCreatedCompleteEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                transientCompleted.TrySetResult(call.ArgAt<FundCreatedCompleteEvent>(0));
                return ValueTask.CompletedTask;
            });
        var projector = new MixedFundEventProjector(
            dbFactory,
            durableQueue,
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions { NonDurableQueueCapacity = 2 });
        var transientEvent = CreateEvent(1_010);
        var durableEvent = new OrderAddedToFundEvent
        {
            Subject = new ActorSubject(ActorType.Event, OrderAddedToFundEvent.Actor, OrderAddedToFundEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = 1_011,
            CommandId = Guid.NewGuid(),
            AggregateId = "Event.FundCommandActor.1011"
        };

        await projector.StartAsync(context);
        try
        {
            await projector.DomainEventsProjectionAsync(
                new DomainEventCollection([transientEvent, durableEvent]));
            await transientCompleted.Task.WaitAsync(TestTimeout);

            await durableQueue.Received(1).EnqueueAsync(
                projector.ProjectorName,
                Arg.Is<IEvent>(value => ReferenceEquals(value, durableEvent)),
                CancellationToken.None);
            await durableQueue.DidNotReceive().EnqueueAsync(
                projector.ProjectorName,
                Arg.Is<IEvent>(value => ReferenceEquals(value, transientEvent)),
                Arg.Any<CancellationToken>());
            await eventSource.Received(1).InsertEventProjectorStateAsync(
                Arg.Is<EventProjectorStateReadModel>(state => state.EventId == durableEvent.EventId));
            await eventSource.DidNotReceive().InsertEventProjectorStateAsync(
                Arg.Is<EventProjectorStateReadModel>(state => state.EventId == transientEvent.EventId));
            await fundDb.Received(1).InsertFundAsync(transientEvent.NewFund);
        }
        finally
        {
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task Retry_exact_rejects_a_non_durable_descriptor()
    {
        var durableQueue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(Substitute.For<IFundDbContext>());
        var projector = CreateTransientProjector(dbFactory, durableQueue, eventSource);
        var source = CreateEvent(1_012);
        eventSource.GetEventLogByEventIdAsync(source.EventId, Arg.Any<CancellationToken>())
            .Returns(new EventLogReadModel(
                81,
                nameof(FundCreatedEvent),
                typeof(FundCreatedEvent).AssemblyQualifiedName!,
                source.EventId,
                source.ToEventData(),
                source.CommandId,
                $"{DateTime.UtcNow:o}"));

        (await projector.RetryExactAsync(source.EventId)).Should().BeFalse();

        await eventSource.DidNotReceive().TryRetryEventProjectorExecutionAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        await durableQueue.DidNotReceive().EnqueueAsync(
            Arg.Any<string>(),
            Arg.Any<IEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transient_queue_preserves_order_and_graceful_stop_drains_accepted_work()
    {
        var logger = Substitute.For<ILogger>();
        var observed = new List<long>();
        var queue = new EventProjectorTransientQueue("test-projector", 2, logger);
        await queue.StartAsync((domainEvent, _) =>
        {
            observed.Add(domainEvent.EventId);
            return ValueTask.CompletedTask;
        });

        await queue.EnqueueAsync(CreateEvent(1_003));
        await queue.EnqueueAsync(CreateEvent(1_004));
        await queue.EnqueueAsync(CreateEvent(1_005));
        await queue.StopAsync();

        observed.Should().Equal(1_003, 1_004, 1_005);
    }

    [Fact]
    public async Task Transient_queue_waits_for_capacity_instead_of_dropping_work()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<long>();
        var queue = new EventProjectorTransientQueue(
            "bounded-projector",
            1,
            Substitute.For<ILogger>());
        await queue.StartAsync(async (domainEvent, _) =>
        {
            if (domainEvent.EventId == 1_007)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }
            observed.Add(domainEvent.EventId);
        });

        await queue.EnqueueAsync(CreateEvent(1_007));
        await firstStarted.Task.WaitAsync(TestTimeout);
        await queue.EnqueueAsync(CreateEvent(1_008));
        var waitingWrite = queue.EnqueueAsync(CreateEvent(1_009)).AsTask();
        await Task.Delay(50);
        waitingWrite.IsCompleted.Should().BeFalse();

        releaseFirst.TrySetResult();
        await waitingWrite.WaitAsync(TestTimeout);
        await queue.StopAsync();

        observed.Should().Equal(1_007, 1_008, 1_009);
    }

    static IEnumerable<string> ProjectorStateCalls(IEventSourceActorDbContext eventSource)
        => eventSource.ReceivedCalls()
            .Select(call => call.GetMethodInfo().Name)
            .Where(name => name.Contains("EventProjector", StringComparison.Ordinal));

    static TransientFundEventProjector CreateTransientProjector(
        IDbContextFactory dbFactory,
        IDurableReplayQueue queue,
        IEventSourceActorDbContext eventSource)
        => new(
            dbFactory,
            queue,
            eventSource,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions
            {
                BoundedRecoveryEnabled = true,
                FencedExecutionEnabled = true,
                TransactionalOutboxEnabled = true,
                BacklogMetricsPollingEnabled = true,
                MetricsPollingInterval = TimeSpan.FromSeconds(1),
                NonDurableQueueCapacity = 2
            });

    static FundCreatedEvent CreateEvent(long eventId)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FundCreatedEvent.Actor, FundCreatedEvent.Verb, "1"),
            EntityId = new FundId(1),
            EventId = eventId,
            CommandId = Guid.NewGuid(),
            AggregateId = $"Event.FundCommandActor.{eventId}",
            NewFund = SampleData.Fund
        };

    static IBlackboardService CreateBlackboard()
    {
        var redis = Substitute.For<IRedisCache>();
        return new BlackboardService(redis, new SystemTextJsonSerializer());
    }

    sealed class TransientFundEventProjector : FundEventProjector
    {
        readonly IReadOnlyCollection<EventProjectionDescriptor> _transientDescriptors;

        public TransientFundEventProjector(
            IDbContextFactory dbFactory,
            IDurableReplayQueue durableReplayQueue,
            IEventSourceActorDbContext dbEventSource,
            IBlackboardService blackboardService,
            EventProjectorReliabilityOptions reliabilityOptions)
            : base(
                CreateContext(dbFactory, durableReplayQueue, dbEventSource, blackboardService),
                reliabilityOptions)
        {
            _transientDescriptors = [.. base.ProjectionDescriptors.Select(
                static descriptor => descriptor with { UseDurableReplay = false })];
        }

        public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors
            => _transientDescriptors;
    }

    sealed class MixedFundEventProjector : FundEventProjector
    {
        readonly IReadOnlyCollection<EventProjectionDescriptor> _mixedDescriptors;

        public MixedFundEventProjector(
            IDbContextFactory dbFactory,
            IDurableReplayQueue durableReplayQueue,
            IEventSourceActorDbContext dbEventSource,
            IBlackboardService blackboardService,
            EventProjectorReliabilityOptions reliabilityOptions)
            : base(
                CreateContext(dbFactory, durableReplayQueue, dbEventSource, blackboardService),
                reliabilityOptions)
        {
            _mixedDescriptors = [.. base.ProjectionDescriptors.Select(
                static descriptor => descriptor.SourceEventType == typeof(FundCreatedEvent)
                    ? descriptor with { UseDurableReplay = false }
                    : descriptor)];
        }

        public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors
            => _mixedDescriptors;
    }

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
}
