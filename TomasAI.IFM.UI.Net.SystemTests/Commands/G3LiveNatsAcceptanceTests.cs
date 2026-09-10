using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.SystemTests.Commands;

[Trait("Category", "G3LiveNats")]
public sealed class G3LiveNatsAcceptanceTests
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Strategy_workflow_ui_notifications_reach_independent_subscribers_and_one_can_stop()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("IFM_RUN_UI_G3_EVENTS"),
                "1",
                StringComparison.Ordinal))
            return;

        var natsUrl = Environment.GetEnvironmentVariable("IFM_NATS_URL")
                      ?? "nats://localhost:4222";
        var consumer = new IntrinsicTimeStrategyWorkflowUIEventConsumer(
            new NatsEventListenerOptions { Url = natsUrl },
            NullLogger.Instance);
        var serializer = new NatsMessagePackDataSerializer();
        var first = new ConcurrentQueue<long>();
        var second = new ConcurrentQueue<long>();
        var firstSite = Guid.NewGuid();
        var secondSite = Guid.NewGuid();

        await using var client = new NatsClient(natsUrl);
        await client.ConnectAsync();
        try
        {
            await consumer.StartAsync(firstSite, value => first.Enqueue(value.WorkflowRevision));
            await consumer.StartAsync(secondSite, value => second.Enqueue(value.WorkflowRevision));
            await WaitForAsync(() => consumer.State == EventListenerState.Running);
            await Task.Delay(250);

            await PublishAsync(client, serializer, WorkflowNotification(1, WorkflowStrategyMachineStatus.Started));
            await PublishAsync(client, serializer, WorkflowNotification(2, WorkflowStrategyMachineStatus.Completed));
            await WaitForAsync(() => first.Count == 2 && second.Count == 2);
            first.Should().Equal(1, 2);
            second.Should().Equal(1, 2);

            await consumer.StopAsync(firstSite);
            consumer.State.Should().Be(EventListenerState.Running);
            await PublishAsync(client, serializer, WorkflowNotification(3, WorkflowStrategyMachineStatus.Completed));
            await WaitForAsync(() => second.Count == 3);
            first.Should().Equal(1, 2);
            second.Should().Equal(1, 2, 3);
        }
        finally
        {
            await consumer.StopAsync(firstSite);
            await consumer.StopAsync(secondSite);
        }
    }

    [Fact]
    public async Task Command_response_catalog_preserves_order_correlation_and_single_listener_reopen()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("IFM_RUN_UI_G3_EVENTS"),
                "1",
                StringComparison.Ordinal))
            return;

        var natsUrl = Environment.GetEnvironmentVariable("IFM_NATS_URL")
                      ?? "nats://localhost:4222";
        var consumer = new CommandResponseUIEventConsumer(
            new NatsEventListenerOptions { Url = natsUrl },
            NullLogger.Instance);
        var serializer = new NatsMessagePackDataSerializer();
        var received = new List<IEvent>();
        var receivedGate = new object();
        var prototypes = new IEvent[]
        {
            new FuturesContractAddedCompleteEvent(),
            new FuturesContractAddedFailEvent()
        };

        await using var client = new NatsClient(natsUrl);
        await client.ConnectAsync();
        try
        {
            await consumer.StartAsync(prototypes, Capture);
            await WaitForAsync(() => consumer.State == EventListenerState.Running);
            await Task.Delay(250);

            var commandIds = Enumerable.Range(0, 128).Select(_ => Guid.NewGuid()).ToArray();
            for (var index = 0; index < commandIds.Length; index++)
            {
                var complete = Complete(index, commandIds[index]);
                await PublishAsync(client, serializer, complete);
            }

            await WaitForAsync(() => Count() == commandIds.Length);
            Snapshot().Select(item => item.EventId).Should().Equal(Enumerable.Range(0, 128).Select(i => (long)i));
            Snapshot().Select(item => item.CommandId).Should().Equal(commandIds);

            var failureCommandId = Guid.NewGuid();
            var failure = Failure(failureCommandId);
            await PublishAsync(client, serializer, failure);
            await WaitForAsync(() => Count() == commandIds.Length + 1);
            Snapshot()[^1].Should().BeOfType<FuturesContractAddedFailEvent>()
                .Which.CommandId.Should().Be(failureCommandId);

            await consumer.StopAsync();
            consumer.State.Should().Be(EventListenerState.Stopped);
            await PublishAsync(client, serializer, Complete(999, Guid.NewGuid()));
            await Task.Delay(300);
            Count().Should().Be(129, "a stopped screen listener must reject later deliveries");

            await consumer.StartAsync(prototypes, Capture);
            await WaitForAsync(() => consumer.State == EventListenerState.Running);
            await Task.Delay(250);
            var reopenedCommandId = Guid.NewGuid();
            await PublishAsync(client, serializer, Complete(128, reopenedCommandId));
            await WaitForAsync(() => Count() == 130);
            Snapshot().Count(item => item.CommandId == reopenedCommandId).Should().Be(1,
                "reopening must create exactly one active listener");
        }
        finally
        {
            await consumer.StopAsync();
        }

        void Capture(IEvent @event)
        {
            lock (receivedGate)
                received.Add(@event);
        }

        int Count()
        {
            lock (receivedGate)
                return received.Count;
        }

        IEvent[] Snapshot()
        {
            lock (receivedGate)
                return received.ToArray();
        }
    }

    static FuturesContractAddedCompleteEvent Complete(long eventId, Guid commandId)
        => new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesContractAddedCompleteEvent.Actor,
                FuturesContractAddedCompleteEvent.Verb,
                "G3"),
            EventId = eventId,
            CommandId = commandId,
            AggregateId = "G3",
            EventSource = nameof(G3LiveNatsAcceptanceTests),
            ReceivedOn = DateTime.UtcNow
        };

    static IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent WorkflowNotification(
        long revision,
        WorkflowStrategyMachineStatus status)
    {
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES.G3", new DateOnly(2026, 9, 9), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(Guid.Parse("0e3b92e2-4d7d-4fd8-934d-538f762c592c"));
        var now = new DateTime(2026, 9, 9, 14, 0, 0, DateTimeKind.Utc).AddSeconds(revision);
        var state = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = entity,
            WorkflowId = workflowId,
            WorkflowRevision = revision,
            Status = status,
            Outcome = status == WorkflowStrategyMachineStatus.Completed
                ? StrategyWorkflowOutcome.Completed
                : StrategyWorkflowOutcome.None,
            CurrentStage = status == WorkflowStrategyMachineStatus.Completed
                ? StrategyWorkflowStage.RiskManagement
                : StrategyWorkflowStage.RegimeDiscovery,
            StartedAtUtc = now.AddMinutes(-1),
            UpdatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10),
            TerminalAtUtc = status == WorkflowStrategyMachineStatus.Completed ? now : null
        };
        return new()
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor,
                IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb,
                entity.Format()),
            Id = Guid.NewGuid(),
            EntityId = entity,
            EventId = revision,
            CommandId = Guid.NewGuid(),
            AggregateId = workflowId.ToString(),
            EventSource = nameof(G3LiveNatsAcceptanceTests),
            ReceivedOn = now,
            WorkflowId = workflowId,
            WorkflowRevision = revision,
            SourceEventId = Guid.NewGuid(),
            State = state,
            UpdatedAtUtc = now
        };
    }

    static FuturesContractAddedFailEvent Failure(Guid commandId)
        => new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesContractAddedFailEvent.Actor,
                FuturesContractAddedFailEvent.Verb,
                "G3"),
            EventId = 128,
            CommandId = commandId,
            AggregateId = "G3",
            EventSource = nameof(G3LiveNatsAcceptanceTests),
            ErrorCode = 7303,
            ErrorMessage = "deterministic G3 failure",
            ErrorType = ErrorType.Command,
            ReceivedOn = DateTime.UtcNow
        };

    static async Task PublishAsync<TEvent>(
        NatsClient client,
        NatsMessagePackDataSerializer serializer,
        TEvent @event)
        where TEvent : class, IEvent
        => await client.PublishAsync(
            @event.Subject.ToString(),
            serializer.Serialize(@event),
            serializer: NatsDefaultSerializer<byte[]>.Default);

    static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
            await Task.Delay(25, timeout.Token);
    }
}
