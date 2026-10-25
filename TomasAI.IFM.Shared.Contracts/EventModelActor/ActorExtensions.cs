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
    const int SubjectCacheGenerationSize = 2048;
    static ConcurrentDictionary<string, ActorSubject> _currentSubjectMap = [];
    static ConcurrentDictionary<string, ActorSubject> _previousSubjectMap = [];
    static int _currentSubjectMapCount;
    static int _subjectMapRotation;

    /// <summary>
    /// Serializer used to convert objects to and from binary payloads for messaging.
    /// Must be initialized before calling serialization extension methods.
    /// </summary>
    public static IDataSerializer DataSerializer { get; set; }


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
        
    {
        ArgumentNullException.ThrowIfNull(@event);
        return @event.RoutedFrom(@event.CommandId, @event.AggregateId, @event.EventSource);
    }

    public static IEvent RoutedFrom(this IEvent @event, ICommand command)
        
    {
        ArgumentNullException.ThrowIfNull(command);
        return @event.RoutedFrom(command.CommandId, command.EventSource);
    }

    /// <summary>
    /// Casts the current command interface to the concrete command type.
    /// </summary>
    /// <typeparam name="TCommand">Target concrete command type.</typeparam>
    /// <typeparam name="TEntityId">Type of the actor entity id.</typeparam>
    /// <param name="command">Command to cast.</param>
    /// <returns>Command cast to <typeparamref name="TCommand"/>.</returns>
    public static TCommand ToCommand<TCommand, TEntityId>(this ICommand<TEntityId> command)
        where TEntityId : IActorEntityId
        where TCommand : class, ICommand<TEntityId>
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
    public static Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(this ICommand<TEntityId> command, Func<ICommand, Task<ServiceResult<Guid>>> commandAction)
        where TEntityId : IActorEntityId
        
    {
        ArgumentNullException.ThrowIfNull(commandAction);
        return commandAction(command);
    }

    /// <summary>
    /// Executes the specified asynchronous command action using the provided command parameter.
    /// </summary>
    /// <param name="command">The command parameter to pass to the command action. Cannot be null.</param>
    /// <param name="commandAction">A function that takes a command parameter and returns a task representing the asynchronous operation. Cannot be
    /// null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResult with the unique
    /// identifier returned by the command action.</returns>
    public static Task<ServiceResult<Guid>> ExecuteAsync(this ICommandParameter command, Func<ICommandParameter, Task<ServiceResult<Guid>>> commandAction)
        
    {
        ArgumentNullException.ThrowIfNull(commandAction);
        return commandAction(command);
    }


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
        ArgumentNullException.ThrowIfNull(commandAction);
        return commandAction(command);
    }

    /// <summary>
    /// Extracts only the verb segment from a subject string without allocating an <see cref="ActorSubject"/>.
    /// Expected subject format: 'ActorType.Name.Verb.EntityId'.
    /// </summary>
    /// <param name="subject">Subject string to extract the verb from.</param>
    /// <returns>The verb segment as a string.</returns>
    public static string ExtractVerb(this string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
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

        var current = Volatile.Read(ref _currentSubjectMap);
        if (current.TryGetValue(subject, out var cached)
            || Volatile.Read(ref _previousSubjectMap).TryGetValue(subject, out cached))
            return cached;

        var parsed = ParseSubject(subject);

        if (current.TryAdd(subject, parsed)
            && ReferenceEquals(current, Volatile.Read(ref _currentSubjectMap))
            && Interlocked.Increment(ref _currentSubjectMapCount) >= SubjectCacheGenerationSize)
        {
            RotateSubjectCache(current);
        }

        return parsed;
    }

    static void RotateSubjectCache(ConcurrentDictionary<string, ActorSubject> generation)
    {
        if (Interlocked.CompareExchange(ref _subjectMapRotation, 1, 0) != 0)
            return;

        try
        {
            if (!ReferenceEquals(generation, Volatile.Read(ref _currentSubjectMap))
                || Volatile.Read(ref _currentSubjectMapCount) < SubjectCacheGenerationSize)
            {
                return;
            }

            Volatile.Write(ref _previousSubjectMap, generation);
            Volatile.Write(ref _currentSubjectMap, new ConcurrentDictionary<string, ActorSubject>(StringComparer.Ordinal));
            Volatile.Write(ref _currentSubjectMapCount, 0);
        }
        finally
        {
            Volatile.Write(ref _subjectMapRotation, 0);
        }
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
