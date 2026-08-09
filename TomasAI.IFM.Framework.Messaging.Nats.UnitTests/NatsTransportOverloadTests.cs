using System.Buffers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public sealed class NatsTransportOverloadTests
{
    public static TheoryData<Type> CompatibleResultTypes => new()
    {
        typeof(GuidResult),
        typeof(string),
        typeof(List<ReadModel>),
        typeof(ReadModel)
    };

    [Theory]
    [MemberData(nameof(CompatibleResultTypes))]
    public void OverloadReply_IsStructurallyCompatibleWithTypedServiceResult(Type resultType)
    {
        var method = typeof(NatsTransportOverloadTests)
            .GetMethod(nameof(AssertTypedCompatibility), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType);

        method.Invoke(null, null);
    }

    [Theory]
    [MemberData(nameof(CompatibleResultTypes))]
    public void LegacyOverloadReply_IsStructurallyCompatibleWithTypedServiceResult(Type resultType)
    {
        var method = typeof(NatsTransportOverloadTests)
            .GetMethod(nameof(AssertLegacyTypedCompatibility), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType);

        method.Invoke(null, null);
    }

    [Fact]
    public async Task RejectedRequest_RepliesAndDisposesExactlyOnce()
    {
        var message = new TrackingMessage(canReply: true);

        await NatsTransportOverload.SettleCoreRejectionAsync(
            message,
            ActorType.Query,
            ActorAdmissionReason.GlobalMessageLimit,
            -429,
            CoreNatsTrafficClass.RequestReplyOnly,
            NullLogger.Instance);

        message.ReplyCount.Should().Be(1);
        message.DisposeCount.Should().Be(1);
        var reply = message.LastReply.Should().BeOfType<ServiceResult<object>>().Subject;
        reply.Success.Should().BeFalse();
        reply.ErrorCode.Should().Be(-429);
        reply.ErrorMessage.Should().Be(NatsTransportOverload.RetryableMessage);
        reply.Value.Should().BeNull();
    }

    [Fact]
    public async Task ReplyFailure_StillDisposesExactlyOnce()
    {
        var message = new TrackingMessage(canReply: true, failReply: true);

        await NatsTransportOverload.SettleCoreRejectionAsync(
            message,
            ActorType.Command,
            ActorAdmissionReason.MailboxLimit,
            -429,
            CoreNatsTrafficClass.RequestReplyOnly,
            NullLogger.Instance);

        message.ReplyCount.Should().Be(1);
        message.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExplicitOptionalTraffic_DropsAndDisposesExactlyOnce()
    {
        var message = new TrackingMessage(canReply: false);

        await NatsTransportOverload.SettleCoreRejectionAsync(
            message,
            ActorType.Notify,
            ActorAdmissionReason.ActorTypeByteLimit,
            -429,
            CoreNatsTrafficClass.Optional,
            NullLogger.Instance);

        message.ReplyCount.Should().Be(0);
        message.DisposeCount.Should().Be(1);
    }

    static void AssertTypedCompatibility<TResult>() where TResult : class
    {
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<ServiceResult<object>>.Default.Serialize(
            writer,
            NatsTransportOverload.CreateReply(-429));

        var result = NatsMessagePackSerializer<ServiceResult<TResult>>.Default.Deserialize(
            new ReadOnlySequence<byte>(writer.WrittenMemory));

        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(-429);
        result.ErrorMessage.Should().Be(NatsTransportOverload.RetryableMessage);
        result.Value.Should().BeNull();
    }

    static void AssertLegacyTypedCompatibility<TResult>() where TResult : class
    {
        var serializer = new NatsMessagePackDataSerializer();
        var bytes = serializer.Serialize(NatsTransportOverload.CreateReply(-429));

        var result = serializer.Deserialize<ServiceResult<TResult>>(bytes);

        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(-429);
        result.ErrorMessage.Should().Be(NatsTransportOverload.RetryableMessage);
        result.Value.Should().BeNull();
    }

    public sealed record ReadModel(int Value);

    sealed class TrackingMessage(bool canReply, bool failReply = false) : IActorMessage
    {
        public bool CanReply { get; } = canReply;
        public int DisposeCount { get; private set; }
        public int ReplyCount { get; private set; }
        public object? LastReply { get; private set; }
        public ActorSubject Subject { get; } = new(ActorType.Query, "Test", "Get", "42");
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
            return failReply
                ? ValueTask.FromException(new InvalidOperationException("reply failed"))
                : ValueTask.CompletedTask;
        }

        public void ReleasePayload() => Dispose();
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => DisposeCount++;
    }
}
