using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// A projector-scoped JetStream process queue and dead-letter/replay queue.
/// Process failures are durably handed to replay before the process message is acknowledged.
/// </summary>
public sealed class NatsJSDurableReplayQueue : IDurableReplayQueue, IAsyncDisposable, IDisposable
{
    const int DefaultMaxReplayAttempts = 3;
    static readonly TimeSpan DefaultReplayInterval = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(2);

    readonly ConcurrentDictionary<string, ProjectorQueueState> _states = new(StringComparer.Ordinal);
    readonly INatsJSDurableQueueTransport _transport;
    readonly TimeSpan _idleTimeout;
    int _disposed;

    public NatsJSDurableReplayQueue(INatsJetStreamConsumerOptions options)
        : this(new NatsJSDurableQueueTransport(options), DefaultIdleTimeout)
    {
    }

    internal NatsJSDurableReplayQueue(INatsJSDurableQueueTransport transport, TimeSpan idleTimeout)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        if (idleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        _idleTimeout = idleTimeout;
    }

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
        _transport.PublishProcessAsync(eventProjectorName, payload, cancellationToken).AsTask().GetAwaiter().GetResult();
    }

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
                    await message.AckAsync(idleCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var replayPayload = MarkFailed(message.Data, ex);
                    await _transport.PublishReplayAsync(eventProjectorName, replayPayload, idleCancellation.Token)
                        .ConfigureAwait(false);
                    await message.AckAsync(idleCancellation.Token).ConfigureAwait(false);
                }
                ResetIdleTimeout(idleCancellation);
            }
        }
        catch (OperationCanceledException) when (idleCancellation.IsCancellationRequested)
        {
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
