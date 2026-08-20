using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Lifecycle;

[Trait("Category", "G4LiveNats")]
public sealed class G4LiveTransportResilienceTests
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Disconnect_reconnect_retains_failure_correlation_and_one_listener()
    {
        if (!G4Enabled())
            return;

        var brokerUri = new Uri(Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222");
        await using var proxy = new TcpFaultProxy(brokerUri.Host, brokerUri.Port);
        await using var publisher = new NatsClient(brokerUri.ToString());
        await publisher.ConnectAsync();

        var consumer = new CommandResponseUIEventConsumer(
            new NatsEventListenerOptions { Url = proxy.Uri.ToString() },
            NullLogger.Instance);
        var serializer = new NatsMessagePackDataSerializer();
        var received = new List<IEvent>();
        var gate = new object();
        IEvent[] prototypes =
        [
            new FuturesContractAddedCompleteEvent(),
            new FuturesContractAddedFailEvent()
        ];

        try
        {
            await consumer.StartAsync(prototypes, Capture);
            await WaitForAsync(() => consumer.State == EventListenerState.Running && proxy.ForwardedConnectionCount >= 1);
            await Task.Delay(250);

            var initialCommandId = Guid.NewGuid();
            await PublishAsync(publisher, serializer, Complete(1, initialCommandId));
            await WaitForAsync(() => Count() == 1);

            var connectionsBeforeFault = proxy.ForwardedConnectionCount;
            proxy.PauseAndDropConnections();
            await WaitForAsync(() => proxy.ActiveConnectionCount == 0);
            await Task.Delay(300);
            proxy.Resume();
            await WaitForAsync(() => proxy.ForwardedConnectionCount > connectionsBeforeFault);
            await Task.Delay(500);

            var failedCommandId = Guid.NewGuid();
            var failedEvent = Failure(2, failedCommandId);
            await PublishUntilAsync(publisher, serializer, failedEvent, () => Count() >= 2);
            await Task.Delay(300);
            Count().Should().Be(2, "the restored subscription must deliver the correlation event once");
            var failure = Snapshot()[1].Should().BeOfType<FuturesContractAddedFailEvent>().Subject;
            failure.CommandId.Should().Be(failedCommandId);
            Snapshot().Count(item => item.CommandId == failedCommandId).Should().Be(1);
            failure.ErrorCode.Should().Be(7404);
            failure.ErrorMessage.Should().Be("G4 reconnect failure correlation");

            var reopenedCommandId = Guid.NewGuid();
            await PublishAsync(publisher, serializer, Complete(3, reopenedCommandId));
            await WaitForAsync(() => Count() == 3);
            Snapshot().Count(item => item.CommandId == reopenedCommandId).Should().Be(1);

            await consumer.StopAsync();
            await PublishAsync(publisher, serializer, Complete(4, Guid.NewGuid()));
            await Task.Delay(300);
            Count().Should().Be(3, "a stopped lifecycle must reject later deliveries");
        }
        finally
        {
            await consumer.StopAsync();
        }

        void Capture(IEvent @event)
        {
            lock (gate)
                received.Add(@event);
        }

        int Count()
        {
            lock (gate)
                return received.Count;
        }

        IEvent[] Snapshot()
        {
            lock (gate)
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
                "G4"),
            Id = Guid.NewGuid(),
            EventId = eventId,
            CommandId = commandId,
            AggregateId = "G4",
            EventSource = nameof(G4LiveTransportResilienceTests),
            ReceivedOn = DateTime.UtcNow
        };

    static FuturesContractAddedFailEvent Failure(long eventId, Guid commandId)
        => new()
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesContractAddedFailEvent.Actor,
                FuturesContractAddedFailEvent.Verb,
                "G4"),
            Id = Guid.NewGuid(),
            EventId = eventId,
            CommandId = commandId,
            AggregateId = "G4",
            EventSource = nameof(G4LiveTransportResilienceTests),
            ErrorCode = 7404,
            ErrorMessage = "G4 reconnect failure correlation",
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

    static async Task PublishUntilAsync<TEvent>(
        NatsClient client,
        NatsMessagePackDataSerializer serializer,
        TEvent @event,
        Func<bool> delivered)
        where TEvent : class, IEvent
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!delivered())
        {
            await PublishAsync(client, serializer, @event);
            await Task.Delay(250, timeout.Token);
        }
    }

    static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
            await Task.Delay(25, timeout.Token);
    }

    static bool G4Enabled()
        => string.Equals(Environment.GetEnvironmentVariable("IFM_RUN_UI_G4"), "1", StringComparison.Ordinal);
}
