using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// One asynchronous worker in the shared actor scheduler.
/// </summary>
/// <remarks>
/// Workers no longer own a mutable entity assignment. They take scheduled mailboxes from a shared ready queue,
/// process a bounded batch, and then either republish the mailbox or retire it. Queue scheduling guarantees that no
/// two workers process the same actor/entity concurrently.
/// </remarks>
sealed class ActorThreadV2(
    IActorSupervisor supervisor,
    ILogger logger,
    ActorReadyQueue readyQueue) : IActorThread, IAsyncDisposable, IDisposable
{
    const int MaxBatchSize = 64;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorReadyQueue _readyQueue = IsArgumentNull.Set(readyQueue);
    readonly CancellationTokenSource _cts = new();
    volatile ActorThreadState _state = ActorThreadState.Ready;
    Task? _processingTask;
    Exception? _exception;
    int _startOnce;
    int _stopOnce;
    int _disposeOnce;

    public ActorThreadId Id { get; set; }

    public bool Post(IActorMessage message)
    {
        var subject = message.Subject;
        if (!_supervisor.Children.TryGetValue(subject.ActorId, out var actor))
            throw new KeyNotFoundException($"Actor with mailbox id '{subject.ActorId}' not found.");
        return actor.Mailbox.ThreadQueues.Write(message, subject);
    }

    public ValueTask WriteToActorThreadQueueAsync(
        IActorMessage message,
        CancellationToken cancellationToken = default)
        => WriteAsync(message, message.Subject, cancellationToken);

    public ValueTask WriteToActorThreadQueueAsync(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
        => WriteAsync(message, subject, cancellationToken);

    ValueTask WriteAsync(IActorMessage message, ActorSubject subject, CancellationToken cancellationToken)
    {
        if (!_supervisor.Children.TryGetValue(subject.ActorId, out var actor))
            return ValueTask.FromException(
                new KeyNotFoundException($"Actor with mailbox id '{subject.ActorId}' not found."));

        var pending = actor.Mailbox.ThreadQueues.WriteAsync(message, subject, cancellationToken);
        if (pending.IsCompletedSuccessfully)
            return pending.Result
                ? ValueTask.CompletedTask
                : ValueTask.FromException(new InvalidOperationException("Actor mailbox rejected the message."));
        return AwaitWrite(pending);
    }

    static async ValueTask AwaitWrite(ValueTask<bool> pending)
    {
        if (!await pending.ConfigureAwait(false))
            throw new InvalidOperationException("Actor mailbox rejected the message.");
    }

    public bool Start()
    {
        if (Interlocked.CompareExchange(ref _startOnce, 1, 0) == 0)
        {
            _state = ActorThreadState.Started;
            _processingTask = Task.Run(ProcessReadyMailboxesAsync);
        }
        return true;
    }

    public bool Start(IActor actor, ActorThreadId threadId) => Start();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SignalMessageAvailable(ActorThreadId threadId)
    {
        if (!_readyQueue.Schedule(threadId) && Volatile.Read(ref _stopOnce) == 0)
            throw new InvalidOperationException("The actor scheduler is not accepting work.");
    }

    public bool Stop()
    {
        if (Interlocked.Exchange(ref _stopOnce, 1) == 0)
        {
            _state = ActorThreadState.Stopped;
            _cts.Cancel();
        }
        return true;
    }

    public bool IsRunning => _state is ActorThreadState.ProcessingMessage or ActorThreadState.WaitingForMessage;
    public bool IsStarted => _state is ActorThreadState.Started or ActorThreadState.ProcessingMessage or ActorThreadState.WaitingForMessage;
    public bool IsStopped => _state == ActorThreadState.Stopped;
    public bool IsFaulted => _state == ActorThreadState.Faulted;
    public bool IsTimedOut => _state == ActorThreadState.TimedOut;
    public ActorThreadState State => _state;
    public Exception? Exception => _exception;
    internal Task Completion => _processingTask ?? Task.CompletedTask;

    async Task ProcessReadyMailboxesAsync()
    {
        var cancellationToken = _cts.Token;
        try
        {
            _state = ActorThreadState.WaitingForMessage;
            await foreach (var threadId in _readyQueue.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await ProcessMailboxAsync(threadId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogErrorEvent(threadId.ToString(), exception,
                        "Actor worker recovered from a mailbox infrastructure failure.");
                    _state = ActorThreadState.WaitingForMessage;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _exception = exception;
            _state = ActorThreadState.Faulted;
            _logger.LogErrorEvent("ActorThread", exception, "Actor worker terminated unexpectedly.");
        }
        finally
        {
            if (_state != ActorThreadState.Faulted)
                _state = ActorThreadState.Stopped;
        }
    }

    async ValueTask ProcessMailboxAsync(ActorThreadId threadId, CancellationToken cancellationToken)
    {
        if (!_supervisor.Children.TryGetValue(threadId.MailboxId, out var actor)
            || !actor.Mailbox.ThreadQueues.TryGetThreadQueue(threadId, out var queue)
            || queue is not IScheduledActorThreadQueue scheduled)
        {
            return;
        }

        Id = threadId;
        try
        {
            while (true)
            {
                var processed = 0;
                while (processed < MaxBatchSize
                       && !cancellationToken.IsCancellationRequested
                       && scheduled.TryRead(out var message))
                {
                    try
                    {
                        _state = ActorThreadState.ProcessingMessage;
                        await actor.HandleMessageAsync(message!, threadId, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception exception)
                    {
                        _logger.LogErrorEvent(threadId.ToString(), exception,
                            "Error processing a message in the actor mailbox.");
                    }
                    finally
                    {
                        message?.Dispose();
                    }

                    processed++;
                }

                _state = ActorThreadState.WaitingForMessage;
                if (!scheduled.CompleteDrain())
                {
                    actor.Mailbox.ThreadQueues.ReleaseThreadQueue(threadId);
                    return;
                }

                if (_readyQueue.Schedule(threadId))
                    return;

                if (!_readyQueue.IsCompleted || cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError("Unable to reschedule actor mailbox {ThreadId}.", threadId);
                    return;
                }

                // Graceful pool shutdown: the ready queue no longer accepts another batch, so this worker retains
                // ownership and drains the mailbox before its processing task completes.
            }
        }
        catch
        {
            _state = ActorThreadState.WaitingForMessage;
            if (scheduled.CompleteDrain())
                _readyQueue.Schedule(threadId);
            else
                actor.Mailbox.ThreadQueues.ReleaseThreadQueue(threadId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeOnce, 1) != 0)
            return;

        Stop();
        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
            }
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
