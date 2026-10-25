using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record ActorMailboxMetricsSnapshot(
    ActorThreadId ThreadId,
    int QueueDepth,
    int QueueCapacity,
    int PeakQueueDepth,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long HandledFailures,
    long EscapedFailures,
    long Cancelled,
    long Rejected,
    bool IsAdmissionOpen,
    ActorMailboxLifecycleState LifecycleState,
    long Generation,
    bool IsProcessing,
    string CurrentVerb,
    DateTime? LastAcceptedUtc,
    DateTime? LastStartedUtc,
    DateTime? LastCompletedUtc,
    DateTime? LastFailedUtc,
    string LastExceptionType,
    string LastError,
    long Revision,
    DateTime? LastRejectedUtc = null,
    string LastRejectionReason = "",
    string LastRejectedVerb = "",
    int ProcessingManagedThreadId = 0,
    long HandlerCount = 0,
    TimeSpan TotalHandlerDuration = default,
    TimeSpan MaximumHandlerDuration = default,
    long CapacityRejected = 0);
