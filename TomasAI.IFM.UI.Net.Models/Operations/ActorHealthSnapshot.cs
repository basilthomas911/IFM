using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.Models.Operations;

public sealed record ActorHealthSnapshot
{
    public DateTime ObservedUtc { get; init; }
    public SupervisorActorHealthStatus OverallStatus { get; init; } = SupervisorActorHealthStatus.Red;
    public int ActorCount { get; init; }
    public int RunningActorCount { get; init; }
    public int ProcessingMailboxCount { get; init; }
    public int QueuedMessageCount { get; init; }
    public IReadOnlyList<ActorHealthActorSnapshot> Actors { get; init; } = [];
    public IReadOnlyList<ActorHealthFailureRecord> Failures { get; init; } = [];
    public IReadOnlyList<ActorHealthProjectorSnapshot> Projectors { get; init; } = [];
    public IReadOnlyList<ActorHealthWorkerSnapshot> Workers { get; init; } = [];
    public ActorHealthCollectionSnapshot Collection { get; init; } = new();
    public ActorHealthPollingStatus Polling { get; init; } = new();
    public IReadOnlyList<ActorHealthIncident> Incidents { get; init; } = [];
    public IReadOnlyList<ActorHealthOperation> Operations { get; init; } = [];
    public IReadOnlyList<ActorHealthHistoryPoint> History { get; init; } = [];
    public string SupervisorAuthorityState { get; init; } = string.Empty;
    public bool ManualMutationEnabled { get; init; }
    public bool AutomaticMutationEnabled { get; init; }
}

public record ActorHealthCollectionSnapshot
{
    public long Revision { get; init; }
    public DateTime ObservedUtc { get; init; }
    public int ExpectedActors { get; init; }
    public int CollectedActors { get; init; }
    public int FailedActors { get; init; }
    public int HealthyActors { get; init; }
    public int DegradedActors { get; init; }
    public int CriticalActors { get; init; }
    public int UnknownActors { get; init; }
    public long QueueDepth { get; init; }
    public long Rejected { get; init; }
    public long Failed { get; init; }
    public double ProcessCpuPercent { get; init; }
    public long ProcessWorkingSetBytes { get; init; }
    public long ManagedHeapBytes { get; init; }
    public long TotalAllocatedBytes { get; init; }
    public long Gen0Collections { get; init; }
    public long Gen1Collections { get; init; }
    public long Gen2Collections { get; init; }
    public TimeSpan TotalGcPause { get; init; }
    public long ThreadPoolPendingItems { get; init; }
    public int ThreadPoolThreads { get; init; }
}

public sealed record ActorHealthPollingStatus
{
    public string State { get; init; } = string.Empty;
    public string ThreadName { get; init; } = string.Empty;
    public bool IsAlive { get; init; }
    public long CompletedCycles { get; init; }
    public long FailedCycles { get; init; }
    public long Overruns { get; init; }
    public DateTime? LastHeartbeatUtc { get; init; }
}

public sealed record ActorHealthIncident
{
    public ActorHealthThreadId ThreadId { get; init; } = new();
    public string Health { get; init; } = string.Empty;
    public DateTime FirstObservedUtc { get; init; }
    public DateTime LastObservedUtc { get; init; }
    public long Generation { get; init; }
    public int QueueDepth { get; init; }
    public int QueueCapacity { get; init; }
    public int RestartsIn60Minutes { get; init; }
    public int RestartsIn24Hours { get; init; }
    public bool RequiresTraderReview { get; init; }
}

public sealed record ActorHealthOperation
{
    public Guid OperationId { get; init; }
    public ActorHealthThreadId ThreadId { get; init; } = new();
    public long ExpectedGeneration { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public string? Outcome { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record ActorHealthHistoryPoint : ActorHealthCollectionSnapshot;

public sealed record ActorHealthWorkerSnapshot
{
    public int WorkerId { get; init; }
    public ActorThreadState State { get; init; }
    public bool IsStarted { get; init; }
    public bool IsRunning { get; init; }
    public bool IsFaulted { get; init; }
    public ActorHealthThreadId? CurrentMailbox { get; init; }
    public string ExceptionType { get; init; } = string.Empty;
    public string FailureReason { get; init; } = string.Empty;
}

public sealed record ActorHealthProjectorSnapshot
{
    public string ActorName { get; init; } = string.Empty;
    public string ProjectorName { get; init; } = string.Empty;
    public string DurableProcessQueue { get; init; } = string.Empty;
    public string DurableReplayQueue { get; init; } = string.Empty;
    public bool IsReady { get; init; }
    public long RecoveryEventsDiscovered { get; init; }
    public long RecoveryEventsQueued { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public long PendingCount { get; init; }
    public double OldestPendingAgeSeconds { get; init; }
    public long BlockedCount { get; init; }
    public long TerminalFailedCount { get; init; }
    public long ExpiredLeaseCount { get; init; }
    public long OutboxPendingCount { get; init; }
    public double OldestOutboxAgeSeconds { get; init; }
    public long OutboxRetryCount { get; init; }
    public int BusyWorkers { get; init; }
    public int WorkerCapacity { get; init; }
}

public sealed record ActorHealthFailureRecord
{
    public Guid FailureId { get; init; }
    public DateTime FailedUtc { get; init; }
    public ActorHealthActorId ActorId { get; init; } = new();
    public ActorHealthThreadId ThreadId { get; init; } = new();
    public string Verb { get; init; } = string.Empty;
    public ActorFailureStage Stage { get; init; }
    public string ExceptionType { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public Guid? PrimaryFailureId { get; init; }
    public ActorFailureSeverity Severity { get; init; }
    public ActorMessageOutcomeType Outcome { get; init; }
    public ActorDeliveryOutcomeType DeliveryOutcome { get; init; }
    public int HResult { get; init; }
    public string ExceptionDetail { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public string SpanId { get; init; } = string.Empty;
}

public sealed record ActorHealthActorSnapshot
{
    public ActorHealthActorId ActorId { get; init; } = new();
    public string Domain { get; init; } = string.Empty;
    public string Implementation { get; init; } = string.Empty;
    public bool IsRunning { get; init; }
    public SupervisorActorLifecycleState LifecycleState { get; init; }
    public long Generation { get; init; }
    public SupervisorActorHealthStatus Status { get; init; } = SupervisorActorHealthStatus.Red;
    public int QueueDepth { get; init; }
    public long Accepted { get; init; }
    public long Dequeued { get; init; }
    public long Succeeded { get; init; }
    public long HandledFailures { get; init; }
    public long EscapedFailures { get; init; }
    public long Cancelled { get; init; }
    public long Rejected { get; init; }
    public IReadOnlyList<ActorHealthMailboxSnapshot> Mailboxes { get; init; } = [];
}

public sealed record ActorHealthActorId
{
    public ActorType ActorType { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed record ActorHealthMailboxSnapshot
{
    public ActorHealthThreadId ThreadId { get; init; } = new();
    public int QueueDepth { get; init; }
    public long Accepted { get; init; }
    public long Dequeued { get; init; }
    public long Succeeded { get; init; }
    public long HandledFailures { get; init; }
    public long EscapedFailures { get; init; }
    public long Cancelled { get; init; }
    public long Rejected { get; init; }
    public bool IsAdmissionOpen { get; init; }
    public ActorMailboxLifecycleState LifecycleState { get; init; }
    public long Generation { get; init; }
    public bool IsProcessing { get; init; }
    public string CurrentVerb { get; init; } = string.Empty;
    public DateTime? LastAcceptedUtc { get; init; }
    public DateTime? LastStartedUtc { get; init; }
    public DateTime? LastCompletedUtc { get; init; }
    public DateTime? LastFailedUtc { get; init; }
    public string LastExceptionType { get; init; } = string.Empty;
    public string LastError { get; init; } = string.Empty;
    public long Revision { get; init; }
}

public sealed record ActorHealthThreadId
{
    public ActorType ActorType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
}
