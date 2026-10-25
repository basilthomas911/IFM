using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

sealed class ActorMailboxMetrics(ActorThreadId threadId, IActorThreadQueue queue, long generation)
{
    readonly ActorThreadId _threadId = threadId;
    IActorThreadQueue _queue = queue;
    long _accepted;
    long _dequeued;
    long _succeeded;
    long _handledFailures;
    long _escapedFailures;
    long _cancelled;
    long _rejected;
    long _capacityRejected;
    long _lastAcceptedTicks;
    long _lastStartedTicks;
    long _lastCompletedTicks;
    long _lastFailedTicks;
    long _lastRejectedTicks;
    long _handlerCount;
    long _totalHandlerStopwatchTicks;
    long _maximumHandlerStopwatchTicks;
    long _revision;
    long _generation = generation;
    int _admissionOpen = 1;
    int _lifecycleState = (int)ActorMailboxLifecycleState.Running;
    int _processing;
    int _peakQueueDepth;
    int _processingManagedThreadId;
    string _currentVerb = string.Empty;
    string _lastExceptionType = string.Empty;
    string _lastError = string.Empty;
    string _lastRejectionReason = string.Empty;
    string _lastRejectedVerb = string.Empty;

    internal void SetQueue(IActorThreadQueue queue, long generation)
    {
        Volatile.Write(ref _queue, queue);
        Interlocked.Exchange(ref _generation, generation);
    }
    internal bool IsFor(IActorThreadQueue queue) => ReferenceEquals(Volatile.Read(ref _queue), queue);

    internal void RecordAccepted()
    {
        Interlocked.Increment(ref _accepted);
        UpdatePeak(Volatile.Read(ref _queue).Count);
        Interlocked.Exchange(ref _lastAcceptedTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _revision);
    }

    internal void RecordDequeued(string verb)
    {
        Interlocked.Increment(ref _dequeued);
        Volatile.Write(ref _currentVerb, verb);
        Interlocked.Exchange(ref _lastStartedTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _processing, 1);
        Volatile.Write(ref _processingManagedThreadId, Environment.CurrentManagedThreadId);
        Interlocked.Increment(ref _revision);
    }

    internal bool RecordSucceeded()
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _succeeded);
        FinishCompletion();
        return true;
    }

    internal bool RecordHandledFailure(Exception exception)
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _handledFailures);
        RecordFailure(exception);
        FinishCompletion();
        return true;
    }

    internal bool RecordEscapedFailure(Exception exception)
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _escapedFailures);
        RecordFailure(exception);
        FinishCompletion();
        return true;
    }

    internal bool RecordCancelled()
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _cancelled);
        FinishCompletion();
        return true;
    }

    internal void RecordRejected(ActorAdmissionReason reason, string verb)
    {
        Interlocked.Increment(ref _rejected);
        if (reason is ActorAdmissionReason.MailboxLimit or ActorAdmissionReason.GlobalMessageLimit
            or ActorAdmissionReason.GlobalByteLimit or ActorAdmissionReason.ActorTypeMessageLimit
            or ActorAdmissionReason.ActorTypeByteLimit)
            Interlocked.Increment(ref _capacityRejected);
        Interlocked.Exchange(ref _lastRejectedTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _lastRejectionReason, reason.ToStringFast());
        Volatile.Write(ref _lastRejectedVerb, verb);
        Interlocked.Increment(ref _revision);
    }

    internal void SetAdmission(bool isOpen)
    {
        Volatile.Write(ref _admissionOpen, isOpen ? 1 : 0);
        Interlocked.Increment(ref _revision);
    }

    internal void SetLifecycle(ActorMailboxLifecycleState state)
    {
        Volatile.Write(ref _lifecycleState, (int)state);
        Interlocked.Increment(ref _revision);
    }

    internal ActorMailboxMetricsSnapshot CaptureSnapshot()
    {
        var currentQueue = Volatile.Read(ref _queue);
        return new ActorMailboxMetricsSnapshot(
            _threadId,
            currentQueue.Count,
            currentQueue.Capacity,
            Volatile.Read(ref _peakQueueDepth),
            Interlocked.Read(ref _accepted),
            Interlocked.Read(ref _dequeued),
            Interlocked.Read(ref _succeeded),
            Interlocked.Read(ref _handledFailures),
            Interlocked.Read(ref _escapedFailures),
            Interlocked.Read(ref _cancelled),
            Interlocked.Read(ref _rejected),
            Volatile.Read(ref _admissionOpen) != 0,
            (ActorMailboxLifecycleState)Volatile.Read(ref _lifecycleState),
            Interlocked.Read(ref _generation),
            Volatile.Read(ref _processing) != 0,
            Volatile.Read(ref _currentVerb),
            ToUtc(Interlocked.Read(ref _lastAcceptedTicks)),
            ToUtc(Interlocked.Read(ref _lastStartedTicks)),
            ToUtc(Interlocked.Read(ref _lastCompletedTicks)),
            ToUtc(Interlocked.Read(ref _lastFailedTicks)),
            Volatile.Read(ref _lastExceptionType),
            Volatile.Read(ref _lastError),
            Interlocked.Read(ref _revision),
            ToUtc(Interlocked.Read(ref _lastRejectedTicks)),
            Volatile.Read(ref _lastRejectionReason),
            Volatile.Read(ref _lastRejectedVerb),
            Volatile.Read(ref _processingManagedThreadId),
            Interlocked.Read(ref _handlerCount),
            StopwatchDuration(Interlocked.Read(ref _totalHandlerStopwatchTicks)),
            StopwatchDuration(Interlocked.Read(ref _maximumHandlerStopwatchTicks)),
            Interlocked.Read(ref _capacityRejected));
    }

    void UpdatePeak(int depth)
    {
        var current = Volatile.Read(ref _peakQueueDepth);
        while (depth > current)
        {
            var observed = Interlocked.CompareExchange(ref _peakQueueDepth, depth, current);
            if (observed == current) return;
            current = observed;
        }
    }

    void RecordFailure(Exception exception)
    {
        Volatile.Write(ref _lastExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Volatile.Write(ref _lastError, Bound(exception.Message, 2048));
        Interlocked.Exchange(ref _lastFailedTicks, DateTime.UtcNow.Ticks);
    }

    bool TryComplete() => Interlocked.CompareExchange(ref _processing, 0, 1) == 1;

    void FinishCompletion()
    {
        var startedUtc = Interlocked.Read(ref _lastStartedTicks);
        if (startedUtc != 0)
        {
            var elapsed = Math.Max(0, DateTime.UtcNow.Ticks - startedUtc);
            Interlocked.Increment(ref _handlerCount);
            Interlocked.Add(ref _totalHandlerStopwatchTicks, elapsed);
            UpdateMaximum(ref _maximumHandlerStopwatchTicks, elapsed);
        }
        Volatile.Write(ref _currentVerb, string.Empty);
        Volatile.Write(ref _processingManagedThreadId, 0);
        Interlocked.Exchange(ref _lastCompletedTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _revision);
    }

    static DateTime? ToUtc(long ticks) => ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    static TimeSpan StopwatchDuration(long ticks) => TimeSpan.FromTicks(Math.Max(0, ticks));
    static void UpdateMaximum(ref long target, long value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current) return;
            current = observed;
        }
    }
    static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}
