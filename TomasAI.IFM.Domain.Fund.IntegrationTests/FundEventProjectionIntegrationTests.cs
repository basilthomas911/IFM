using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Command;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.EventProjector;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class FundEventProjectionCollection
{
    public const string CollectionName = "Fund event projection integration";
}

[Collection(FundEventProjectionCollection.CollectionName)]
[Trait("Category", "Integration")]
public sealed class FundEventProjectionIntegrationTests(FundDatabaseFixture database)
    : IClassFixture<FundDatabaseFixture>, IAsyncLifetime
{
    const string ProcessStream = "IFM_FundEventProjector_PROCESS";
    const string ReplayStream = "IFM_FundEventProjector_REPLAY";
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    readonly ConcurrentBag<int> _fundIds = [];
    readonly ConcurrentBag<string> _eventStreams = [];
    readonly string _natsUrl = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
    NatsClient _adminClient = null!;
    INatsJSContext _jetStream = null!;

    public async Task InitializeAsync()
    {
        _adminClient = new NatsClient(_natsUrl);
        await _adminClient.ConnectAsync();
        _jetStream = _adminClient.CreateJetStreamContext();
        await DeleteQueueStreamsAsync();
    }

    [Fact]
    public async Task SaveStateAsync_persists_event_then_projects_and_completes_projector_state()
    {
        var context = new RecordingCommandActorContext();
        var queue = new NatsJSDurableReplayQueue(CreateNatsOptions());
        await using var queueScope = queue;
        var projector = new GatedFundEventProjector(
            database.DbFactory,
            queue,
            database.ActorEventSourceDb,
            database.BlackboardService,
            Substitute.For<ILogger<FundEventProjector>>(),
            new EventProjectorReliabilityOptions
            {
                BoundedRecoveryEnabled = true,
                FencedExecutionEnabled = true
            });
        var repository = CreateRepository(projector);
        var (fund, command, state) = CreateFundState();
        Track(fund.FundId, command.StreamId);
        await database.FundDb.DeleteFundAsync(fund.FundId);
        await projector.StartAsync(context);

        try
        {
            await repository.SaveStateAsync(context, state, command);
            var domainEvent = state.Events.Should().ContainSingle().Subject;
            await projector.ProcessingStarted.Task.WaitAsync(TestTimeout);

            var streamId = await database.ActorEventSourceDb.GetEventStreamIdAsync(command.StreamId);
            streamId.Should().BeGreaterThan(0);
            var eventStream = await database.ActorEventSourceDb.LoadActorEventStreamAsync<FundCommandState>(streamId);
            eventStream.Should().ContainSingle(e =>
                e.EventVersion == domainEvent.EventId
                && e.EventTypeName == typeof(FundCreatedEvent).AssemblyQualifiedName);
            var processingState = await database.ActorEventSourceDb.GetEventProjectorStateAsync(
                domainEvent.EventId,
                projector.ProjectorName);
            processingState.Should().NotBeNull();
            processingState!.Outcome.Should().Be(EventProjectorOutcomeType.Processing);
            processingState.Stage.Should().Be(EventProjectorStageType.PublishProcessingEvent);

            projector.ReleaseProcessing();

            var completedState = await WaitForCompletedStateAsync(domainEvent.EventId, projector.ProjectorName);
            completedState.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
            completedState.Stage.Should().Be(EventProjectorStageType.Completed);
            var projectedFund = await WaitForFundAsync(fund.FundId);
            projectedFund.Name.Should().Be(fund.Name);
            context.Events.Should().Contain(e => e is FundCreatedEvent && e.EventId == domainEvent.EventId);
            context.Events.Should().Contain(e => e is FundCreatedCompleteEvent && e.EventId == domainEvent.EventId);
        }
        finally
        {
            projector.ReleaseProcessing();
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_recovers_explicit_processing_event_from_event_log_and_completes_projection()
    {
        var context = new RecordingCommandActorContext();
        var queue = new NatsJSDurableReplayQueue(CreateNatsOptions());
        await using var queueScope = queue;
        var projector = CreateProjector(queue);
        var (fund, command, state) = CreateFundState();
        Track(fund.FundId, command.StreamId);
        await database.FundDb.DeleteFundAsync(fund.FundId);
        var savedEvents = await database.ActorEventSourceDb.SaveEventsAsync(
            command.StreamId,
            command.CommandId,
            state.Events);
        var domainEvent = savedEvents.Should().ContainSingle().Subject;
        var now = DateTime.UtcNow;
        await database.ActorEventSourceDb.InsertEventProjectorStateAsync(
            new EventProjectorStateReadModel(
                domainEvent.EventId,
                projector.ActorName,
                projector.ProjectorName,
                isReplay: false,
                attemptNumber: 0,
                outcome: EventProjectorOutcomeType.Processing,
                stage: EventProjectorStageType.PublishProcessingEvent,
                createdTimestamp: now,
                updatedTimestamp: now));
        (await database.FundDb.GetFundAsync(fund.FundId)).Should().BeNull();

        await projector.StartAsync(context);

        try
        {
            var completedState = await WaitForCompletedStateAsync(domainEvent.EventId, projector.ProjectorName);
            completedState.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
            completedState.Stage.Should().Be(EventProjectorStageType.Completed);
            var projectedFund = await WaitForFundAsync(fund.FundId);
            projectedFund.Name.Should().Be(fund.Name);
            context.Events.Should().Contain(e => e is FundCreatedCompleteEvent && e.EventId == domainEvent.EventId);
        }
        finally
        {
            await projector.StopAsync();
        }
    }

    [Fact]
    public async Task Fund_projection_descriptors_are_repeat_apply_safe_for_all_eight_operations()
    {
        var projector = CreateProjector(Substitute.For<IDurableReplayQueue>());
        projector.ProjectionDescriptors.Should().HaveCount(8)
            .And.OnlyContain(descriptor =>
                descriptor.IdempotencyStrategy == EventProjectionIdempotencyStrategy.NaturalKeyMutation);

        var fundId = Random.Shared.Next(2_000_001, 2_100_000);
        var orderId = Random.Shared.Next(2_100_001, 2_200_000);
        var tradeId = Random.Shared.Next(2_200_001, 2_300_000);
        var now = DateTime.UtcNow;
        var fund = new FundReadModel(
            fundId,
            $"Repeat-safe fund {fundId}",
            "SWO-06 Tranche C",
            250_000m,
            false,
            now,
            "integration-test");
        var order = new FundOrderReadModel(
            fundId,
            orderId,
            now,
            TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open,
            "ES",
            DateOnly.FromDateTime(now),
            DateOnly.FromDateTime(now.AddMonths(3)),
            "repeat-safe",
            now,
            "integration-test",
            null,
            string.Empty);
        var trade = new FundOrderTradeReadModel(
            fundId,
            orderId,
            tradeId,
            TradeType.LongCall,
            DateOnly.FromDateTime(now),
            DateOnly.FromDateTime(now.AddMonths(3)),
            TradeState.TradeToOpen,
            TradeAction.Buy,
            "repeat-safe",
            true,
            "ES",
            now,
            "integration-test",
            null,
            string.Empty);
        var fundOrderId = new FundOrderId(fundId, orderId);
        var fundOrderTradeId = new FundOrderTradeId(fundId, orderId, tradeId);
        IEvent[] events =
        [
            new FundCreatedEvent { NewFund = fund },
            new OrderAddedToFundEvent { FundOrder = order },
            new TradeAddedToFundOrderEvent { FundOrderTrade = trade },
            new FundOrderTradeStateChangedEvent
            {
                FundOrderTradeId = fundOrderTradeId,
                TradeState = TradeState.OrderCompleted,
                UpdatedOn = now.AddMinutes(1),
                UpdatedBy = "integration-test"
            },
            new FundOrderClosedEvent { FundOrderId = fundOrderId },
            new FundMaxProfitGeneratedEvent
            {
                FundOrder = order,
                FundMaxProfit = new FundMaxProfitReadModel(fundOrderId, 100m, 0.10)
            },
            new TradeRemovedFromFundOrderEvent { FundOrderTradeId = fundOrderTradeId },
            new OrderRemovedFromFundEvent { FundOrderId = fundOrderId }
        ];

        for (var index = 0; index < events.Length; index++)
            EventInitHelper.SetProperty(events[index], nameof(IEvent.EventId), index + 1L);

        try
        {
            foreach (var domainEvent in events.Take(6))
                await ApplyTwiceAsync(projector, domainEvent);

            var persistedFund = await database.FundDb.GetFundAsync(fundId);
            persistedFund.Should().NotBeNull();
            persistedFund!.Name.Should().Be(fund.Name);
            persistedFund.Description.Should().Be(fund.Description);
            persistedFund.Balance.Should().Be(fund.Balance);
            persistedFund.IsProduction.Should().Be(fund.IsProduction);
            (await database.FundDb.GetFundOrderAsync(fundId, orderId))!.OrderStatus.Should()
                .Be(TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Closed);
            (await database.FundDb.GetFundOrderTradeAsync(fundId, orderId, tradeId))!.TradeState
                .Should().Be(TradeState.OrderCompleted);

            await ApplyTwiceAsync(projector, events[6]);
            (await database.FundDb.GetFundOrderTradeAsync(fundId, orderId, tradeId)).Should().BeNull();
            await ApplyTwiceAsync(projector, events[7]);
            (await database.FundDb.GetFundOrderAsync(fundId, orderId)).Should().BeNull();
            (await database.FundDb.GetFundAsync(fundId)).Should().NotBeNull();
        }
        finally
        {
            await database.FundDb.DeleteFundOrderTradeAsync(fundId, orderId, tradeId);
            await database.FundDb.DeleteFundOrderAsync(fundId, orderId);
            await database.FundDb.DeleteFundAsync(fundId);
        }

        static async Task ApplyTwiceAsync(FundEventProjector projector, IEvent domainEvent)
        {
            var descriptor = projector.ProjectionDescriptors.Single(candidate =>
                candidate.SourceEventType == domainEvent.GetType());
            var context = new ProjectionExecutionContext(
                projector.ProjectorName,
                domainEvent.EventId,
                1,
                new EventProjectorEffectIdentity(
                    projector.ProjectorName,
                    domainEvent.EventId,
                    EventProjectorEffectKind.TargetProjection),
                Guid.NewGuid(),
                descriptor.IdempotencyStrategy,
                CancellationToken.None);

            (await descriptor.ApplyAsync(domainEvent, context)).Outcome.Should().Be(EventProjectionApplyOutcome.Applied);
            (await descriptor.ApplyAsync(domainEvent, context)).Outcome.Should().Be(EventProjectionApplyOutcome.Applied);
        }
    }

    FundStateRepository CreateRepository(IEventProjector<FundCommandActor> projector) => new(
        Substitute.For<IEventSourceActorStateFactory>(),
        database.ActorEventSourceDb,
        Substitute.For<IActorService>(),
        projector,
        Substitute.For<ILogger<FundStateRepository>>());

    FundEventProjector CreateProjector(IDurableReplayQueue queue) => new(
        database.DbFactory,
        queue,
        database.ActorEventSourceDb,
        database.BlackboardService,
        Substitute.For<ILogger<FundEventProjector>>());

    NatsJetStreamConsumerOptions CreateNatsOptions() => new()
    {
        Url = _natsUrl
    };

    (FundReadModel Fund, CreateFundCommand Command, FundCommandState State) CreateFundState()
    {
        var fundId = Random.Shared.Next(1_000_000, 2_000_000);
        var fund = new FundReadModel(
            fundId,
            $"Projection Test Fund {fundId}",
            "Fund event-sourcing projection integration test",
            100_000m,
            false,
            DateTime.UtcNow,
            "integration-test");
        var command = new CreateFundCommand(fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                CreateFundCommand.Actor,
                CreateFundCommand.Verb,
                fund.Id.Format()),
            PostEvents = true
        };
        var state = new FundCommandState
        {
            Id = command.Subject.ThreadId
        };
        command.Execute(state).Success.Should().BeTrue();
        return (fund, command, state);
    }

    async Task<EventProjectorStateReadModel> WaitForCompletedStateAsync(long eventId, string projectorName)
    {
        var expires = DateTime.UtcNow + TestTimeout;
        while (DateTime.UtcNow < expires)
        {
            var state = await database.ActorEventSourceDb.GetEventProjectorStateAsync(eventId, projectorName);
            if (state is { Stage: EventProjectorStageType.Completed })
                return state;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Event {eventId} did not reach a completed projector state.");
    }

    async Task<FundReadModel> WaitForFundAsync(int fundId)
    {
        var expires = DateTime.UtcNow + TestTimeout;
        while (DateTime.UtcNow < expires)
        {
            var fund = await database.FundDb.GetFundAsync(fundId);
            if (fund is not null)
                return fund;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Fund {fundId} was not projected.");
    }

    void Track(int fundId, string eventStream)
    {
        _fundIds.Add(fundId);
        _eventStreams.Add(eventStream);
    }

    public async Task DisposeAsync()
    {
        await DeleteQueueStreamsAsync();
        foreach (var fundId in _fundIds)
            await database.FundDb.DeleteFundAsync(fundId);
        foreach (var eventStream in _eventStreams)
        {
            var streamId = await database.ActorEventSourceDb.GetEventStreamIdAsync(eventStream);
            if (streamId > 0)
                await database.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(streamId);
        }
        await _adminClient.DisposeAsync();
    }

    async Task DeleteQueueStreamsAsync()
    {
        await TryDeleteStreamAsync(ProcessStream);
        await TryDeleteStreamAsync(ReplayStream);
    }

    async Task TryDeleteStreamAsync(string streamName)
    {
        try
        {
            await _jetStream.DeleteStreamAsync(streamName);
        }
        catch (NatsJSApiException)
        {
        }
    }

    sealed class GatedFundEventProjector(
        IDbContextFactory dbFactory,
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext dbEventSource,
        IBlackboardService blackboardService,
        ILogger<FundEventProjector> logger,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : FundEventProjector(
            dbFactory,
            durableReplayQueue,
            dbEventSource,
            blackboardService,
            logger,
            reliabilityOptions)
    {
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ProcessingStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask ProcessDomainEventAsync(IEvent domainEvent)
        {
            ProcessingStarted.TrySetResult();
            await _release.Task;
            await base.ProcessDomainEventAsync(domainEvent);
        }

        public void ReleaseProcessing() => _release.TrySetResult();
    }

    sealed class RecordingCommandActorContext : ICommandActorContext
    {
        public ConcurrentBag<IEvent> Events { get; } = [];
        public ActorMailboxId ActorId { get; } = new(ActorType.Command, FundCommandActor.Actor);
        public IContainerInstance Container => null!;

        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            Events.Add(@event);
            return ValueTask.CompletedTask;
        }

        public bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info) => false;
        public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb) => null;
    }
}
