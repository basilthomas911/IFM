using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Shared.EventSourcing;

public sealed record DurableProjectionRequirement(string ActorName, string ProjectorName, EventProjectorStageType InitialStage);
