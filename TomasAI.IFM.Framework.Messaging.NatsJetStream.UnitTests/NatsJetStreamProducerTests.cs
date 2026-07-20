using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream.Models;
using NATS.Client.Core;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Threading.Tasks;
using System;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public class NatsJetStreamProducerTests
{
    /// <summary>
    /// Minimal test command implementing ICommand for test purposes.
    /// </summary>
    record TestCommand(Guid CommandId, string CommandName) : ICommand
    {
        public string CommandName => CommandName;
        public BoundedContextName RouteTo => BoundedContextName.Undefined;
        public Guid CommandId { get; } = CommandId;
        public string StreamId => string.Empty;
        public string EventSource => string.Empty;
        public int ErrorCode => 0;
    }

    /// <summary>
    /// Minimal test event implementing IEvent for test purposes.
    /// </summary>
    record TestEvent(Guid Id, string EventName) : IEvent
    {
        public Guid Id { get; set; } = Id;
        public string UserName => string.Empty;
        public string EventName => EventName;
        public EventType EventType => EventType.DomainEvent;
        public long EventId { get; set; }
        public Guid CommandId { get; set; }
        public string AggregateId { get; set; } = string.Empty;
        public string EventSource => string.Empty;
        public DateTime ReceivedOn => DateTime.UtcNow;
        public void CheckForEmptyCommandId() { }
        public IEvent SetEventSource(string eventSource) => this;
        public IEvent SetReceivedOn(DateTime receivedOn) => this;
    }

    [Fact]
    public async Task PublishAsync_Command_CallsPublishAsyncOnJetStreamContext()
    {
        // Arrange
        var mailbox = new ActorMailboxId(ActorType.Command, "unit.test");
        var js = Substitute.For<INatsJSContext>();

        // Configure PublishAsync to return a dummy PubAckResponse substitute
        var fakeAck = Substitute.For<PubAckResponse>();
        js.PublishAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(Task.FromResult((PubAckResponse)fakeAck));

        var options = new NatsJetStreamProducerOptions
        {
            JetStreamContext = js,
            SubjectPrefix = "prefix",
            JsonSerializerOptions = new JsonSerializerOptions()
        };

        var logger = Substitute.For<ILogger<NatsJetStreamProducer>>();
        var producer = new NatsJetStreamProducer(mailbox, options, logger);
        var cmd = new TestCommand(Guid.NewGuid(), "TestCommand");

        // Act
        await producer.PublishAsync(cmd);

        // Assert
        await js.Received(1).PublishAsync(Arg.Is<string>(s => s == "prefix.unit.test"), Arg.Any<byte[]>());
        fakeAck.Received(1).EnsureSuccess();
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex == null),
            Arg.Any<Func<object, Exception, string>>() );
    }

    [Fact]
    public async Task PublishAsync_Event_CallsPublishAsyncOnJetStreamContext()
    {
        // Arrange
        var mailbox = new ActorMailboxId(ActorType.Event, "unit.test.event");
        var js = Substitute.For<INatsJSContext>();

        var fakeAck = Substitute.For<PubAckResponse>();
        js.PublishAsync(Arg.Any<string>(), Arg.Any<byte[]>()).Returns(Task.FromResult((PubAckResponse)fakeAck));

        var options = new NatsJetStreamProducerOptions
        {
            JetStreamContext = js,
            SubjectPrefix = "prefix",
            JsonSerializerOptions = new JsonSerializerOptions()
        };

        var logger = Substitute.For<ILogger<NatsJetStreamProducer>>();
        var producer = new NatsJetStreamProducer(mailbox, options, logger);
        var evt = new TestEvent(Guid.NewGuid(), "TestEvent");

        // Act
        await producer.PublishAsync(evt);

        // Assert
        await js.Received(1).PublishAsync(Arg.Is<string>(s => s == "prefix.unit.test.event"), Arg.Any<byte[]>());
        fakeAck.Received(1).EnsureSuccess();
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex == null),
            Arg.Any<Func<object, Exception, string>>() );
    }
}
