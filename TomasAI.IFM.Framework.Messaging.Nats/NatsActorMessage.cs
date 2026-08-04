using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents a message used in the NATS-based actor messaging system, encapsulating the underlying NATS message and
/// providing methods to deserialize its data into specific command, event, or query types.
/// </summary>
/// <remarks>This type is designed to facilitate communication between actors in a distributed system using NATS
/// as the messaging infrastructure. It provides utility methods to deserialize the message payload into strongly-typed
/// data structures, as well as a mechanism to send replies.</remarks>
/// <param name="NatsMessage"></param>
public sealed class NatsActorMessage(NatsMsg<byte[]> natsMessage)
    : IActorMessage
{
    static readonly NatsMessagePackDataSerializer  _dataSerializer = new();
    static readonly NatsByteArrayMessageSerializer _msgSerializer = new();

    public NatsMsg<byte[]> NatsMessage { get; } = natsMessage;

    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
       => _dataSerializer.Deserialize<TCommand>(NatsMessage.Data!);

    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
        => _dataSerializer.Deserialize<TEvent>(NatsMessage.Data!);

    public TQuery? AsQuery<TQuery, TResult>()
        where TQuery : class,IQuery<TResult>
        where TResult : class
        => _dataSerializer.Deserialize<TQuery>(NatsMessage.Data!);

    public async ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
    {
        var data = _dataSerializer.Serialize(result);
        if (!string.IsNullOrEmpty(NatsMessage.ReplyTo))
        {
            await NatsMessage.ReplyAsync(data, serializer: _msgSerializer);
        }
    }

    public ActorSubject Subject
        => ToSubject(NatsMessage.Subject);

    public ActorSubject ReplySubject { get; set; } = default!;

    public NatsMsg<byte[]> GetMessage()
        => NatsMessage!;

    public void ReleasePayload()
    {
        // Legacy byte[] messages are GC-owned and have no explicit payload lease.
    }

    public void Dispose()
    {
        // No unmanaged or pooled ownership on the staged legacy path.
    }

    static ActorSubject ToSubject(string subject)
        => subject.ToSubject();

   
}
