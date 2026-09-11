namespace TomasAI.IFM.UI.Net.Models.Operations;

public sealed record ActorHealthSnapshot
{
    public DateTime ObservedUtc { get; init; }
    public int OverallStatus { get; init; } = 2;
    public int ActorCount { get; init; }
    public int RunningActorCount { get; init; }
    public int ProcessingMailboxCount { get; init; }
    public int QueuedMessageCount { get; init; }
    public IReadOnlyList<ActorHealthActorSnapshot> Actors { get; init; } = [];
    public IReadOnlyList<ActorHealthFailureRecord> Failures { get; init; } = [];
    public IReadOnlyList<ActorHealthProjectorSnapshot> Projectors { get; init; } = [];
    public IReadOnlyList<ActorHealthWorkerSnapshot> Workers { get; init; } = [];
}

public sealed record ActorHealthWorkerSnapshot
{
    public int WorkerId { get; init; }
    public int State { get; init; }
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
}

public sealed record ActorHealthFailureRecord
{
    public Guid FailureId { get; init; }
    public DateTime FailedUtc { get; init; }
    public ActorHealthActorId ActorId { get; init; } = new();
    public ActorHealthThreadId ThreadId { get; init; } = new();
    public string Verb { get; init; } = string.Empty;
    public int Stage { get; init; }
    public string ExceptionType { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public Guid? PrimaryFailureId { get; init; }
    public int Severity { get; init; }
    public int Outcome { get; init; }
    public int DeliveryOutcome { get; init; }
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
    public int LifecycleState { get; init; }
    public long Generation { get; init; }
    public int Status { get; init; } = 2;
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
    public int ActorType { get; init; }
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
    public int LifecycleState { get; init; }
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
    public int ActorType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
}
