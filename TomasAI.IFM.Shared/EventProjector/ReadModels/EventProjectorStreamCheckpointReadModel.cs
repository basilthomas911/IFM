namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// Records the highest source-stream version whose target mutation has been accepted by one projector.
/// </summary>
public sealed record EventProjectorStreamCheckpointReadModel(
    string ProjectorName,
    long EventStreamId,
    long LastAppliedStreamVersion,
    long LastAppliedEventId,
    long Revision,
    DateTime UpdatedAtUtc);
