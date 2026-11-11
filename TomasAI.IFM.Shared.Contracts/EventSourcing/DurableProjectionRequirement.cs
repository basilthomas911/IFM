using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Shared.EventSourcing;

public sealed record DurableProjectionRequirement
{
    public DurableProjectionRequirement(string actorName, string projectorName, EventProjectorStageType initialStage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (initialStage is not (EventProjectorStageType.PublishProcessingEvent or EventProjectorStageType.ApplyProjection))
            throw new ArgumentException("Invalid initial durable projection stage.", nameof(initialStage));
        ActorName = actorName;
        ProjectorName = projectorName;
        InitialStage = initialStage;
    }

    public string ActorName { get; }
    public string ProjectorName { get; }
    public EventProjectorStageType InitialStage { get; }
}
