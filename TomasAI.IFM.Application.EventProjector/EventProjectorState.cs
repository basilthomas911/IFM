using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Application.EventProjector;

public sealed record EventProjectorState
{
    public required string ProjectorName { get; init; }

    public required Guid StreamId { get; init; }

    public required long LastCompletedStreamVersionId { get; init; }

    public long? ActiveStreamVersionId { get; init; }

    public Guid? ActiveSourceEventId { get; init; }

    public EventProjectorStageType CurrentStage { get; init; }

    public EventProjectorOutcomeType? LastOutcome { get; init; }

    public EventProjectorStageType? FailedStage { get; init; }

    public EventProjectorStageType? ResumeFromStage { get; init; }

    public int RetryCount { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}