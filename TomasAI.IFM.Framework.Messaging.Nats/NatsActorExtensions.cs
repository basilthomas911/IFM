using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Framework.Messaging.Nats;
/// <summary>Wire serialization and replies belong to the NATS adapter.</summary>
public static class NatsActorExtensions
{
    public static INatsSerializer<byte[]> MsgSerializer { get; set; } = default!;
    static IDataSerializer DataSerializer => ActorExtensions.DataSerializer;
    /// <summary>
    /// Deserializes a <see cref="NatsMsg{byte[]}"/> payload into a concrete command type.
    /// </summary>
    /// <typeparam name="TCommand">Target command type.</typeparam>
    /// <param name="message">NATS message containing serialized command data.</param>
    /// <returns>Deserialized command instance or null if data is missing.</returns>
    public static TCommand? AsCommand<TCommand>(this NatsMsg<byte[]> message) where TCommand : class, ICommand
      => DataSerializer.Deserialize<TCommand>(message.Data);

    /// <summary>
    /// Deserializes a <see cref="NatsMsg{byte[]}"/> payload into a concrete event type.
    /// </summary>
    /// <typeparam name="TEvent">Target event type.</typeparam>
    /// <param name="message">NATS message containing serialized event data.</param>
    /// <returns>Deserialized event instance or null if data is missing.</returns>
    public static TEvent? AsEvent<TEvent>(this NatsMsg<byte[]> message) where TEvent : class, IEvent
        => DataSerializer.Deserialize<TEvent>(message.Data!);

    /// <summary>
    /// Deserializes a NATS message into a query instance.
    /// </summary>
    /// <typeparam name="TQuery">Query type.</typeparam>
    /// <typeparam name="TResult">Expected query result type.</typeparam>
    /// <param name="message">NATS message containing serialized query data.</param>
    /// <returns>The deserialized query instance or null if data is missing.</returns>
    public static TQuery? AsQuery<TQuery, TResult>(this NatsMsg<byte[]> message)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => DataSerializer.Deserialize<TQuery>(message.Data!);

    /// <summary>
    /// Sends an asynchronous reply to the current NATS message with the specified result, serializing the result as the
    /// reply payload.
    /// </summary>
    /// <remarks>No reply is sent if the message does not specify a reply subject. The result object is
    /// serialized before being sent; ensure that the type is supported by the configured serializer.</remarks>
    /// <typeparam name="TResult">The type of the result object to serialize and send as the reply payload. Must be a reference type.</typeparam>
    /// <param name="message">The NATS message to which the reply will be sent. The message must have a non-empty reply subject.</param>
    /// <param name="result">The result object to serialize and include in the reply message. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous reply operation.</returns>
    public static async ValueTask NatsReplyAsync<TResult>(this NatsMsg<byte[]> message, TResult result) where TResult : class
    {
        var data = DataSerializer.Serialize(result);
        if (!string.IsNullOrEmpty(message.ReplyTo))
        {
            await message.ReplyAsync(data, serializer: MsgSerializer).ConfigureAwait(false);
        }
    }

}
