using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Provides projector-scoped durable processing and replay queues backed by NATS JetStream.
/// </summary>
/// <remarks>
/// <para>
/// Each projector name identifies an independent pair of streams and durable consumers: a process
/// queue for newly enqueued events and a replay queue for events whose initial processing failed.
/// Projector names are normalized for use in NATS resource names by replacing characters other than
/// letters, digits, hyphens, and underscores with underscores.
/// </para>
/// <para>
/// A process message is acknowledged after its handler succeeds. If the handler fails, an envelope
/// containing the original event and failure details is first published to the replay stream and the
/// process message is then acknowledged. A failed replay publication or process acknowledgement requests
/// process redelivery without stopping the worker. Stable JetStream message identifiers suppress duplicate
/// process and replay publications within the stream duplicate window. A replay message is negatively
/// acknowledged with the configured delay until it succeeds or reaches the configured delivery limit. At
/// the delivery limit, the optional terminal action is invoked and the replay message is acknowledged even
/// if that action fails.
/// </para>
/// <para>
/// Calling <see cref="DequeueAsync"/> registers the handler used by both workers. Callers should therefore
/// register a handler before enqueueing events. Workers stop after two minutes of inactivity by default and
/// are restarted on the next start, dequeue, or enqueue operation. Configuration and delegates are retained
/// when workers stop. Instances support concurrent use and maintain isolated state for each projector name.
/// </para>
/// </remarks>
public sealed class NatsJSDurableReplayQueue : IDurableReplayQueue, IAsyncDisposable, IDisposable
{
    const int DefaultMaxReplayAttempts = 3;
    static readonly TimeSpan DefaultReplayInterval = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(2);

    readonly ConcurrentDictionary<string, ProjectorQueueState> _states = new(StringComparer.Ordinal);
    readonly INatsJSDurableQueueTransport _transport;
    readonly TimeSpan _idleTimeout;
    int _disposed;

    /// <summary>
    /// Initializes a durable replay queue that connects to the NATS server described by
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options that provide the NATS server URL.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public NatsJSDurableReplayQueue(INatsJetStreamConsumerOptions options)
        : this(new NatsJSDurableQueueTransport(options), DefaultIdleTimeout)
    {
    }

    /// <summary>
    /// Initializes a durable replay queue with a transport and worker idle timeout.
    /// </summary>
    /// <param name="transport">The transport used to configure queues and exchange messages.</param>
    /// <param name="idleTimeout">
    /// The period without consumed messages after which each background worker stops.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="idleTimeout"/> is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    internal NatsJSDurableReplayQueue(INatsJSDurableQueueTransport transport, TimeSpan idleTimeout)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        if (idleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        _idleTimeout = idleTimeout;
    }

    /// <summary>
    /// Creates or updates the JetStream resources for a projector and starts its process and replay workers.
    /// </summary>
    /// <param name="eventProjectorName">
    /// The logical projector name used to isolate state and derive stream, subject, and consumer names.
    /// </param>
    /// <param name="replayInterval">
    /// The initial delay before a failed replay message is made available for another delivery.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels queue initialization and the workers started by this call.
    /// </param>
    /// <returns>A task that completes when the queue resources and workers have been initialized.</returns>
    /// <remarks>
    /// The replay consumer uses exponential backoff derived from <paramref name="replayInterval"/>, capped
    /// at two minutes. Calling this method again updates the projector's replay interval and is otherwise
    /// idempotent while workers are running.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="replayInterval"/> is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public async Task StartAsync(
        string eventProjectorName,
        TimeSpan replayInterval,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        if (replayInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(replayInterval));

        var state = GetState(eventProjectorName);
        state.ReplayInterval = replayInterval;
        await EnsureWorkersStartedAsync(eventProjectorName, state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the process and replay workers for a projector.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector whose workers should stop.</param>
    /// <param name="cancellationToken">A token that cancels the wait to enter the lifecycle operation.</param>
    /// <returns>A task that completes after both workers have stopped.</returns>
    /// <remarks>
    /// This method does not delete JetStream resources or discard the registered handler and retry
    /// configuration. A subsequent start, dequeue, or enqueue operation restarts the workers. If the
    /// projector has not been initialized, the method completes without performing any work.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public async Task StopAsync(string eventProjectorName, CancellationToken cancellationToken = default)
    {
        ValidateProjectorName(eventProjectorName);
        if (!_states.TryGetValue(eventProjectorName, out var state))
            return;

        await state.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            state.ProcessCancellation?.Cancel();
            state.ReplayCancellation?.Cancel();
            await AwaitStoppedAsync(state.ProcessWorker).ConfigureAwait(false);
            await AwaitStoppedAsync(state.ReplayWorker).ConfigureAwait(false);
            state.DisposeWorkers();
        }
        finally
        {
            state.LifecycleGate.Release();
        }
    }

    /// <summary>
    /// Serializes and durably publishes a domain event to a projector's process stream.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector that will process the event.</param>
    /// <param name="domainEvent">The domain event to enqueue.</param>
    /// <param name="cancellationToken">
    /// A token that cancels initialization or publication and, when workers are started by this call,
    /// remains linked to those workers.
    /// </param>
    /// <remarks>
    /// This synchronous method waits for JetStream to acknowledge the publication. It starts inactive
    /// workers before publishing and stores the event's assembly-qualified runtime type in its durable
    /// envelope so that the event can be reconstructed when consumed.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="InvalidOperationException">
    /// The event type cannot be represented in the durable envelope or the transport cannot initialize the queue.
    /// </exception>
    public void Enqueue(
        string eventProjectorName,
        IEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var state = GetState(eventProjectorName);
        EnsureWorkersStartedAsync(eventProjectorName, state, cancellationToken).GetAwaiter().GetResult();
        var payload = Serialize(domainEvent, eventProjectorName);
        var messageId = CreateProcessMessageId(eventProjectorName, domainEvent);
        _transport.PublishProcessAsync(eventProjectorName, payload, messageId, cancellationToken)
            .AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Registers the event handler for a projector and starts its process and replay workers.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector that owns the handler.</param>
    /// <param name="processMessageFunc">
    /// The asynchronous handler invoked for both newly queued events and replay deliveries.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels queue initialization and the workers started by this call.
    /// </param>
    /// <returns>
    /// A task that completes after the handler is registered and workers are started; it does not wait for a message.
    /// </returns>
    /// <remarks>A subsequent call for the same projector replaces the previously registered handler.</remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="processMessageFunc"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public async Task DequeueAsync(
        string eventProjectorName,
        Func<IEvent, Task> processMessageFunc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        ArgumentNullException.ThrowIfNull(processMessageFunc);

        var state = GetState(eventProjectorName);
        state.ProcessMessage = processMessageFunc;
        await EnsureWorkersStartedAsync(eventProjectorName, state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers the action invoked when a replay message reaches its maximum delivery count.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector whose action is configured.</param>
    /// <param name="maxAttemptsReachedFunc">The asynchronous terminal action to invoke with the failed event.</param>
    /// <param name="overwrite">
    /// <see langword="true"/> to replace any existing action; <see langword="false"/> to set the action only
    /// when one has not already been registered.
    /// </param>
    /// <remarks>
    /// After the action completes or throws, the replay message is acknowledged and will not be delivered again.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="maxAttemptsReachedFunc"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    public void SetMaxAttemptsReachedAction(
        string eventProjectorName,
        Func<IEvent, Task> maxAttemptsReachedFunc,
        bool overwrite = true)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        ArgumentNullException.ThrowIfNull(maxAttemptsReachedFunc);
        var state = GetState(eventProjectorName);
        if (overwrite)
            state.MaxAttemptsReached = maxAttemptsReachedFunc;
        else
            Interlocked.CompareExchange(ref state.MaxAttemptsReached, maxAttemptsReachedFunc, null);
    }

    /// <summary>
    /// Sets the maximum number of deliveries allowed for replay messages belonging to a projector.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector whose delivery limit is configured.</param>
    /// <param name="maxReplayAttemps">The maximum replay delivery count. The minimum value is one.</param>
    /// <param name="overwrite">
    /// <see langword="true"/> to replace the current value; <see langword="false"/> to replace it only while
    /// it still has the default value of three.
    /// </param>
    /// <remarks>
    /// The setting takes effect in worker logic immediately. The JetStream consumer configuration is updated
    /// the next time queue initialization runs, such as when workers are restarted.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxReplayAttemps"/> is less than one.</exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    public void SetMaxReplayAttemps(string eventProjectorName, int maxReplayAttemps, bool overwrite = true)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        if (maxReplayAttemps < 1)
            throw new ArgumentOutOfRangeException(nameof(maxReplayAttemps), "Maximum replay attempts must be at least one.");

        var state = GetState(eventProjectorName);
        if (overwrite)
            Volatile.Write(ref state.MaxReplayAttempts, maxReplayAttemps);
        else
            Interlocked.CompareExchange(ref state.MaxReplayAttempts, maxReplayAttemps, DefaultMaxReplayAttempts);
    }

    /// <summary>
    /// Gets the configured maximum replay delivery count for a projector.
    /// </summary>
    /// <param name="eventProjectorName">The logical name of the projector whose delivery limit is returned.</param>
    /// <returns>The configured limit, or three when the projector has not previously been configured.</returns>
    /// <exception cref="ArgumentException"><paramref name="eventProjectorName"/> is empty or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    public int GetMaxReplayAttemps(string eventProjectorName)
    {
        ThrowIfDisposed();
        ValidateProjectorName(eventProjectorName);
        return Volatile.Read(ref GetState(eventProjectorName).MaxReplayAttempts);
    }

    async Task EnsureWorkersStartedAsync(
        string eventProjectorName,
        ProjectorQueueState state,
        CancellationToken cancellationToken)
    {
        await state.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = CreateSettings(eventProjectorName, state.ReplayInterval, state.MaxReplayAttempts);
            await _transport.EnsureQueueAsync(eventProjectorName, settings, cancellationToken).ConfigureAwait(false);

            if (state.ProcessWorker is null || state.ProcessWorker.IsCompleted)
            {
                state.ProcessCancellation?.Dispose();
                state.ProcessCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                state.ProcessCancellation.CancelAfter(_idleTimeout);
                state.ProcessWorker = Task.Run(
                    () => RunProcessWorkerAsync(eventProjectorName, state, state.ProcessCancellation),
                    CancellationToken.None);
            }

            if (state.ReplayWorker is null || state.ReplayWorker.IsCompleted)
            {
                state.ReplayCancellation?.Dispose();
                state.ReplayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                state.ReplayCancellation.CancelAfter(_idleTimeout);
                state.ReplayWorker = Task.Run(
                    () => RunReplayWorkerAsync(eventProjectorName, state, state.ReplayCancellation),
                    CancellationToken.None);
            }
        }
        finally
        {
            state.LifecycleGate.Release();
        }
    }

    async Task RunProcessWorkerAsync(
        string eventProjectorName,
        ProjectorQueueState state,
        CancellationTokenSource idleCancellation)
    {
        try
        {
            await foreach (var message in _transport.ConsumeProcessAsync(eventProjectorName, idleCancellation.Token)
                .ConfigureAwait(false))
            {
                ResetIdleTimeout(idleCancellation);
                try
                {
                    var domainEvent = Deserialize(message.Data);
                    var handler = state.ProcessMessage
                        ?? throw new InvalidOperationException($"No process handler is registered for projector '{eventProjectorName}'.");
                    await handler(domainEvent).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await MoveToReplayOrRequestRedeliveryAsync(
                        eventProjectorName,
                        state,
                        message,
                        ex,
                        idleCancellation.Token).ConfigureAwait(false);
                    ResetIdleTimeout(idleCancellation);
                    continue;
                }

                await AcknowledgeOrRequestRedeliveryAsync(message, state.ReplayInterval, idleCancellation.Token)
                    .ConfigureAwait(false);
                ResetIdleTimeout(idleCancellation);
            }
        }
        catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
        {
        }
    }

    async Task MoveToReplayOrRequestRedeliveryAsync(
        string eventProjectorName,
        ProjectorQueueState state,
        INatsJSDurableMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var replayPayload = MarkFailed(message.Data, exception);
            var messageId = CreateReplayMessageId(eventProjectorName, message.Data);
            await _transport.PublishReplayAsync(eventProjectorName, replayPayload, messageId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await RequestRedeliveryAsync(message, state.ReplayInterval, cancellationToken).ConfigureAwait(false);
            return;
        }

        await AcknowledgeOrRequestRedeliveryAsync(message, state.ReplayInterval, cancellationToken)
            .ConfigureAwait(false);
    }

    static async Task AcknowledgeOrRequestRedeliveryAsync(
        INatsJSDurableMessage message,
        TimeSpan redeliveryDelay,
        CancellationToken cancellationToken)
    {
        try
        {
            await message.AckAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await RequestRedeliveryAsync(message, redeliveryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    static async Task RequestRedeliveryAsync(
        INatsJSDurableMessage message,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await message.NakAsync(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Leaving the message unacknowledged lets JetStream redeliver it after the acknowledgement wait.
        }
    }

    async Task RunReplayWorkerAsync(
        string eventProjectorName,
        ProjectorQueueState state,
        CancellationTokenSource idleCancellation)
    {
        try
        {
            await foreach (var message in _transport.ConsumeReplayAsync(eventProjectorName, idleCancellation.Token)
                .ConfigureAwait(false))
            {
                ResetIdleTimeout(idleCancellation);
                var domainEvent = Deserialize(message.Data);
                try
                {
                    var handler = state.ProcessMessage
                        ?? throw new InvalidOperationException($"No replay handler is registered for projector '{eventProjectorName}'.");
                    await handler(domainEvent).ConfigureAwait(false);
                    await message.AckAsync(idleCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    if (message.DeliveryCount >= (ulong)Volatile.Read(ref state.MaxReplayAttempts))
                    {
                        var maxAttemptsReached = state.MaxAttemptsReached;
                        try
                        {
                            if (maxAttemptsReached is not null)
                                await maxAttemptsReached(domainEvent).ConfigureAwait(false);
                        }
                        finally
                        {
                            await message.AckAsync(idleCancellation.Token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await message.NakAsync(state.ReplayInterval, idleCancellation.Token).ConfigureAwait(false);
                    }
                }
                ResetIdleTimeout(idleCancellation);
            }
        }
        catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
        {
        }
    }

    void ResetIdleTimeout(CancellationTokenSource source)
    {
        if (!source.IsCancellationRequested)
            source.CancelAfter(_idleTimeout);
    }

    ProjectorQueueState GetState(string eventProjectorName) =>
        _states.GetOrAdd(eventProjectorName, static _ => new ProjectorQueueState());

    static NatsJSDurableQueueSettings CreateSettings(
        string eventProjectorName,
        TimeSpan replayInterval,
        int maxReplayAttempts)
    {
        var safeName = new string(eventProjectorName
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_')
            .ToArray());
        var prefix = $"ifm.projector.{safeName}";
        var names = new NatsJSDurableQueueNames(
            $"IFM_{safeName}_PROCESS",
            $"{prefix}.process",
            $"{safeName}-process-worker",
            $"IFM_{safeName}_REPLAY",
            $"{prefix}.replay",
            $"{safeName}-replay-worker");
        var backoff = Enumerable.Range(0, maxReplayAttempts)
            .Select(attempt => TimeSpan.FromTicks(Math.Min(
                replayInterval.Ticks * (1L << Math.Min(attempt, 6)),
                TimeSpan.FromMinutes(2).Ticks)))
            .ToArray();
        return new NatsJSDurableQueueSettings(names, replayInterval, maxReplayAttempts, backoff);
    }

    static byte[] Serialize(IEvent domainEvent, string eventProjectorName)
    {
        var eventType = domainEvent.GetType();
        var envelope = new DurableEventEnvelope(
            eventProjectorName,
            eventType.AssemblyQualifiedName
                ?? throw new InvalidOperationException($"Could not resolve the assembly-qualified name for '{eventType}'."),
            JsonConvert.SerializeObject(domainEvent, eventType, SerializerSettings),
            DateTimeOffset.UtcNow,
            null,
            null);
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, SerializerSettings));
    }

    static string CreateProcessMessageId(string eventProjectorName, IEvent domainEvent)
    {
        var eventIdentity = domainEvent.EventId > 0
            ? $"event-{domainEvent.EventId.ToString(CultureInfo.InvariantCulture)}"
            : $"id-{domainEvent.Id:N}";
        return $"{eventProjectorName}:process:{eventIdentity}";
    }

    static string CreateReplayMessageId(string eventProjectorName, byte[] processPayload) =>
        $"{eventProjectorName}:replay:{Convert.ToHexString(SHA256.HashData(processPayload))}";

    static IEvent Deserialize(byte[] payload)
    {
        var envelope = DeserializeEnvelope(payload);
        var eventType = Type.GetType(envelope.EventType, throwOnError: true)!;
        if (!typeof(IEvent).IsAssignableFrom(eventType))
            throw new InvalidOperationException($"Envelope type '{eventType}' does not implement {nameof(IEvent)}.");
        return (IEvent)(JsonConvert.DeserializeObject(envelope.EventJson, eventType, SerializerSettings)
            ?? throw new InvalidOperationException($"Could not deserialize event type '{eventType}'."));
    }

    static byte[] MarkFailed(byte[] payload, Exception exception)
    {
        var envelope = DeserializeEnvelope(payload) with
        {
            FailedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = exception.Message
        };
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, SerializerSettings));
    }

    static DurableEventEnvelope DeserializeEnvelope(byte[] payload) =>
        JsonConvert.DeserializeObject<DurableEventEnvelope>(Encoding.UTF8.GetString(payload), SerializerSettings)
        ?? throw new InvalidOperationException("The durable event envelope is invalid.");

    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    };

    static void ValidateProjectorName(string eventProjectorName)
    {
        if (string.IsNullOrWhiteSpace(eventProjectorName))
            throw new ArgumentException("An event projector name is required.", nameof(eventProjectorName));
    }

    static async Task AwaitStoppedAsync(Task? worker)
    {
        if (worker is null)
            return;
        try
        {
            await worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    /// <summary>
    /// Asynchronously stops all workers and releases the underlying NATS transport.
    /// </summary>
    /// <returns>A value task that completes when all owned asynchronous resources have been released.</returns>
    /// <remarks>This method is idempotent. It does not delete streams or consumers from JetStream.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        foreach (var state in _states.Values)
        {
            state.ProcessCancellation?.Cancel();
            state.ReplayCancellation?.Cancel();
        }
        await Task.WhenAll(_states.Values.SelectMany(state => new[] { state.ProcessWorker, state.ReplayWorker })
            .Where(task => task is not null)!
            .Cast<Task>()).ConfigureAwait(false);
        foreach (var state in _states.Values)
            state.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops all workers and synchronously releases the underlying NATS transport.
    /// </summary>
    /// <remarks>This method blocks until asynchronous disposal completes and is safe to call more than once.</remarks>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    sealed record DurableEventEnvelope(
        string EventProjectorName,
        string EventType,
        string EventJson,
        DateTimeOffset EnqueuedAtUtc,
        DateTimeOffset? FailedAtUtc,
        string? ErrorMessage);

    sealed class ProjectorQueueState : IDisposable
    {
        public readonly SemaphoreSlim LifecycleGate = new(1, 1);
        public Func<IEvent, Task>? ProcessMessage;
        public Func<IEvent, Task>? MaxAttemptsReached;
        public int MaxReplayAttempts = DefaultMaxReplayAttempts;
        public TimeSpan ReplayInterval = DefaultReplayInterval;
        public CancellationTokenSource? ProcessCancellation;
        public CancellationTokenSource? ReplayCancellation;
        public Task? ProcessWorker;
        public Task? ReplayWorker;

        public void DisposeWorkers()
        {
            ProcessCancellation?.Dispose();
            ReplayCancellation?.Dispose();
            ProcessCancellation = null;
            ReplayCancellation = null;
            ProcessWorker = null;
            ReplayWorker = null;
        }

        public void Dispose()
        {
            DisposeWorkers();
            LifecycleGate.Dispose();
        }
    }
}
