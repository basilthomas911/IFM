namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Compare-and-set transition requested by the active projector execution.
/// </summary>
public sealed record EventProjectorStateTransition(
    long EventId,
    string ProjectorName,
    Guid ExecutionToken,
    long ExpectedRevision,
    EventProjectorStageType ExpectedStage,
    EventProjectorStageType NextStage,
    EventProjectorOutcomeType Outcome,
    EventProjectorStageType LastCompletedStage,
    int RetryCount = 0,
    DateTime? NextAttemptAtUtc = null,
    DateTime? LastErrorAtUtc = null,
    string ErrorMessage = "",
    string BlockedReason = "");
