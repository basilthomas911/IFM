using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// GC-owned byte-array adapter retained while query and event payload ownership is migrated in later stages.
/// </summary>
public sealed class LegacyNatsActorMessage(NatsMsg<byte[]> message) : IActorMessage
{
    public int AdmissionSizeBytes => message.Data?.Length ?? 0;

    public bool CanReply => !string.IsNullOrEmpty(message.ReplyTo);

    public ActorSubject Subject { get; } = message.Subject.ToSubject();

    public ActorSubject ReplySubject { get; set; } = default!;

    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
        => ActorExtensions.DataSerializer.Deserialize<TCommand>(message.Data!);

    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
        => ActorExtensions.DataSerializer.Deserialize<TEvent>(message.Data!);

    public TQuery? AsQuery<TQuery, TResult>()
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => ActorExtensions.DataSerializer.Deserialize<TQuery>(message.Data!);

    public async ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
    {
        if (string.IsNullOrEmpty(message.ReplyTo))
            return;
        var data = ActorExtensions.DataSerializer.Serialize(result);
        await message.ReplyAsync(data, serializer: ActorExtensions.MsgSerializer).ConfigureAwait(false);
    }

    public void ReleasePayload()
    {
        // The staged legacy payload is a GC-owned byte array.
    }

    public NatsMsg<byte[]> GetMessage() => message;

    public void Dispose()
    {
        // The staged legacy payload has no pooled ownership.
    }
}
