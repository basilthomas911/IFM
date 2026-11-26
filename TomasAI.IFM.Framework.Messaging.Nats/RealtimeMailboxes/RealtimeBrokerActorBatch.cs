using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>One bounded in-flight scheduler slot; every original message keeps its own identity and settlement.</summary>
internal sealed class RealtimeBrokerActorBatch : IRealtimeBrokerBatch
{
    public RealtimeBrokerActorBatch(IReadOnlyList<IActorMessage> messages)
    {
        if (messages.Count is < 1 or > 64 || messages.Any(message => message.Subject.ThreadId != messages[0].Subject.ThreadId))
            throw new InvalidDataException("Broker delivery group crosses mailbox or size bounds.");
        Messages = messages;
    }
    public IReadOnlyList<IActorMessage> Messages { get; }
    public ActorSubject Subject => Messages[0].Subject;
    public ActorSubject SourceSubject => Messages[0].SourceSubject;
    public string? SourceDataset => Messages[0].SourceDataset;
    public Guid SourceGenerationId => Messages[0].SourceGenerationId;
    public MessageOrigin? Origin => Messages[0].Origin;
    public ActivityContext TraceContext => Messages[0].TraceContext;
    public int AdmissionSizeBytes => Messages.Sum(message => message.AdmissionSizeBytes);
    public ActorSubject ReplySubject { get; set; }
    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => throw new InvalidOperationException("Scheduler must unwrap opted-in broker batch.");
    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => throw new InvalidOperationException("Broker batches contain events only.");
    public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => throw new InvalidOperationException("Broker batches contain events only.");
    public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
    public void ReleasePayload() { foreach (var message in Messages) message.ReleasePayload(); }
    public void Dispose() { foreach (var message in Messages) message.Dispose(); }
    public async ValueTask CompleteDeliveryAsync(bool succeeded)
    { foreach (var message in Messages) if (message is IActorDeliveryCompletion delivery) await delivery.CompleteDeliveryAsync(succeeded).ConfigureAwait(false); }
    public async ValueTask CompleteFencedAsync()
    { foreach (var message in Messages) if (message is IRealtimeBrokerDelivery delivery) await delivery.CompleteFencedAsync().ConfigureAwait(false); }
}
