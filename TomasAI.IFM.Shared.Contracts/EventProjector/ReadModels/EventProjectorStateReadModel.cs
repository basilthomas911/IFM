using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

public record EventProjectorStateReadModel
{
    public long EventId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ProjectorName { get; init; } = string.Empty;
    public bool IsReplay { get; init; }
    public int AttemptNumber { get; init; }
    public EventProjectorOutcomeType Outcome { get; init; }
    public EventProjectorStageType Stage { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTime CreatedTimestamp { get; init; }
    public DateTime UpdatedTimestamp { get; init; }

    public EventProjectorStateReadModel(
        long eventId,
        string actorName,
        string projectorName,
        bool isReplay,
        int attemptNumber,
        EventProjectorOutcomeType outcome,
        EventProjectorStageType stage,
        string errorMessage = "",
        DateTime createdTimestamp = default,
        DateTime updatedTimestamp = default)
    {
        EventId = eventId;
        ActorName = actorName;
        ProjectorName = projectorName;
        IsReplay = isReplay;
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        Stage = stage;
        ErrorMessage = errorMessage;
        CreatedTimestamp = createdTimestamp;
        UpdatedTimestamp = updatedTimestamp;
    }
}
