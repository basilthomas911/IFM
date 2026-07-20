using NATS.Client.Core;
using System.Collections.Concurrent;
using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides extension helper methods for actor commands, events, queries and NATS message handling.
/// </summary>
/// <remarks>
/// This static class centralizes common operations such as setting command identifiers, converting
/// between interface types, serializing/deserializing NATS messages, and replying to requests.
/// </remarks>
public static class ActorExtensions
{
    const int MaxSubjectCacheSize = 4096;
    static readonly ConcurrentDictionary<string, ActorSubject> _subjectMap = [];
    static int _subjectMapCount;

    /// <summary>
    /// Serializer used to convert objects to and from binary payloads for messaging.
    /// Must be initialized before calling serialization extension methods.
    /// </summary>
    public static IDataSerializer DataSerializer { get; set; }

    /// <summary>
    /// NATS message serializer used by the <see cref="NatsMsg{T}.ReplyAsync(byte[], INatsSerializer{byte[]})"/> call.
    /// Must be initialized before sending replies.
    /// </summary>
    public static INatsSerializer<byte[]> MsgSerializer { get; set; }

    public static IEvent RoutedFrom(this IEvent @event, Guid correlationId, string aggregateId, string eventSource)
    {
        EventInitHelper.SetProperty(@event, nameof(IEvent.CommandId), correlationId);
        EventInitHelper.SetProperty(@event, nameof(IEvent.AggregateId), aggregateId);
        EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
        return @event;
    }

    public static IEvent RoutedFrom(this IEvent @event, Guid correlationId, string eventSource)
    {
        EventInitHelper.SetProperty(@event, nameof(IEvent.CommandId), correlationId);
        EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
        return @event;
    }

    public static IEvent RoutedFrom(this IEvent @event)
        => @event.RoutedFrom(@event.CommandId, @event.AggregateId, @event.EventSource);

    public static IEvent RoutedFrom(this IEvent @event, ICommand command)
        => @event.RoutedFrom(command.CommandId, command.EventSource);

    /// <summary>
    /// Casts the current command interface to the concrete command type.
    /// </summary>
    /// <typeparam name="TCommand">Target concrete command type.</typeparam>
    /// <typeparam name="TEntityId">Type of the actor entity id.</typeparam>
    /// <param name="command">Command to cast.</param>
    /// <returns>Command cast to <typeparamref name="TCommand"/>.</returns>
    public static TCommand ToCommand<TCommand, TEntityId>(this ICommand<TEntityId> command) 
        where TEntityId : IActorEntityId
        where TCommand : class,ICommand<TEntityId>
    {
        return (TCommand)command;
    }

    /// <summary>
    /// Casts a general <see cref="IEvent"/> instance to a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">Target event type.</typeparam>
    /// <param name="event">Event instance.</param>
    /// <returns>The event cast to <typeparamref name="TEvent"/>.</returns>
    public static TEvent ToEvent<TEvent>(this IEvent @event) 
        where TEvent : IEvent
    {
        return (TEvent)@event;
    }

    /// <summary>
    /// Casts an <see cref="IEvent"/> to a concrete event type that implements <see cref="IEvent{TEntityId}"/>.
    /// </summary>
    /// <typeparam name="TEvent">Target event type.</typeparam>
    /// <param name="event">Event instance.</param>
    /// <returns>The event cast to <typeparamref name="TEvent"/>.</returns>
    public static TEvent ToDenormalizerEvent<TEvent>(this IEvent @event)
    where TEvent : IEvent
    {
        return (TEvent)@event;
    }

    /// <summary>
    /// Executes an asynchronous command action using the provided command and returns the resulting service result.
    /// </summary>
    /// <typeparam name="TEntityId">Type of the actor entity id.</typeparam>
    /// <param name="command">Command to execute.</param>
    /// <param name="commandAction">Async action that performs the command execution.</param>
    /// <returns>A task that yields the <see cref="ServiceResult{Guid}"/> from the command action.</returns>
    public static async Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(this ICommand<TEntityId> command, Func<ICommand, Task<ServiceResult<Guid>>> commandAction) 
        where TEntityId : IActorEntityId
    {   
        return await commandAction(command);
    }

    /// <summary>
    /// Executes the specified asynchronous command action using the provided command parameter.
    /// </summary>
    /// <param name="command">The command parameter to pass to the command action. Cannot be null.</param>
    /// <param name="commandAction">A function that takes a command parameter and returns a task representing the asynchronous operation. Cannot be
    /// null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResult with the unique
    /// identifier returned by the command action.</returns>
    public static async Task<ServiceResult<Guid>> ExecuteAsync(this ICommandParameter command, Func<ICommandParameter, Task<ServiceResult<Guid>>> commandAction)
        => await commandAction(command);
    

    /// <summary>
    /// Executes a synchronous command action using the provided command and returns the resulting service result.
    /// </summary>
    /// <typeparam name="TEntityId">Type of the actor entity id.</typeparam>
    /// <param name="command">Command to execute.</param>
    /// <param name="commandAction">Synchronous action that performs the command execution.</param>
    /// <returns>The <see cref="ServiceResult{Guid}"/> returned by the action.</returns>
    public static ServiceResult<Guid> Execute<TEntityId>(this ICommand<TEntityId> command, Func<ICommand, ServiceResult<Guid>> commandAction) 
        where TEntityId : IActorEntityId
    {
        return commandAction(command);
    }

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
            await message.ReplyAsync(data, serializer: MsgSerializer);
        }
    }

    /// <summary>
    /// Extracts only the verb segment from a subject string without allocating an <see cref="ActorSubject"/>.
    /// Expected subject format: 'ActorType.Name.Verb.EntityId'.
    /// </summary>
    /// <param name="subject">Subject string to extract the verb from.</param>
    /// <returns>The verb segment as a string.</returns>
    public static string ExtractVerb(this string subject)
    {
        var span = subject.AsSpan();
        int dot1 = span.IndexOf('.');
        int dot2 = span[(dot1 + 1)..].IndexOf('.') + dot1 + 1;
        int dot3 = span[(dot2 + 1)..].IndexOf('.') + dot2 + 1;
        return subject.Substring(dot2 + 1, dot3 - dot2 - 1);
    }

    /// <summary>
    /// Parses a subject string into an <see cref="ActorSubject"/> instance.
    /// Expected subject format: 'ActorType.Name.Verb.EntityId' where parts are delimited by '.'.
    /// </summary>
    /// <remarks>Uses <see cref="ReadOnlySpan{T}"/> index scanning to avoid the heap allocations
    /// that <see cref="string.Split(char)"/> and <see cref="string.Join(char, string[])"/> would incur.</remarks>
    /// <param name="subject">Subject string to parse.</param>
    /// <returns>Parsed <see cref="ActorSubject"/> representing the subject.</returns>
    /// <exception cref="ArgumentException">Thrown when the subject is null/whitespace or not in the expected format.</exception>
    public static ActorSubject ToSubject(this string subject)
    {
        if (string.IsNullOrEmpty(subject))
            throw new ArgumentException("Value cannot be null or empty.", nameof(subject));

        if (_subjectMap.TryGetValue(subject, out var cached))
            return cached;

        var parsed = ParseSubject(subject);

        if (_subjectMapCount < MaxSubjectCacheSize && _subjectMap.TryAdd(subject, parsed))
            Interlocked.Increment(ref _subjectMapCount);

        return parsed;
    }

    /// <summary>
    /// Parses a subject string in the format 'ActorType.Name.Verb.EntityId' and returns an ActorSubject instance
    /// containing the extracted components.
    /// </summary>
    /// <remarks>Ensure that the input string strictly follows the expected format to avoid exceptions. This
    /// method does not perform additional validation beyond checking the presence of three dots.</remarks>
    /// <param name="subject">The subject string to parse. Must be in the format 'ActorType.Name.Verb.EntityId' and cannot be null or empty.</param>
    /// <returns>An ActorSubject instance representing the actor type, name, verb, and entity ID parsed from the subject string.</returns>
    /// <exception cref="ArgumentException">Thrown if the subject string does not contain exactly three dots, indicating an invalid format.</exception>
    static ActorSubject ParseSubject(string subject)
    {
        var span = subject.AsSpan();

        // Find first dot: ActorType
        int dot1 = span.IndexOf('.');
        if (dot1 < 0) throw new ArgumentException($"Invalid subject format. Expected format: 'ActorType.Name.Verb.EntityId'. Actual: '{subject}'", nameof(subject));

        // Find second dot: Name
        int dot2 = span[(dot1 + 1)..].IndexOf('.');
        if (dot2 < 0) throw new ArgumentException($"Invalid subject format. Expected format: 'ActorType.Name.Verb.EntityId'. Actual: '{subject}'", nameof(subject));
        dot2 += dot1 + 1;

        // Find third dot: Verb
        int dot3 = span[(dot2 + 1)..].IndexOf('.');
        if (dot3 < 0) throw new ArgumentException($"Invalid subject format. Expected format: 'ActorType.Name.Verb.EntityId'. Actual: '{subject}'", nameof(subject));
        dot3 += dot2 + 1;

        var actorType = ActorTypeExtensions.ParseActorTypeFast(span[..dot1]);

        // Slice the original string to avoid allocating new strings when the span matches a substring.
        var actorName = subject.Substring(dot1 + 1, dot2 - dot1 - 1);
        var verb = subject.Substring(dot2 + 1, dot3 - dot2 - 1);
        var entityId = subject[(dot3 + 1)..];

        return new ActorSubject(actorType, actorName, verb, entityId);
    }


}
