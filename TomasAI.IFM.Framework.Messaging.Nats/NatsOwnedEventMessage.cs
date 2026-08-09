using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// One mailbox branch over a reference-counted pooled event payload. Every routed
/// destination receives a distinct branch and releases only its own reference.
/// </summary>
public sealed class NatsOwnedEventMessage : IActorMessage
{
    static readonly MessagePackSerializerOptions SerializerOptions =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    readonly NatsSharedEventPayload _payload;
    int _released;

    internal NatsOwnedEventMessage(
        NatsSharedEventPayload payload,
        ActorSubject subject)
    {
        _payload = payload;
        Subject = subject;
    }

    public ActorSubject Subject { get; }

    public int AdmissionSizeBytes
        => Volatile.Read(ref _released) == 0 ? _payload.Memory.Length : 0;

    public ActorSubject ReplySubject { get; set; } = default!;

    internal bool IsPayloadReleased => Volatile.Read(ref _released) != 0;

    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
        => throw new InvalidOperationException(
            "An owned event message cannot be deserialized as a command.");

    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _released) != 0,
            nameof(NatsOwnedEventMessage));
        var memory = _payload.Memory;
        if (memory.IsEmpty)
            return default;
        return MessagePackSerializer.Deserialize<TEvent>(
            new ReadOnlySequence<byte>(memory),
            SerializerOptions);
    }

    public TQuery? AsQuery<TQuery, TResult>()
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => throw new InvalidOperationException(
            "An owned event message cannot be deserialized as a query.");

    public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
        => ValueTask.CompletedTask;

    public void ReleasePayload()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;
        _payload.ReleaseBranch();
    }

    public void Dispose() => ReleasePayload();

    public NatsMsg<byte[]> GetMessage()
        => throw new InvalidOperationException(
            "Owned event payloads cannot be exposed as byte arrays.");
}

/// <summary>
/// Owns one NATS pooled event buffer. The ingress root reference prevents the
/// buffer from being returned while mailbox branches are still being created.
/// </summary>
internal sealed class NatsSharedEventPayload : IDisposable
{
    NatsMemoryOwner<byte> _owner;
    int _referenceCount = 1;
    int _rootReleased;
    int _disposed;

    internal NatsSharedEventPayload(NatsMemoryOwner<byte> owner)
        => _owner = owner;

    internal ReadOnlyMemory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref _disposed) != 0,
                nameof(NatsSharedEventPayload));
            return _owner.Memory;
        }
    }

    internal int ReferenceCount => Math.Max(0, Volatile.Read(ref _referenceCount));

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal NatsOwnedEventMessage CreateBranch(ActorSubject subject)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            nameof(NatsSharedEventPayload));
        Interlocked.Increment(ref _referenceCount);
        return new NatsOwnedEventMessage(this, subject);
    }

    internal void ReleaseBranch()
    {
        var remaining = Interlocked.Decrement(ref _referenceCount);
        if (remaining == 0)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Dispose();
                _owner = default;
            }
            return;
        }

        if (remaining < 0)
            throw new InvalidOperationException("Event payload reference count fell below zero.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _rootReleased, 1) != 0)
            return;
        ReleaseBranch();
    }
}
