using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// One mailbox branch over a reference-counted pooled event payload. Every routed
/// destination receives a distinct branch and releases only its own reference.
/// </summary>
public sealed class NatsOwnedEventMessage : IActorMessage, IActorDeliveryCompletion
{
    readonly NatsSharedEventPayload _payload;
    readonly EventFanoutDelivery? _delivery;
    int _released;
    int _deliveryCompleted;

    internal NatsOwnedEventMessage(
        NatsSharedEventPayload payload,
        ActorSubject subject,
        EventFanoutDelivery? delivery = null)
    {
        _payload = payload;
        Subject = subject;
        _delivery = delivery;
    }

    public ActorSubject Subject { get; }
    public ActorSubject SourceSubject => _payload.SourceSubject;
    public string? SourceDataset => _payload.SourceDataset;
    public Guid SourceGenerationId => _payload.SourceGenerationId;

    public System.Diagnostics.ActivityContext TraceContext => _payload.TraceContext;

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
        return NatsMessagePackSerializer<TEvent>.Default.Deserialize(
            new System.Buffers.ReadOnlySequence<byte>(memory));
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

    public ValueTask CompleteDeliveryAsync(bool succeeded)
    {
        if (_delivery is null || Interlocked.Exchange(ref _deliveryCompleted, 1) != 0)
            return ValueTask.CompletedTask;
        return _delivery.CompleteHandoffAsync(succeeded);
    }

    public NatsMsg<byte[]> GetMessage()
        => throw new InvalidOperationException(
            "Owned event payloads cannot be exposed as byte arrays.");
}
