using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents a message used in the NATS-based actor messaging system, encapsulating the underlying NATS message and
/// providing methods to deserialize its data into specific command, event, or query types.
/// </summary>
/// <remarks>This type is designed to facilitate communication between actors in a distributed system using NATS
/// as the messaging infrastructure. It provides utility methods to deserialize the message payload into strongly-typed
/// data structures, as well as a mechanism to send replies.</remarks>
/// <param name="NatsMessage"></param>
public record struct NatsActorMessage(NatsMsg<byte[]> NatsMessage)
        : IActorMessage
{
    static readonly NatsMessagePackDataSerializer  _dataSerializer = new();
    static readonly NatsByteArrayMessageSerializer _msgSerializer = new();

    public readonly TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
       => _dataSerializer.Deserialize<TCommand>(NatsMessage.Data!);

    public readonly TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
        => _dataSerializer.Deserialize<TEvent>(NatsMessage.Data!);

    public readonly TQuery? AsQuery<TQuery, TResult>() 
        where TQuery : class,IQuery<TResult>
        where TResult : class
        => _dataSerializer.Deserialize<TQuery>(NatsMessage.Data!);

    public  readonly async ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
    {
        var data = _dataSerializer.Serialize(result);
        if (!string.IsNullOrEmpty(NatsMessage.ReplyTo))
        {
            await NatsMessage.ReplyAsync(data, serializer: _msgSerializer);
        }
    }

    public readonly ActorSubject Subject 
        => ToSubject(NatsMessage.Subject);

    public ActorSubject ReplySubject { get; set; } = default!;

    public readonly NatsMsg<byte[]> GetMessage()
        => NatsMessage!;

    static ActorSubject ToSubject(string subject)
        => subject.ToSubject();

   
}
