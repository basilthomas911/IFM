using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using System.Reflection;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Base implementation of a query-driven actor that processes messages asynchronously with mailbox-driven processing.
/// </summary>
/// <remarks>
/// Mirrors the pattern used by <see cref="BaseEventSourceCommandActor{TActor}"/> but targets the <see cref="IQueryActor{TActor}"/> contract.
/// Provides lifecycle hooks (startup/shutdown), message handling, validation, state load/save, and exception handling.
/// </remarks>
/// <typeparam name="TActor">The actor type implementing <see cref="IQueryActor{TActor}"/>.</typeparam>
/// <param name="actorContext">The closed-generic query context owned by the actor for its entire lifetime.</param>
/// <param name="logger">The logger used to record operational and diagnostic information.</param>
public abstract class BaseQueryActor<TActor>(
    IQueryActorContext<TActor> actorContext,
    ILogger logger)
    : IQueryActor<TActor> where TActor : IActor
{
    const int InvalidQueryMessageErrorCode = 9998;
    const int UnsupportedQueryTypeErrorCode = 9999;

    /// <summary>Handles a failed, already-materialized query with its exact result contract.</summary>
    protected delegate ValueTask QueryExceptionHandler(
        IQueryActorContext<TActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception);

    readonly IQueryActorContext<TActor> _context = IsArgumentNull.Set(actorContext);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorContext).ActorId;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    string _serviceId = string.Empty;

    IActorSupervisor _supervisor;
    int _lifecycle;

    // IActor properties
    public ActorMailboxId Id => _actorId;
    /// <summary>
    /// Gets the closed-generic query context retained for the lifetime of this actor.
    /// </summary>
    protected IQueryActorContext<TActor> Context => _context;
    protected IQuery Query { get; set; }
    protected ILogger Logger => _logger;

    public IActorMailbox Mailbox { get; private set; }
    public bool IsRunning
    {
        get => Volatile.Read(ref _lifecycle) == 2;
        protected set => Volatile.Write(ref _lifecycle, value ? 2 : 0);
    }
    public bool IsParent { get; protected set; }

    /// <summary>
    /// Asynchronously starts the actor and its associated components, including the mailbox, producer, and consumer.
    /// </summary>
    /// <remarks>This method initializes the actor's mailbox, starts the producer and consumer processes, and
    /// sets up the actor's command context. If the actor is already running, the method exits without performing any
    /// actions.</remarks>
    /// <param name="supervisor">The <see cref="IActorSupervisor"/> responsible for managing the actor's lifecycle and providing necessary
    /// resources.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mailbox is not set before starting the actor.</exception>
    public async ValueTask StartAsync(IActorSupervisor supervisor)
        => await StartAsync(supervisor, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(supervisor);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0)
            return;
        IActorProducer? producer = null;
        try
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(_actorId);
            producer = supervisor.GetProducer(_actorId);
            await producer.StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
            _serviceId = typeof(TActor).Name;
            _logger.LogInformationEvent(_serviceId, "Started {MailboxId} producer.", _actorId);
            await OnStartup(_context, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lifecycle, 2);
        }
        catch
        {
            if (producer is not null)
            {
                try { await producer.StopAsync().ConfigureAwait(false); }
                catch (Exception cleanupException) { _logger.LogError(cleanupException, "Failed to roll back {MailboxId} producer startup.", _actorId); }
            }
            Volatile.Write(ref _lifecycle, 0);
            throw;
        }
    }

    /// <summary>
    /// Stops the actor and releases associated resources asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the actor is properly shut down by invoking the shutdown logic and
    /// stopping any associated consumer or producer components, if they are present. If the actor is not running, the
    /// method returns immediately without performing any operations.</remarks>
    /// <param name="context">The context in which the actor is operating. This parameter provides access to actor-specific state and
    /// services.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous stop operation.</returns>
    public async ValueTask StopAsync()
        => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 3, 2) != 2)
            return;
        try
        {
            var producer = _supervisor.GetProducer(_actorId);
            // Once shutdown owns the actor lifecycle transition, finish cleanup atomically.
            await producer.StopAsync().ConfigureAwait(false);
            _logger.LogInformationEvent(_serviceId, "Stopped {MailboxId} producer.", _actorId);
        }
        finally
        {
            try
            {
                await OnShutdown(_context!).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _lifecycle, 0);
            }
        }
    }

    /// <summary>
    /// Handles an incoming message for the actor, performing validation and message processing.
    /// </summary>
    /// <remarks>This method validates the message to ensure it is intended for the current actor and processes
    /// the message. If an exception occurs during processing, it is handled by invoking the exception handler.</remarks>
    /// <param name="message">The message to be processed, containing the subject and entity information.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not intended for the current actor or if the thread ID is invalid.</exception>
    public ValueTask HandleMessageAsync(IActorMessage message)
        => HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);

    /// <summary>
    /// Handles an incoming message using a pre-resolved thread identifier, avoiding redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="threadId">The pre-resolved thread identifier from the caller.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => await HandleMessageAsync(message, threadId, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        IQuery? query = null;
        var verb = message.Subject.Verb;
        var activeStage = ActorRuntimeMetrics.ValidationStage;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                query = ParseMessage(_context!, message)
                    ?? throw new InvalidOperationException(
                        $"The {Id.Name} query parser returned no query.");
            }
            finally
            {
                // Query execution and reply metadata no longer need the serialized request.
                message.ReleasePayload();
            }

            /// check if the message is a command and validate it
            cancellationToken.ThrowIfCancellationRequested();
            activeStage = ActorRuntimeMetrics.ValidationStage;
            var stageStarted = ActorRuntimeMetrics.StartStage();
            try
            {
                if (cancellationToken.CanBeCanceled)
                    await OnValidateAsync(_context!, query, cancellationToken).ConfigureAwait(false);
                else
                    await OnValidateAsync(_context!, query).ConfigureAwait(false);
            }
            finally
            {
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Query);
            }

            /// process the message
            cancellationToken.ThrowIfCancellationRequested();
            activeStage = ActorRuntimeMetrics.ExecutionStage;
            stageStarted = ActorRuntimeMetrics.StartStage();
            try
            {
                if (cancellationToken.CanBeCanceled)
                    await ReceiveAsync(_context!, query, cancellationToken).ConfigureAwait(false);
                else
                    await ReceiveAsync(_context!, query).ConfigureAwait(false);
            }
            finally
            {
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Query);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActorRuntimeMetrics.RecordStageFailure(activeStage, ActorType.Query);
            if (query is null)
                await HandleQueryParseFailureAsync(message, ex).ConfigureAwait(false);
            else
                await OnExceptionAsync(_context!, threadId, query, verb, ex).ConfigureAwait(false);
        }
        finally
        {
            // ReplyAsync normally removes this entry. This terminal cleanup also
            // covers handlers that throw, time out, or forget to reply.
            _context!.RemoveMessageInfo(threadId, verb);
        }
    }

    // Explicit interface implementations forwarding to protected hooks
    ValueTask IQueryActor<TActor>.OnStartup(IQueryActorContext<TActor> context) => OnStartup(context);
    ValueTask IQueryActor<TActor>.OnShutdown(IQueryActorContext<TActor> context) => OnShutdown(context);
    ValueTask IQueryActor<TActor>.ReceiveAsync(IQueryActorContext<TActor> context, IQuery query) => ReceiveAsync(context, query);
    ValueTask IQueryActor<TActor>.OnValidateAsync(IQueryActorContext<TActor> context, IQuery query) => OnValidateAsync(context, query);
    ValueTask IQueryActor<TActor>.OnExceptionAsync(IQueryActorContext<TActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => OnExceptionAsync(context, threadId, query, verb, ex);

    // Protected hooks for derived classes
    protected abstract IQuery ParseMessage(IQueryActorContext<TActor> context, IActorMessage message);

    /// <summary>
    /// Resolves a query parser from an actor-owned verb map, materializes the query, and registers
    /// the request correlation needed by <see cref="IQueryActorContext.ReplyAsync{TResult}"/>.
    /// </summary>
    protected IQuery ParseMappedQuery(
        IQueryActorContext<TActor> context,
        IActorMessage message,
        IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> parseMap)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(parseMap);

        var subject = message.Subject;
        if (subject.ActorType != ActorType.Query
            || !string.Equals(subject.Name, Id.Name, StringComparison.Ordinal)
            || !parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException(
                $"Unable to resolve {Id.Name} query from message: {subject}");

        IQuery? query;
        try
        {
            query = parser(message);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Unable to deserialize {Id.Name}.{subject.Verb} query.", exception);
        }

        if (query is null)
            throw new InvalidOperationException(
                $"Parser for {Id.Name}.{subject.Verb} returned no query.");

        context.SetMessageInfo(
            subject.ThreadId,
            subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>Resolves a query receive handler by the query's exact concrete CLR type.</summary>
    protected THandler ResolveMappedQueryHandler<THandler>(
        IQuery query,
        IReadOnlyDictionary<Type, THandler> receiveMap)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(receiveMap);

        if (!receiveMap.TryGetValue(query.GetType(), out var handler))
            throw new InvalidOperationException(
                $"Unable to process {Id.Name} query: {query.GetType().Name}");

        return handler;
    }

    /// <summary>Creates an exact-result failure handler for a mapped query.</summary>
    protected static QueryExceptionHandler QueryException<TResult>() where TResult : class
        => static (context, threadId, query, verb, exception) =>
            context.ReplyAsync(
                threadId,
                verb,
                new ServiceFailed<TResult>(query.ErrorCode, exception.Message));

    /// <summary>Creates an exact-result failure handler with actor-specific error-code selection.</summary>
    protected static QueryExceptionHandler QueryException<TResult>(
        Func<IQuery, Exception, int> errorCodeSelector)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(errorCodeSelector);
        return (context, threadId, query, verb, exception) =>
            context.ReplyAsync(
                threadId,
                verb,
                new ServiceFailed<TResult>(errorCodeSelector(query, exception), exception.Message));
    }

    /// <summary>
    /// Builds an exception map from exact query types by reading each query's single
    /// <see cref="IQuery{TResult}"/> result contract.
    /// </summary>
    protected static IReadOnlyDictionary<Type, QueryExceptionHandler> CreateQueryExceptionMap(
        IEnumerable<Type> queryTypes)
        => CreateQueryExceptionMap(queryTypes, static (query, _) => query.ErrorCode);

    /// <summary>Builds an exact-type exception map with actor-specific error-code selection.</summary>
    protected static IReadOnlyDictionary<Type, QueryExceptionHandler> CreateQueryExceptionMap(
        IEnumerable<Type> queryTypes,
        Func<IQuery, Exception, int> errorCodeSelector)
    {
        ArgumentNullException.ThrowIfNull(queryTypes);
        ArgumentNullException.ThrowIfNull(errorCodeSelector);
        var factory = typeof(BaseQueryActor<TActor>).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(method =>
                method.Name == nameof(CreateQueryExceptionHandler)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("Unable to resolve the query exception-handler factory.");
        var result = new Dictionary<Type, QueryExceptionHandler>();
        foreach (var queryType in queryTypes)
        {
            var contract = queryType.GetInterfaces().SingleOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQuery<>))
                ?? throw new InvalidOperationException(
                    $"{queryType.FullName} must implement exactly one IQuery<TResult> contract.");
            var handler = (QueryExceptionHandler)factory
                .MakeGenericMethod(contract.GetGenericArguments()[0])
                .Invoke(null, [errorCodeSelector])!;
            result.Add(queryType, handler);
        }
        return result;
    }

    static QueryExceptionHandler CreateQueryExceptionHandler<TResult>(
        Func<IQuery, Exception, int> errorCodeSelector)
        where TResult : class
        => QueryException<TResult>(errorCodeSelector);

    /// <summary>Dispatches execution failures through an actor-owned exact-type exception map.</summary>
    protected async ValueTask ExceptionMappedQueryAsync(
        IQueryActorContext<TActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception,
        IReadOnlyDictionary<Type, QueryExceptionHandler> exceptionMap)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        ArgumentNullException.ThrowIfNull(query);
        IsArgumentNull.Check(verb);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(exceptionMap);

        if (!exceptionMap.TryGetValue(query.GetType(), out var handler))
        {
            _logger.LogError(
                exception,
                "No query exception handler is registered for {ActorId}.{QueryType}.",
                Id,
                query.GetType().FullName);
            try
            {
                await context.ReplyAsync(
                    threadId,
                    verb,
                    new ServiceFailed<object>(UnsupportedQueryTypeErrorCode, exception.Message)).ConfigureAwait(false);
            }
            catch (Exception replyException)
            {
                _logger.LogError(
                    replyException,
                    "Unable to return fallback query failure for {ActorId}.{QueryType}.",
                    Id,
                    query.GetType().FullName);
            }
            return;
        }

        try
        {
            await handler(context, threadId, query, verb, exception).ConfigureAwait(false);
        }
        catch (Exception replyException)
        {
            _logger.LogError(
                replyException,
                "Failed to return query failure for {ActorId}, thread {ThreadId}, verb {Verb}. Original failure: {OriginalFailure}",
                Id,
                threadId,
                verb,
                exception.Message);
        }
    }

    async ValueTask HandleQueryParseFailureAsync(IActorMessage message, Exception exception)
    {
        _logger.LogError(
            exception,
            "Unable to parse query message for {ActorId}: {Subject}.",
            Id,
            message.Subject);
        if (!message.CanReply)
            return;
        try
        {
            await message.ReplyAsync(
                new ServiceFailed<object>(
                    InvalidQueryMessageErrorCode,
                    "The query request could not be parsed.")).ConfigureAwait(false);
        }
        catch (Exception replyException)
        {
            _logger.LogError(
                replyException,
                "Unable to return query parsing failure for {Subject}.",
                message.Subject);
        }
    }

    /// <summary>
    /// Compatibility entry point for existing query actor tests during the staged mailbox migration.
    /// Runtime query ingress uses <see cref="IActorMessage"/> directly.
    /// </summary>
    protected IQuery ParseMessage(IQueryActorContext<TActor> context, in NatsMsg<byte[]> message)
        => ParseMessage(context, new LegacyNatsActorMessage(message));
    protected virtual ValueTask OnStartup(IQueryActorContext<TActor> context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnStartup(
        IQueryActorContext<TActor> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnStartup(context);
    }
    protected virtual ValueTask OnShutdown(IQueryActorContext<TActor> context) => ValueTask.CompletedTask;
    protected abstract ValueTask ReceiveAsync(IQueryActorContext<TActor> context, IQuery query);
    protected virtual ValueTask ReceiveAsync(IQueryActorContext<TActor> context, IQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReceiveAsync(context, query);
    }
    protected virtual ValueTask OnValidateAsync(IQueryActorContext<TActor> context, IQuery query) => ValueTask.CompletedTask;
    protected virtual ValueTask OnValidateAsync(IQueryActorContext<TActor> context, IQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnValidateAsync(context, query);
    }
    protected abstract  ValueTask OnExceptionAsync(IQueryActorContext<TActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex);
}
