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
    ActorReadyQueue readyQueue,
    ActorThreadPoolMetricsState metricsState,
    int workerId) : IActorThread, IAsyncDisposable, ISupervisorWorkerMetricsSource
{
    const int MaxBatchSize = 64;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorReadyQueue _readyQueue = IsArgumentNull.Set(readyQueue);
    readonly ActorThreadPoolMetricsState _metricsState = IsArgumentNull.Set(metricsState);
    readonly CancellationTokenSource _cts = new();
    volatile ActorThreadState _state = ActorThreadState.Ready;
    Task? _processingTask;
    Exception? _exception;
    int _startOnce;
    int _stopOnce;
    int _disposeOnce;

    public int SupervisorWorkerId { get; } = workerId;

    public ActorThreadId Id { get; set; }

    public bool Post(IActorMessage message)
    {
        var subject = message.Subject;
        if (!_supervisor.Children.TryGetValue(subject.ActorId, out var actor))
            throw new KeyNotFoundException($"Actor with mailbox id '{subject.ActorId}' not found.");
        return actor.Mailbox.ThreadQueues.TryAdmit(message, subject).Accepted;
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

        var pending = actor.Mailbox.ThreadQueues.TryAdmitAsync(message, subject, cancellationToken);
        if (pending.IsCompletedSuccessfully)
            return pending.Result.Accepted
                ? ValueTask.CompletedTask
                : ValueTask.FromException(CreateAdmissionException(pending.Result));
        return AwaitWrite(pending);
    }

    static async ValueTask AwaitWrite(ValueTask<ActorAdmissionResult> pending)
    {
        var result = await pending.ConfigureAwait(false);
        if (!result.Accepted)
            throw CreateAdmissionException(result);
    }

    static InvalidOperationException CreateAdmissionException(ActorAdmissionResult result)
        => new($"Actor mailbox rejected the message: {result.Reason.ToStringFast()}.");

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

    public SupervisorWorkerSnapshot CaptureSupervisorSnapshot()
    {
        var exception = _exception;
        return new(
            SupervisorWorkerId,
            _state,
            IsStarted,
            IsRunning,
            IsFaulted,
            _state == ActorThreadState.ProcessingMessage ? Id : null,
            exception?.GetType().FullName ?? string.Empty,
            exception?.Message ?? string.Empty);
    }

    async Task ProcessReadyMailboxesAsync()
    {
        var cancellationToken = _cts.Token;
        try
        {
            _state = ActorThreadState.WaitingForMessage;
            await foreach (var threadId in _readyQueue.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                ActorRuntimeMetrics.RecordWorkerBusy();
                _metricsState.RecordMailboxStarted();
                try
                {
                    await ProcessMailboxAsync(threadId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _supervisor.RuntimeContext?.RecordFailure(
                        threadId.MailboxId, threadId, string.Empty,
                        ActorFailureStage.MailboxInfrastructure, exception);
                    _logger.LogErrorEvent(threadId.ToString(), exception,
                        "Actor worker recovered from a mailbox infrastructure failure.");
                    _state = ActorThreadState.WaitingForMessage;
                }
                finally
                {
                    _metricsState.RecordMailboxCompleted();
                    ActorRuntimeMetrics.RecordWorkerAvailable();
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
                    var handlerStarted = ActorRuntimeMetrics.StartHandler();
                    var deliverySucceeded = false;
                    Guid? escapedFailureId = null;
                    var mailboxMetrics = (actor.Mailbox.Metrics as ActorMetricsStore)
                        ?.GetOrRegister(threadId, queue);
                    mailboxMetrics?.RecordDequeued(message!.Subject.Verb);
                    try
                    {
                        _state = ActorThreadState.ProcessingMessage;
                        using var trace = ActorTrace.Start(message!);
                        await actor.HandleMessageAsync(message!, threadId, cancellationToken).ConfigureAwait(false);
                        ActorRuntimeMetrics.RecordProcessed(threadId.ActorType);
                        deliverySucceeded = mailboxMetrics?.RecordSucceeded() ?? true;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        ActorRuntimeMetrics.RecordCanceled(threadId.ActorType);
                        mailboxMetrics?.RecordCancelled();
                    }
                    catch (Exception exception)
                    {
                        ActorRuntimeMetrics.RecordFailed(threadId.ActorType);
                        mailboxMetrics?.RecordEscapedFailure(exception);
                        if (SupervisorRuntimeContext.TryGetRecordedFailureId(exception, out var recordedFailureId))
                            escapedFailureId = recordedFailureId;
                        else
                            escapedFailureId = _supervisor.RuntimeContext?.RecordFailure(
                                threadId.MailboxId, threadId, message!.Subject.Verb,
                                ActorFailureStage.Execution, exception);
                        _logger.LogErrorEvent(threadId.ToString(), exception,
                            "Error processing a message in the actor mailbox.");
                    }
                    finally
                    {
                        if (message is IActorDeliveryCompletion delivery)
                        {
                            try
                            {
                                await delivery.CompleteDeliveryAsync(deliverySucceeded).ConfigureAwait(false);
                            }
                            catch (Exception acknowledgementFailure)
                            {
                                _supervisor.RuntimeContext?.RecordFailure(
                                    threadId.MailboxId, threadId, message.Subject.Verb,
                                    ActorFailureStage.Publication, acknowledgementFailure,
                                    escapedFailureId, ActorMessageOutcomeType.HandledFailure,
                                    ActorDeliveryOutcomeType.Failed);
                                _logger.LogErrorEvent(threadId.ToString(), acknowledgementFailure,
                                    "Actor durable delivery acknowledgement failed.");
                            }
                        }
                        try
                        {
                            message?.Dispose();
                        }
                        catch (Exception disposalFailure)
                        {
                            _supervisor.RuntimeContext?.RecordFailure(
                                threadId.MailboxId, threadId, message?.Subject.Verb ?? string.Empty,
                                ActorFailureStage.Cleanup, disposalFailure, escapedFailureId,
                                ActorMessageOutcomeType.HandledFailure);
                            _logger.LogErrorEvent(threadId.ToString(), disposalFailure,
                                "Actor message disposal failed after processing completed.");
                        }
                        _metricsState.RecordMessageCompleted();
                        ActorRuntimeMetrics.RecordHandler(handlerStarted, threadId.ActorType);
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
}
