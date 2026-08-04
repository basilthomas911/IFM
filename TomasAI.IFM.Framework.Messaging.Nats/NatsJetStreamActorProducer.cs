using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Provides functionality for producing actor command and event messages using the NATS JetStream API
/// with durable, at-least-once delivery guarantees.
/// </summary>
/// <remarks>
/// This class is responsible for publishing commands and events to actor subjects using NATS JetStream.
/// Unlike <see cref="NatsActorProducer"/>, this producer uses JetStream for durable message delivery
/// and verifies acknowledgement from the server after each publish. It is designed exclusively for
/// fire-and-forget command and event messages; queries and request-reply patterns are not supported.
/// <para>
/// The producer receives a pre-configured <see cref="INatsJSContext"/> via options, allowing the caller
/// to manage the NATS connection and JetStream context lifecycle externally.
/// </para>
/// </remarks>
/// <param name="options">The NATS JetStream producer options containing the JetStream context and subject prefix.</param>
/// <param name="logger">The logger instance used to record diagnostic and lifecycle events.</param>
public class NatsJetStreamActorProducer(
    INatsJetStreamProducerOptions options,
    ILogger<NatsJetStreamActorProducer> logger,
    NatsConnectionManager? connectionManager = null)
    : IJSActorProducer
{
    readonly INatsJetStreamProducerOptions _options = IsArgumentNull.Set(options);
    readonly ILogger<NatsJetStreamActorProducer> _logger = IsArgumentNull.Set(logger);
    readonly NatsConnectionManager _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    NatsClient _nc;
    INatsJSContext _js;
    bool _isRunning;

    /// <summary>
    /// Gets a value indicating whether the NATS JetStream actor producer is currently active.
    /// </summary>
    public bool IsRunning => Volatile.Read(ref _isRunning);

    /// <summary>
    /// Starts the NATS JetStream producer for the specified mailbox.
    /// </summary>
    /// <remarks>
    /// Since the JetStream context is provided externally via options, this method simply marks
    /// the producer as running. If the producer is already started, the call is a no-op.
    /// </remarks>
    /// <param name="mailboxId">The unique identifier of the mailbox. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask StartAsync(ActorMailboxId mailboxId)
    {
        IsArgumentNull.Check(mailboxId);
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isRunning)
                return;
            _nc = await _connectionManager.GetClientAsync(_options.Url).ConfigureAwait(false);
            _js = await _connectionManager.GetJetStreamContextAsync(_options.Url).ConfigureAwait(false);
            Volatile.Write(ref _isRunning, true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Stops the NATS JetStream producer.
    /// </summary>
    /// <remarks>
    /// Marks the producer as stopped. The external JetStream context is not disposed by this method;
    /// its lifecycle is managed by the caller.
    /// </remarks>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isRunning)
                return;
            Volatile.Write(ref _isRunning, false);
            if (_ownsConnectionManager)
                await _connectionManager.DisposeAsync().ConfigureAwait(false);
            _nc = default!;
            _js = default!;
            _logger.LogInformation("NATS JetStream producer stopped.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Sends a command to the specified actor subject using JetStream publish with acknowledgement.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to send. Must implement <see cref="ICommand{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier. Must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="subject">The actor subject to which the command will be published. Cannot be <see langword="null"/>.</param>
    /// <param name="command">The command instance to send. Cannot be <see langword="null"/>.</param>
    /// <param name="entityId">The entity identifier associated with the command. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(command);
            IsArgumentNull.Check(entityId);
            await PublishAsync(subject.ToString(), command.ToCommand<TCommand, TEntityId>()).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Published command to JetStream subject {Subject} CommandId={CommandId}", subject, command.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command to JetStream subject {Subject}", subject);
            throw;
        }
    }

    /// <summary>
    /// Sends an event to the specified actor subject using JetStream publish with acknowledgement.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to send. Must implement <see cref="IEvent{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier. Must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="subject">The actor subject to which the event will be published. Cannot be <see langword="null"/>.</param>
    /// <param name="event">The event instance to send. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            IsArgumentNull.Check(subject);
            IsArgumentNull.Check(@event);
            await PublishAsync(subject.ToString(), @event.ToEvent<TEvent>()).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Published event to JetStream subject {Subject} CommandId={CommandId}", subject, @event.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to JetStream subject {Subject}", subject);
            throw;
        }
    }

    async ValueTask PublishAsync<T>(string subject, T message)
    {
        var acknowledgement = await _js.PublishAsync(
            subject,
            message,
            serializer: NatsMessagePackSerializer<T>.Default).ConfigureAwait(false);
        acknowledgement.EnsureSuccess();
        NatsMessagingMetrics.Published.Add(1);
    }
   
}
