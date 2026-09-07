using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Opts a private event into atomic event/initial-projector-state persistence.
/// Recovery can then discover a committed event even if enqueue never happened.</summary>
public interface IRequireDurableProjection
{
    DurableProjectionRequirement RequiredProjection { get; }
}

public sealed record DurableProjectionRequirement(string ActorName, string ProjectorName, EventProjectorStageType InitialStage);
