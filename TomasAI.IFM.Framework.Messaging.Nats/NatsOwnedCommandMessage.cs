using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Command message that owns a NATS pooled payload until the command is deserialized.
/// Ownership is transferred linearly from ingress to stripe, mailbox, and actor processing.
/// </summary>
public sealed class NatsOwnedCommandMessage : IActorMessage
{
    static readonly MessagePackSerializerOptions SerializerOptions =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    readonly INatsConnection _connection;
    readonly string? _replyTo;
    NatsMemoryOwner<byte> _owner;
    int _released;

    public NatsOwnedCommandMessage(
        NatsMsg<NatsMemoryOwner<byte>> sourceMessage,
        ActorSubject subject)
    {
        _connection = sourceMessage.Connection;
        _replyTo = sourceMessage.ReplyTo;
        _owner = sourceMessage.Data;
        Subject = subject;
    }

    public ActorSubject Subject { get; }

    public int AdmissionSizeBytes
        => Volatile.Read(ref _released) == 0 ? _owner.Memory.Length : 0;

    public bool CanReply => !string.IsNullOrEmpty(_replyTo);

    public ActorSubject ReplySubject { get; set; } = default!;

    internal bool IsPayloadReleased => Volatile.Read(ref _released) != 0;

    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
        => Deserialize<TCommand>();

    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
        => throw new InvalidOperationException("An owned command message cannot be deserialized as an event.");

    public TQuery? AsQuery<TQuery, TResult>()
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => throw new InvalidOperationException("An owned command message cannot be deserialized as a query.");

    T? Deserialize<T>()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _released) != 0,
            nameof(NatsOwnedCommandMessage));
        var owner = _owner;
        if (owner.Memory.IsEmpty)
            return default;
        return MessagePackSerializer.Deserialize<T>(
            new ReadOnlySequence<byte>(owner.Memory),
            SerializerOptions);
    }

    public async ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
    {
        if (string.IsNullOrEmpty(_replyTo))
            return;
        await _connection.PublishAsync(
            _replyTo,
            result,
            serializer: NatsMessagePackSerializer<TResult>.Default).ConfigureAwait(false);
    }

    public void ReleasePayload()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;
        _owner.Dispose();
        _owner = default;
    }

    public void Dispose() => ReleasePayload();

    public NatsMsg<byte[]> GetMessage()
        => throw new InvalidOperationException("Owned command payloads cannot be exposed as byte arrays.");
}
