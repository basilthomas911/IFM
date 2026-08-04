using System.Buffers;
using FluentAssertions;
using MessagePack;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public class NatsOwnedQueryMessageTests
{
    [Fact]
    public void AsQuery_DeserializesFromOwnedMemory_AndReleaseIsIdempotent()
    {
        var query = CreateQuery();
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<TestQuery>.Default.Serialize(writer, query);
        var owner = NatsMemoryOwner<byte>.Allocate(writer.WrittenCount);
        writer.WrittenSpan.CopyTo(owner.Span);
        var source = new NatsMsg<NatsMemoryOwner<byte>>(
            query.Subject.ToString(),
            "reply.inbox",
            default,
            default,
            owner,
            default!,
            default);
        var message = new NatsOwnedQueryMessage(source, query.Subject);

        var deserialized = message.AsQuery<TestQuery, TestQueryResult>();

        deserialized.Should().BeEquivalentTo(query);
        message.ReleasePayload();
        message.ReleasePayload();
        message.IsPayloadReleased.Should().BeTrue();
        FluentActions.Invoking(() => message.AsQuery<TestQuery, TestQueryResult>())
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task QueryContext_ReplyAtomicallyRemovesMessageInfo()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var context = new QueryActorContext(
            supervisor,
            new ActorMailboxId(ActorType.Query, "TestQuery"));
        var threadId = new ActorThreadId(ActorType.Query, "TestQuery", "42");
        var query = CreateQuery();
        var message = new TrackingQueryMessage(query.Subject);
        context.SetMessageInfo(
            threadId,
            "Get",
            new ActorMessageInfo(message, query));

        await context.ReplyAsync(
            threadId,
            "Get",
            new ServiceResult<TestQueryResult>(new TestQueryResult { Value = 42 }));

        context.PendingMessageCount.Should().Be(0);
        message.ReplyCount.Should().Be(1);
        message.LastReply.Should().BeOfType<ServiceResult<TestQueryResult>>()
            .Which.Value.Should().BeEquivalentTo(new TestQueryResult { Value = 42 });
    }

    [Fact]
    public void QueryContext_RemoveCleansUpTerminalNoReplyPath()
    {
        var context = new QueryActorContext(
            Substitute.For<IActorSupervisor>(),
            new ActorMailboxId(ActorType.Query, "TestQuery"));
        var threadId = new ActorThreadId(ActorType.Query, "TestQuery", "42");
        var query = CreateQuery();
        context.SetMessageInfo(
            threadId,
            "Get",
            new ActorMessageInfo(new TrackingQueryMessage(query.Subject), query));

        context.RemoveMessageInfo(threadId, "Get").Should().BeTrue();

        context.PendingMessageCount.Should().Be(0);
        context.RemoveMessageInfo(threadId, "Get").Should().BeFalse();
    }

    static TestQuery CreateQuery() => new()
    {
        Subject = new ActorSubject(ActorType.Query, "TestQuery", "Get", "42"),
        EntityId = new ActorEntityId("42"),
        Value = 42
    };

    sealed class TrackingQueryMessage(ActorSubject subject) : IActorMessage
    {
        public int ReplyCount { get; private set; }
        public object? LastReply { get; private set; }
        public ActorSubject Subject { get; } = subject;
        public ActorSubject ReplySubject { get; set; } = default!;

        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;

        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        {
            ReplyCount++;
            LastReply = result;
            return ValueTask.CompletedTask;
        }

        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }

    [MessagePackObject]
    public sealed record TestQuery : IQuery<TestQueryResult>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public IActorEntityId EntityId { get; init; } = new ActorEntityId();
        [Key(2)] public int Value { get; init; }
        [IgnoreMember] public int ErrorCode => 0;
        [IgnoreMember] public string? QueryParams => null;
    }

    [MessagePackObject]
    public sealed record TestQueryResult
    {
        [Key(0)] public int Value { get; init; }
    }
}
