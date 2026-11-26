using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

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
                    if (threadId.ActorType == ActorType.Realtime && actor is IBatchedRealtimeActor batched)
                    {
                        IReadOnlyList<IActorMessage> messages;
                        Task handling;
                        if(message is IRealtimeBrokerBatch brokerBatch)
                        {
                            messages=brokerBatch.Messages;
                            handling=ProcessBrokerBatchAsync(actor,batched,brokerBatch,threadId,queue,handlerToken);
                        }
                        else
                        {
                            var local=new List<IActorMessage>(MaxBatchSize){message!};
                            long bytes=message!.AdmissionSizeBytes;
                            // A broker group is already bounded and ordered by its durable owner.
                            // Never flatten it into a preceding legacy backlog or coalesce separate broker deliveries.
                            while(message is not IRealtimeBrokerDelivery && local.Count<MaxBatchSize && bytes<64*1024
                                && scheduled.TryPeek(out var peek) && peek is not IRealtimeBrokerDelivery && scheduled.TryRead(out var next))
                            {local.Add(next!);bytes+=next!.AdmissionSizeBytes;}
                            messages=local;
                            handling=ProcessRealtimeBatchAsync(actor,batched,messages,threadId,queue,handlerToken);
                        }
                        try { await handling.WaitAsync(handlerToken).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (!handling.IsCompleted)
                        {
                            // The batch task retains every payload/lease until the real handler finishes.
                            cancellationTransferred = true;
                            _ = ObserveRetiredBatchAsync(handling, executionCancellation);
                            return;
                        }
                        processed += messages.Count;
                        continue;
                    }
                    using var trace = ActorTrace.Start(message!);
                    var loggingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    var handlerStarted = ActorRuntimeMetrics.StartHandler();
                    var verb = message!.Subject.Verb;
                    var outcome = "Succeeded";
                    var informationEnabled = _logger.IsEnabled(LogLevel.Information);
                    var suppressRoutineInformation = !informationEnabled || !_informationLoggingPolicy.SelectRoutineInformation(threadId, verb);
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
                                if(message is IRealtimeBrokerBatch)throw new InvalidOperationException("Broker batches require an opted-in realtime batch actor.");
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
                                if (outcome == "Fenced" && message is IRealtimeBrokerDelivery broker) await broker.CompleteFencedAsync().ConfigureAwait(false);
                                else await delivery.CompleteDeliveryAsync(deliverySucceeded).ConfigureAwait(false);
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
                                informationEntryLogged = true;
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
                        var slowInformation = informationEnabled && suppressRoutineInformation
                            && _informationLoggingPolicy.CapturesSlowInformation(threadId, verb, loggingStarted);
                        if (!suppressRoutineInformation || slowInformation || !string.Equals(outcome, "Succeeded", StringComparison.Ordinal))
                        {
                            if (!informationEntryLogged)
                                ActorMessageProcessingLog.Entry(_logger, threadId, verb, message!.Subject, nameof(ProcessMailboxAsync));
                            ActorMessageProcessingLog.Exit(_logger, threadId, verb, outcome, loggingStarted, message!.Subject, nameof(ProcessMailboxAsync));
                        }
                        else if (informationEnabled) _informationLoggingPolicy.RecordSuppressedSuccess(threadId);
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

    async Task ProcessBrokerBatchAsync(IActor actor,IBatchedRealtimeActor batched,IRealtimeBrokerBatch wrapper,
        ActorThreadId threadId,IActorThreadQueue queue,CancellationToken token)
    {
        try
        {
            var children=wrapper.Messages;
            if(children.Count is <1 or >64 || children.Any(child=>child is IRealtimeBrokerBatch || child.Subject.ThreadId!=threadId))
                throw new InvalidDataException("Broker batch children must be bounded and belong to one exact mailbox partition.");
            await ProcessRealtimeBatchAsync(actor,batched,children,threadId,queue,token).ConfigureAwait(false);
        }
        catch(Exception error)
        {
            _supervisor.RuntimeContext?.RecordFailure(threadId.MailboxId,threadId,wrapper.Subject.Verb,ActorFailureStage.Execution,error);
            _logger.LogErrorEvent(threadId.ToString(),error,"Broker realtime batch rejected before settlement.");
            try{await wrapper.CompleteDeliveryAsync(false).ConfigureAwait(false);}
            catch(Exception settlement){_logger.LogErrorEvent(threadId.ToString(),settlement,"Rejected broker batch settlement failed.");}
        }
        finally
        {
            // This task, not the cancellable worker wait, owns pooled wrapper/child storage.
            try{wrapper.Dispose();}
            catch(Exception cleanup){_logger.LogErrorEvent(threadId.ToString(),cleanup,"Broker batch payload cleanup failed.");}
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A batch owns every input until its handler and delivery settlement finish; failure is recorded for each input and isolated from unrelated mailboxes.")]
    async Task ProcessRealtimeBatchAsync(IActor actor, IBatchedRealtimeActor batched,
        IReadOnlyList<IActorMessage> messages, ActorThreadId threadId, IActorThreadQueue queue, CancellationToken token)
    {
        var admitted = new List<IActorMessage>(messages.Count);
        var leases = new List<IDisposable>(messages.Count);
        var fenced = new HashSet<IActorMessage>(ReferenceEqualityComparer.Instance);
        var metrics = (actor.Mailbox.Metrics as ActorMetricsStore)?.GetOrRegister(threadId, queue);
        var started = ActorRuntimeMetrics.StartHandler();
        var succeeded = false;
        Exception? failure = null;
        _state = ActorThreadState.ProcessingMessage;
        metrics?.RecordDequeued(messages[0].Subject.Verb);
        using var trace = ActorTrace.Start(messages[0]);
        try
        {
            foreach (var message in messages)
            {
                token.ThrowIfCancellationRequested();
                IDisposable? lease = null;
                if (_realtimeSourceAdmission?.TryEnter(message.SourceSubject, message.SourceDataset, message.SourceGenerationId, out lease) == false)
                    fenced.Add(message);
                else
                {
                    admitted.Add(message);
                    if (lease is not null) leases.Add(lease);
                }
            }
            if (admitted.Count != 0) await batched.HandleBatchAsync(admitted, threadId, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            succeeded = true;
        }
        catch (Exception error)
        {
            failure = error;
            _supervisor.RuntimeContext?.RecordFailure(threadId.MailboxId, threadId, messages[0].Subject.Verb, ActorFailureStage.Execution, error);
            if (!token.IsCancellationRequested)
                _logger.LogErrorEvent(threadId.ToString(), error, "Realtime durable batch failed before delivery settlement.");
        }
        finally
        {
            for (var index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                var wasFenced = fenced.Contains(message);
                var complete = wasFenced || succeeded && !token.IsCancellationRequested;
                if (index != 0) metrics?.RecordDequeued(message.Subject.Verb);
                if (wasFenced) metrics?.RecordCancelled();
                else if (complete) { metrics?.RecordSucceeded(); ActorRuntimeMetrics.RecordProcessed(threadId.ActorType); }
                else if (token.IsCancellationRequested) { metrics?.RecordCancelled(); ActorRuntimeMetrics.RecordCanceled(threadId.ActorType); }
                else { metrics?.RecordEscapedFailure(failure!); ActorRuntimeMetrics.RecordFailed(threadId.ActorType); }
                var settled = false;
                try
                {
                    if (message is IActorDeliveryCompletion delivery)
                        if (wasFenced && message is IRealtimeBrokerDelivery broker) await broker.CompleteFencedAsync().ConfigureAwait(false);
                        else await delivery.CompleteDeliveryAsync(complete).ConfigureAwait(false);
                    settled = true;
                }
                catch (Exception error)
                {
                    _supervisor.RuntimeContext?.RecordFailure(threadId.MailboxId, threadId, message.Subject.Verb, ActorFailureStage.Publication, error);
                    _logger.LogErrorEvent(threadId.ToString(), error, "Realtime batch delivery settlement failed.");
                }
                finally
                {
                    try { message.Dispose(); }
                    catch (Exception error) { settled = false; _logger.LogErrorEvent(threadId.ToString(), error, "Realtime batch payload disposal failed."); }
                    _metricsState.RecordMessageCompleted();
                }
                if (complete && !wasFenced && settled)
                    _realtimeCompletionEvidence?.Record(message.SourceDataset, message.SourceGenerationId, message.Subject);
            }
            foreach (var lease in leases)
            {
                try { lease.Dispose(); }
                catch (Exception error) { _logger.LogErrorEvent(threadId.ToString(), error, "Realtime batch source lease disposal failed."); }
            }
            ActorRuntimeMetrics.RecordHandler(started, threadId.ActorType);
        }
    }

    async Task ObserveRetiredBatchAsync(Task handling, CancellationTokenSource? cancellation)
    {
        try { await handling.ConfigureAwait(false); }
        catch (Exception error) { SharedActorDiagnosticLog.Write25(_logger, error, nameof(ObserveRetiredBatchAsync), default); }
        finally { cancellation?.Dispose(); }
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
