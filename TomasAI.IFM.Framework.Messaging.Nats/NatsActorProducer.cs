using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Provides functionality for producing and managing messages in a NATS-based actor system.
/// </summary>
/// <remarks>This class is responsible for sending commands, events, and queries to actor subjects using NATS
/// messaging. It supports asynchronous operations for starting and stopping the producer, as well as for publishing and
/// requesting messages. Ensure that the producer is started by calling <see cref="StartAsync"/> before invoking any
/// messaging methods.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class NatsActorProducer(
    INatsProducerOptions options,
    ILogger logger,
    NatsConnectionManager? connectionManager = null)
    : IActorProducer, IAsyncDisposable
{
    readonly INatsProducerOptions _options = IsArgumentNull.Set(options);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly NatsConnectionManager _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    int _disposeStarted;

    /// <summary>Stops owned transport activity before releasing the host lifetime gate.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposeStarted, 1);
                _disposeTask = StopAsync().AsTask();
            }
            GC.SuppressFinalize(this);
            return new ValueTask(_disposeTask);
        }
    }

    readonly object _disposeGate = new();
    Task? _disposeTask;
    // Supervisors may stop a producer after container disposal. As with consumers,
    // retain this managed gate until collection; no WaitHandle is allocated.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2213", Justification = "Late supervisor stops must retain the managed lifecycle gate, which has no allocated WaitHandle.")]
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    NatsClient? _nc;
    CancellationTokenSource? _operationStopping;
    ActorMailboxId _actorId;
    bool _isRunning;

    public bool IsRunning => Volatile.Read(ref _isRunning);

    /// <summary>
    /// Initializes and starts the NATS producer for the specified mailbox.
    /// </summary>
    /// <remarks>This method establishes a connection to the NATS server and creates or updates a stream
    /// for the specified mailbox. If the connection is already established, the method does nothing.</remarks>
    /// <param name="mailboxId">The unique identifier of the mailbox, including its name and actor type. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask StartAsync(ActorMailboxId mailboxId)
        => await StartAsync(mailboxId, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken)
    {
        try
        {
            IsArgumentNull.Check(mailboxId);
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
                if (_isRunning)
                    return;
                _nc = await _connectionManager.GetClientAsync(_options.Url, cancellationToken).ConfigureAwait(false);
                _operationStopping?.Dispose();
                _operationStopping = new CancellationTokenSource();
                _actorId = mailboxId;
                Volatile.Write(ref _isRunning, true);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write1(_logger, ex, nameof(NatsActorProducer), nameof(StartAsync), mailboxId);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously stops the NATS producer and releases associated resources.
    /// </summary>
    /// <remarks>This method disposes of the underlying connection and sets internal references to null.  If
    /// an error occurs during the disposal process, the exception is logged and rethrown.</remarks>
    /// <returns></returns>
    public async ValueTask StopAsync()
        => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!IsRunning)
                    return;
                Volatile.Write(ref _isRunning, false);
                var operationStopping = _operationStopping;
                _operationStopping = null;
                if (operationStopping is not null)
                {
                    await operationStopping.CancelAsync().ConfigureAwait(false);
                    operationStopping.Dispose();
                }
                if (_ownsConnectionManager)
                    await _connectionManager.DisposeAsync().ConfigureAwait(false);
                _nc = null;
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write2(_logger, null, nameof(NatsActorProducer), nameof(StopAsync));
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write3(_logger, ex, nameof(NatsActorProducer), nameof(StopAsync));
            throw;
        }
    }

    /// <summary>
    /// Asynchronously sends a command to the specified actor subject, ensuring the actor is running before publishing
    /// the command.
    /// </summary>
    /// <remarks>If the actor is not already running, this method will start it before publishing the command.
    /// Exceptions encountered during publishing are logged and rethrown.</remarks>
    /// <typeparam name="TCommand">The type of the command to send. Must be a class implementing the ICommand interface for the specified entity
    /// ID.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command. Must implement the IActorEntityId interface.</typeparam>
    /// <param name="subject">The actor subject to which the command will be published. Cannot be null.</param>
    /// <param name="command">The command instance to send. Cannot be null and must match the type specified by TCommand.</param>
    /// <param name="entityId">The identifier of the entity associated with the command. Cannot be null and must match the type specified by
    /// TEntityId.</param>
    /// <returns>A ValueTask representing the asynchronous operation of sending the command.</returns>
    public async ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => await SendAsync(subject, command, entityId, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SendAsync<TCommand, TEntityId>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureCoreSubject(subject, ActorType.Command);
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(command);
            IsArgumentNull.Check(entityId);
            if (!IsRunning)
                await StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
            await PublishAsync(subject.ToString(), command.ToCommand<TCommand, TEntityId>(), cancellationToken).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write4(_logger, null, nameof(NatsActorProducer), nameof(SendAsync), subject, command.CommandId);
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write5(_logger, ex, nameof(NatsActorProducer), nameof(SendAsync), subject);
            throw;
        }
    }

    /// <summary>
    /// Publishes an event to the specified subject asynchronously.
    /// </summary>
    /// <remarks>This method requires the NATS producer to be started before it can publish messages.  If the
    /// producer is not started, an <see cref="InvalidOperationException"/> is thrown.</remarks>
    /// <param name="subject">The subject to which the event will be published. Cannot be <see langword="null"/>.</param>
    /// <param name="event">The event to publish. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the NATS producer is not started.</exception>
    public async ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => await SendAsync<TEvent, TEntityId>(subject, @event, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SendAsync<TEvent, TEntityId>(
        ActorSubject subject,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(@event);
        subject.ActorType.EnsureDeliveryType(ActorDeliveryType.NatsCore, nameof(NatsActorProducer));
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(@event);
            if (!IsRunning)
                await StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
            await PublishAsync(subject.ToString(), @event.ToEvent<TEvent>(), cancellationToken).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write6(_logger, null, nameof(NatsActorProducer), nameof(SendAsync), subject, @event.CommandId);
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write7(_logger, ex, nameof(NatsActorProducer), nameof(SendAsync), subject);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously publishes a denormalizer event to the specified actor subject.
    /// </summary>
    /// <remarks>If the actor is not running, it will be started before publishing the event. This method logs
    /// debug information on successful publish and logs errors if the operation fails.</remarks>
    /// <typeparam name="TEvent">The type of the event to publish.</typeparam>
    /// <param name="subject">The actor subject to which the event will be published. If <see langword="null"/>, the event will not be
    /// published.</param>
    /// <param name="event">The denormalizer event to publish. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous publish operation.</returns>
    public async ValueTask SendAsync<TEvent>(ActorSubject subject, TEvent @event)
        where TEvent : class
    {
        subject.ActorType.EnsureDeliveryType(ActorDeliveryType.NatsCore, nameof(NatsActorProducer));
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(@event);
            if (!IsRunning)
                await StartAsync(_actorId).ConfigureAwait(false);
            await PublishAsync(subject.ToString(), @event).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write8(_logger, null, nameof(NatsActorProducer), nameof(SendAsync), subject);
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write9(_logger, ex, nameof(NatsActorProducer), nameof(SendAsync), subject);
            throw;
        }
    }

    /// <summary>
    /// Sends a query to the specified actor subject and asynchronously retrieves the result.
    /// </summary>
    /// <remarks>This method uses NATS (a messaging system) to send the query and receive the response. Ensure
    /// that the NATS producer is started by calling the appropriate initialization method before invoking this
    /// method.</remarks>
    /// <typeparam name="TResult">The type of the result expected from the query.</typeparam>
    /// <param name="subject">The actor subject to which the query is sent. Cannot be <see langword="null"/>.</param>
    /// <param name="query">The query to be sent. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The task result contains the
    /// response of type <typeparamref name="TResult"/> returned by the actor subject.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the NATS producer is not started before calling this method.</exception>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => await RequestAsync<TResult, TQuery>(subject, query, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
        ActorSubject subject,
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureCoreSubject(subject, ActorType.Query);
        ServiceResult<TResult> result;
        var started = NatsMessagingMetrics.StartOperation();
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(query);
            if (!IsRunning)
                await StartAsync(_actorId, cancellationToken).ConfigureAwait(false);

            using var operationCancellation = CreateOperationCancellation(cancellationToken);

            // Read the typed reply directly from the receive buffer.
            TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.ThrowIfRetired();
            var reply = await _nc!.RequestAsync<TQuery, ServiceResult<TResult>>(
                subject.ToString(),
                 query,
                 headers: ActorTrace.Headers(),
                 requestSerializer: NatsMessagePackSerializer<TQuery>.Default,
                 replySerializer: NatsMessagePackSerializer<ServiceResult<TResult>>.Default,
                 cancellationToken: operationCancellation.Token)
                .ConfigureAwait(false);
            result = IsArgumentNull.Set(reply.Data);
            NatsMessagingMetrics.Published.Add(1);
            if (_logger.IsEnabled(LogLevel.Debug))
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write10(_logger, null, nameof(NatsActorProducer), nameof(RequestAsync), subject, query.GetType().Name);
        }
        catch (Exception ex)
        {
            NatsMessagingMetrics.RecordOperationFailure(NatsMessagingMetrics.CoreRequestOperation);
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write11(_logger, ex, nameof(NatsActorProducer), nameof(RequestAsync), subject);
            throw;
        }
        finally
        {
            NatsMessagingMetrics.RecordOperation(started, NatsMessagingMetrics.CoreRequestOperation);
        }
        return result!;
    }

    /// <summary>
    /// Sends a command request to the specified actor subject and returns the result.
    /// </summary>
    /// <remarks>This method ensures that the actor is running before sending the request. If the actor is not
    /// running, it will be started automatically. The command is serialized and sent to the specified subject, and the
    /// response is deserialized into a <see cref="ServiceResult{T}"/>.</remarks>
    /// <typeparam name="TEntityId">The type of the entity identifier, which must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="subject">The actor subject to which the command is sent. Cannot be <see langword="null"/>.</param>
    /// <param name="command">The command to be executed. Cannot be <see langword="null"/>.</param>
    /// <param name="entityId">The identifier of the entity associated with the command. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="ServiceResult{T}"/> with a <see cref="Guid"/> representing the outcome of the command execution.</returns>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
        => await RequestAsync<TCommand, TEntityId, TResult>(subject, command, entityId, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureCoreSubject(subject, ActorType.Command);
        ServiceResult<TResult> result;
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(command);
            IsArgumentNull.Check(entityId);
            if (!IsRunning)
                await StartAsync(_actorId, cancellationToken).ConfigureAwait(false);

            result = await RequestAsync<TCommand, TResult>(
                subject.ToString(),
                command.ToCommand<TCommand, TEntityId>(),
                cancellationToken).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write12(_logger, null, nameof(NatsActorProducer), nameof(RequestAsync), subject, command.CommandId);
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write13(_logger, ex, nameof(NatsActorProducer), nameof(RequestAsync), subject);
            throw;
        }
        return result!;
    }

    /// <inheritdoc />
    public async ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
    {
        EnsureCoreSubject(subject, ActorType.Function);
        IsArgumentNull.Check(command);
        IsArgumentNull.Check(entityId);
        if (!IsRunning)
            await StartAsync(_actorId, cancellationToken).ConfigureAwait(false);

        return await RequestAsync<TCommand, TResult>(
            subject.ToString(),
            command.ToCommand<TCommand, TEntityId>(),
            cancellationToken).ConfigureAwait(false);
    }

    async ValueTask PublishAsync<T>(string subject, T message, CancellationToken cancellationToken = default)
    {
        var started = NatsMessagingMetrics.StartOperation();
        try
        {
            TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.ThrowIfRetired();
            await _nc!.PublishAsync(
                subject,
                message,
                headers: RealtimeSourceGenerationHeaders.Add(ActorTrace.Headers(), message!),
                serializer: NatsMessagePackSerializer<T>.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            NatsMessagingMetrics.Published.Add(1);
        }
        catch
        {
            NatsMessagingMetrics.RecordOperationFailure(NatsMessagingMetrics.CorePublishOperation);
            throw;
        }
        finally
        {
            NatsMessagingMetrics.RecordOperation(started, NatsMessagingMetrics.CorePublishOperation);
        }
    }

    async ValueTask<ServiceResult<TResult>> RequestAsync<TRequest, TResult>(
        string subject,
        TRequest message,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        var started = NatsMessagingMetrics.StartOperation();
        try
        {
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var reply = await _nc!.RequestAsync<TRequest, ServiceResult<TResult>>(
                subject,
                message,
                headers: ActorTrace.Headers(),
                requestSerializer: NatsMessagePackSerializer<TRequest>.Default,
                replySerializer: NatsMessagePackSerializer<ServiceResult<TResult>>.Default,
                cancellationToken: operationCancellation.Token).ConfigureAwait(false);
            NatsMessagingMetrics.Published.Add(1);
            return IsArgumentNull.Set(reply.Data);
        }
        catch
        {
            NatsMessagingMetrics.RecordOperationFailure(NatsMessagingMetrics.CoreRequestOperation);
            throw;
        }
        finally
        {
            NatsMessagingMetrics.RecordOperation(started, NatsMessagingMetrics.CoreRequestOperation);
        }
    }

    // Borrow the producer's shutdown token when no caller token needs linking. Disposing this scope
    // releases only its own linked source; the producer owns the shutdown source across requests.
    readonly struct OperationCancellation(CancellationTokenSource? ownedSource, CancellationToken token) : IDisposable
    {
        public CancellationToken Token { get; } = token;
        public void Dispose() => ownedSource?.Dispose();
    }

    OperationCancellation CreateOperationCancellation(CancellationToken cancellationToken)
    {
        var operationStopping = _operationStopping
            ?? throw new InvalidOperationException("The NATS actor producer is not running.");
        var stoppingToken = operationStopping.Token;
        if (!cancellationToken.CanBeCanceled)
            return new OperationCancellation(null, stoppingToken);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stoppingToken);
        return new OperationCancellation(linked, linked.Token);
    }

    static void EnsureCoreSubject(ActorSubject subject, ActorType expectedActorType)
    {
        subject.ActorType.EnsureDeliveryType(ActorDeliveryType.NatsCore, nameof(NatsActorProducer));
        if (subject.ActorType != expectedActorType)
        {
            throw new InvalidOperationException(
                $"A {expectedActorType} message cannot be published to actor type '{subject.ActorType}'.");
        }
    }
}

