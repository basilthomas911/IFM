using System.Buffers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>One bounded in-flight payload. The broker still owns pending work until the completion result is confirmed.</summary>
internal sealed class RealtimeBrokerActorMessage(CapturedRealtimeSource source, ActorSubject destination) : IActorMessage, IRealtimeBrokerDelivery
{
    readonly TaskCompletionSource<BrokerDeliveryResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    CapturedRealtimeSource? payload = source;
    readonly RealtimeSourcePublication metadata = source.Source with { Payload = ReadOnlyMemory<byte>.Empty };
    readonly int payloadSize = source.Source.Payload.Length;
    int disposed, released;
    public Task<BrokerDeliveryResult> Completion => completion.Task;
    public ActorSubject Subject => destination;
    public ActorSubject SourceSubject => metadata.Subject;
    public string? SourceDataset => metadata.Dataset;
    public Guid SourceGenerationId => metadata.SourceGenerationId;
    public MessageOrigin? Origin => metadata.Origin;
    public System.Diagnostics.ActivityContext TraceContext => System.Diagnostics.ActivityContext.TryParse(metadata.TraceParent, metadata.TraceState, true, out var value) ? value : default;
    public int AdmissionSizeBytes => Volatile.Read(ref released) == 0 ? payloadSize : 0;
    public ActorSubject ReplySubject { get; set; }
    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref released) != 0, this);
        var value = NatsMessagePackSerializer<TEvent>.Default.Deserialize(new ReadOnlySequence<byte>(payload!.Source.Payload));
        return value is null ? null : MessageOrigins.Restore(value, Origin);
    }
    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => throw new InvalidOperationException("Realtime mailbox contains events only.");
    public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => throw new InvalidOperationException("Realtime mailbox contains events only.");
    public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
    public ValueTask CompleteDeliveryAsync(bool succeeded) { completion.TrySetResult(succeeded ? BrokerDeliveryResult.Succeeded : BrokerDeliveryResult.Failed); return ValueTask.CompletedTask; }
    public ValueTask CompleteFencedAsync() { completion.TrySetResult(BrokerDeliveryResult.Fenced); return ValueTask.CompletedTask; }
    public void ReleasePayload() { if (Interlocked.Exchange(ref released, 1) == 0) payload = null; }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        ReleasePayload();
        // Retirement/disposal before settlement never invents success.
        completion.TrySetResult(BrokerDeliveryResult.Failed);
    }
}
internal enum BrokerDeliveryResult { Succeeded, Failed, Fenced }
