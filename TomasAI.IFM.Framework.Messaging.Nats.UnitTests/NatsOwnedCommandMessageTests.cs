using System.Buffers;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public class NatsOwnedCommandMessageTests
{
    [Fact]
    public void AsCommand_DeserializesFromOwnedMemory_AndReleaseIsIdempotent()
    {
        var command = new TestCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, "Test", "Create", "42"),
            Value = 42
        };
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<TestCommand>.Default.Serialize(writer, command);
        var owner = NatsMemoryOwner<byte>.Allocate(writer.WrittenCount);
        writer.WrittenSpan.CopyTo(owner.Span);
        var source = new NatsMsg<NatsMemoryOwner<byte>>(
            command.Subject.ToString(),
            null,
            default,
            default,
            owner,
            default!,
            default);
        var message = new NatsOwnedCommandMessage(source, command.Subject);

        message.AdmissionSizeBytes.Should().Be(writer.WrittenCount);
        var deserialized = message.AsCommand<TestCommand>();

        deserialized.Should().BeEquivalentTo(command);
        message.ReleasePayload();
        message.ReleasePayload();
        message.IsPayloadReleased.Should().BeTrue();
        message.AdmissionSizeBytes.Should().Be(0);
        FluentActions.Invoking(() => message.AsCommand<TestCommand>())
            .Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActorQueue_Stop_DisposesPendingOwnedMessages(bool useSpscQueue)
    {
        IActorThreadQueue queue = useSpscQueue
            ? new ActorThreadQueueV2(8, 1, 1)
            : new ActorThreadQueue();
        var message = new TrackingActorMessage();
        queue.Start();
        queue.Write(message).Should().BeTrue();

        queue.Stop();

        message.DisposeCount.Should().Be(1);
        (queue as IDisposable)?.Dispose();
    }

    [Fact]
    public void SpscRingBuffer_Stop_DisposesPendingOwnedMessages()
    {
        var buffer = new NatsActorSpscRingBuffer(8, 1, 1);
        var message = new TrackingActorMessage();
        buffer.Start();
        buffer.Enqueue(message);

        buffer.Stop();

        message.DisposeCount.Should().Be(1);
    }

    sealed class TrackingActorMessage : IActorMessage
    {
        public int DisposeCount { get; private set; }
        public ActorSubject Subject { get; } = new(ActorType.Command, "Test", "Create", "42");
        public ActorSubject ReplySubject { get; set; } = default!;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() => Dispose();
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => DisposeCount++;
    }

    public sealed record TestCommand : ICommand
    {
        public ActorSubject Subject { get; init; }
        public string CommandName => nameof(TestCommand);
        public BoundedContextName RouteTo => BoundedContextName.Undefined;
        public Guid CommandId { get; init; }
        public string StreamId => Subject.StreamId;
        public string EventSource => "TestActor";
        public int ErrorCode => 0;
        public int Value { get; init; }
    }
}
