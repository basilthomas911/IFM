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
    int workerId,
    ActorInformationLoggingPolicy informationLoggingPolicy,
    IRealtimeSourceAdmission? realtimeSourceAdmission,
    RealtimeActorCompletionEvidence? realtimeCompletionEvidence) : IActorThread, IAsyncDisposable, ISupervisorWorkerMetricsSource
{
    const int MaxBatchSize = 64;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorReadyQueue _readyQueue = IsArgumentNull.Set(readyQueue);
    readonly ActorThreadPoolMetricsState _metricsState = IsArgumentNull.Set(metricsState);
    readonly ActorInformationLoggingPolicy _informationLoggingPolicy = IsArgumentNull.Set(informationLoggingPolicy);
    readonly IRealtimeSourceAdmission? _realtimeSourceAdmission = realtimeSourceAdmission;
    readonly RealtimeActorCompletionEvidence? _realtimeCompletionEvidence = realtimeCompletionEvidence;
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
        {
            var result = pending.Result;
            return result.Accepted
                ? ValueTask.CompletedTask
                : ValueTask.FromException(CreateAdmissionException(result));
        }
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
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
        { /* Cancellation is the expected shutdown/retirement outcome. */ 
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the ordered actor lifecycle state machine in one method so cancellation, commit, acknowledgement and cleanup ordering can be audited together.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "The mailbox worker disposes messages and admission/cancellation resources on completion, or transfers them to the detached-handler observer, verified by retirement tests.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2025", Justification = "Detached work explicitly transfers cancellation and admission resource ownership to an observer that disposes them after task completion; recovery does not wait for a handler.")]
    async ValueTask ProcessMailboxAsync(ActorThreadId threadId, CancellationToken cancellationToken)
    {
        if (!_supervisor.Children.TryGetValue(threadId.MailboxId, out var actor)
            || !actor.Mailbox.ThreadQueues.TryGetThreadQueue(threadId, out var queue)
            || queue is not IScheduledActorThreadQueue scheduled)
        {
            return;
        }

        if (!scheduled.TryClaimProcessing()) return;
        var ownsProcessing = true;
        void ReleaseOwnership()
        {
            if (!ownsProcessing) return;
            ownsProcessing = false;
            scheduled.ReleaseProcessing();
        }
        var generation = RealtimeActorResetPolicy.CanReplace(actor.GetType(), threadId.ActorType)
            ? (actor as IReplaceableRealtimeActor)?.RealtimeGeneration : null;
        var executionCancellation = generation is null ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, generation.Token);
        var handlerToken = executionCancellation?.Token ?? cancellationToken;
        var cancellationTransferred = false;
        Id = threadId;
        try
        {
            while (true)
            {
                var processed = 0;
                while (processed < MaxBatchSize
                       && !handlerToken.IsCancellationRequested
                       && scheduled.TryRead(out var message))
                {
                    using var trace = ActorTrace.Start(message!);
                    var loggingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    var handlerStarted = ActorRuntimeMetrics.StartHandler();
                    var verb = message!.Subject.Verb;
                    var outcome = "Succeeded";
                    var suppressRoutineInformation = _informationLoggingPolicy.SuppressesRoutineInformation(threadId, verb);
                    var informationEntryLogged = false;
                    if (!suppressRoutineInformation)
                    {
                        ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                        informationEntryLogged = true;
                    }
                    var deliverySucceeded = false;
                    var deferredCleanup = false;
                    Guid? escapedFailureId = null;
                    var mailboxMetrics = (actor.Mailbox.Metrics as ActorMetricsStore)
                        ?.GetOrRegister(threadId, queue);
                    mailboxMetrics?.RecordDequeued(verb);
                    try
                    {
                        _state = ActorThreadState.ProcessingMessage;
                        IDisposable? generationLease = null;
                        var admitted = threadId.ActorType != ActorType.Realtime
                            || _realtimeSourceAdmission?.TryEnter(message!.SourceSubject,
                                message.SourceDataset, message.SourceGenerationId,
                                out generationLease) != false;
                        try
                        {
                            if (admitted)
                            {
                                var handling = actor.HandleMessageAsync(message!, threadId, handlerToken).AsTask();
                                try
                                {
                                    if (generation is null) await handling.ConfigureAwait(false);
                                    else await handling.WaitAsync(handlerToken).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException) when (generation is not null && handlerToken.IsCancellationRequested && !handling.IsCompleted)
                                {
                                    deferredCleanup = true;
                                    // The detached handler owns its resources until it actually finishes.
                                    cancellationTransferred = true;
                                    _ = ObserveRetiredHandlerAsync(handling, message!, executionCancellation, generationLease);
                                    throw;
                                }
                                ActorRuntimeMetrics.RecordProcessed(threadId.ActorType);
                                deliverySucceeded = mailboxMetrics?.RecordSucceeded() ?? true;
                            }
                            else
                            {
                                outcome = "Fenced";
                                deliverySucceeded = true;
                                mailboxMetrics?.RecordCancelled();
                            }
                        }
                        finally
                        {
                            if (!deferredCleanup) generationLease?.Dispose();
                        }
                    }
                    catch (OperationCanceledException) when (handlerToken.IsCancellationRequested)
                    {
                        outcome = "Cancelled";
                        if (!informationEntryLogged)
                        {
                            ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                            informationEntryLogged = true;
                        }
                        ActorRuntimeMetrics.RecordCanceled(threadId.ActorType);
                        mailboxMetrics?.RecordCancelled();
                    }
                    catch (Exception exception)
                    {
                        outcome = "Failed";
                        if (!informationEntryLogged)
                        {
                            ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                            informationEntryLogged = true;
                        }
                        ActorRuntimeMetrics.RecordFailed(threadId.ActorType);
                        mailboxMetrics?.RecordEscapedFailure(exception);
                        if (SupervisorRuntimeContext.TryGetRecordedFailureId(exception, out var recordedFailureId))
                            escapedFailureId = recordedFailureId;
                        else
                            escapedFailureId = _supervisor.RuntimeContext?.RecordFailure(
                                threadId.MailboxId, threadId, message!.Subject.Verb,
                                ActorFailureStage.Execution, exception);
                        ActorMessageProcessingLog.Failed(_logger, threadId, verb, loggingStarted, exception, message!.Subject, nameof(ProcessMailboxAsync));
                    }
                    finally
                    {
                        if (!deferredCleanup && message is IActorDeliveryCompletion delivery)
                        {
                            try
                            {
                                await delivery.CompleteDeliveryAsync(deliverySucceeded).ConfigureAwait(false);
                            }
                            catch (Exception acknowledgementFailure)
                            {
                                outcome = "Failed";
                                if (!informationEntryLogged)
                                {
                                    ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                                    informationEntryLogged = true;
                                }
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
                            if (!deferredCleanup) message?.Dispose();
                        }
                        catch (Exception disposalFailure)
                        {
                            outcome = "Failed";
                            if (!informationEntryLogged)
                            {
                                ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                            }
                            _supervisor.RuntimeContext?.RecordFailure(
                                threadId.MailboxId, threadId, message?.Subject.Verb ?? string.Empty,
                                ActorFailureStage.Cleanup, disposalFailure, escapedFailureId,
                                ActorMessageOutcomeType.HandledFailure);
                            _logger.LogErrorEvent(threadId.ToString(), disposalFailure,
                                "Actor message disposal failed after processing completed.");
                        }
                        // Observe only after handler, delivery acknowledgement, and cleanup have succeeded.
                        if (string.Equals(outcome, "Succeeded", StringComparison.Ordinal) && threadId.ActorType == ActorType.Realtime)
                            _realtimeCompletionEvidence?.Record(message?.SourceDataset,
                                message?.SourceGenerationId ?? Guid.Empty, message!.Subject);
                        _metricsState.RecordMessageCompleted();
                        ActorRuntimeMetrics.RecordHandler(handlerStarted, threadId.ActorType);
                        if (!suppressRoutineInformation || !string.Equals(outcome, "Succeeded", StringComparison.Ordinal))
                            ActorMessageProcessingLog.Exit(_logger, threadId, verb, outcome, loggingStarted, message!.Subject, nameof(ProcessMailboxAsync));
                    }

                    processed++;
                }

                _state = ActorThreadState.WaitingForMessage;
                if (handlerToken.IsCancellationRequested || scheduled.IsRetired) return;
                ownsProcessing = false; // CompleteDrain atomically releases queue ownership.
                if (!scheduled.CompleteDrain())
                {
                    actor.Mailbox.ThreadQueues.ReleaseThreadQueue(threadId);
                    return;
                }

                ReleaseOwnership();
                if (_readyQueue.Schedule(threadId))
                    return;

                if (!_readyQueue.IsCompleted || cancellationToken.IsCancellationRequested)
                {
                    SharedActorDiagnosticLog.Write26(_logger, null, threadId);
                    return;
                }

                ownsProcessing = scheduled.TryClaimProcessing();
                if (!ownsProcessing) return;

                // Graceful pool shutdown: the ready queue no longer accepts another batch, so this worker retains
                // ownership and drains the mailbox before its processing task completes.
            }
        }
        catch
        {
            _state = ActorThreadState.WaitingForMessage;
            var mayComplete = ownsProcessing;
            ownsProcessing = false;
            if (mayComplete && !scheduled.IsRetired && scheduled.CompleteDrain())
                _readyQueue.Schedule(threadId);
            else
                actor.Mailbox.ThreadQueues.ReleaseThreadQueue(threadId);
            throw;
        }
        finally
        {
            ReleaseOwnership();
            if (!cancellationTransferred) executionCancellation?.Dispose();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    async Task ObserveRetiredHandlerAsync(Task handler, IActorMessage message,
        CancellationTokenSource? executionCancellation, IDisposable? generationLease)
    {
        try { await handler.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* Cancellation is the expected shutdown/retirement outcome. */  }
        catch (Exception error)
        {
            SharedActorDiagnosticLog.Write25(_logger, error, nameof(ObserveRetiredHandlerAsync), message.Subject);
        }
        finally
        {
            try { message.Dispose(); }
            catch (Exception error) { SharedActorDiagnosticLog.Write24(_logger, error); }
            finally
            {
                try { generationLease?.Dispose(); }
                finally { executionCancellation?.Dispose(); }
            }
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
            { /* Cancellation is the expected shutdown/retirement outcome. */ 
            }
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
