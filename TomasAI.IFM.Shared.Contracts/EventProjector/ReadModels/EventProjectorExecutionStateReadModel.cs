namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// Additive SWO-06 execution metadata for one event/projector state row.
/// </summary>
public sealed record EventProjectorExecutionStateReadModel(
    long EventId,
    string ActorName,
    string ProjectorName,
    bool IsReplay,
    int AttemptNumber,
    EventProjectorOutcomeType Outcome,
    EventProjectorStageType Stage,
    string ErrorMessage,
    DateTime CreatedTimestamp,
    DateTime UpdatedTimestamp,
    long EventStreamId,
    string SourceEventName,
    long Revision,
    Guid? ExecutionToken,
    DateTime? LeaseExpiresAtUtc,
    int RetryCount,
    DateTime? NextAttemptAtUtc,
    DateTime? LastErrorAtUtc,
    string BlockedReason,
    EventProjectorStageType LastCompletedStage,
    DateTime UpdatedAtUtc,
    EventProjectorStageType BlockedStage = EventProjectorStageType.None,
    long StreamVersion = 0);
