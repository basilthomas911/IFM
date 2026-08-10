using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// One joined event-log/state row returned by bounded keyset recovery.
/// </summary>
public sealed record EventProjectorRecoveryItemReadModel(
    EventLogReadModel EventLog,
    EventProjectorExecutionStateReadModel State);
