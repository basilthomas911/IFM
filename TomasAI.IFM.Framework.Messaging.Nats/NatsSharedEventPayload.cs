using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

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

    internal NatsSharedEventPayload(
        NatsMemoryOwner<byte> owner,
        System.Diagnostics.ActivityContext traceContext = default,
        ActorSubject sourceSubject = default,
        NatsHeaders? headers = null)
    {
        _owner = owner;
        TraceContext = traceContext;
        SourceSubject = sourceSubject;
        if (RealtimeSourceGenerationHeaders.TryRead(headers, out var dataset, out var generation))
        {
            SourceDataset = dataset;
            SourceGenerationId = generation;
        }
        NatsMessagingMetrics.AcquirePayloadLease(owner.Memory.Length);
    }

    internal System.Diagnostics.ActivityContext TraceContext { get; }
    internal ActorSubject SourceSubject { get; }
    internal string? SourceDataset { get; }
    internal Guid SourceGenerationId { get; }

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

    internal NatsOwnedEventMessage CreateBranch(
        ActorSubject subject,
        EventFanoutDelivery? delivery = null)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            nameof(NatsSharedEventPayload));
        Interlocked.Increment(ref _referenceCount);
        return new NatsOwnedEventMessage(this, subject, delivery);
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
                NatsMessagingMetrics.ReleasePayloadLease();
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
