namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

/// <summary>The source head and one statement's selected checkpoint, legacy candidates or replay tail.</summary>
public sealed record AggregateCheckpointWindow(long Head, IReadOnlyCollection<AggregateCheckpointRow> Rows);

/// <summary>A versioned checkpoint or event selected in an aggregate window.</summary>
public sealed record AggregateCheckpointRow(long Head, AggregateCheckpointRowKind Kind, long SourceRevision, EventStreamReadModel? Event);

public enum AggregateCheckpointRowKind
{
    None,
    IndexedCheckpoint,
    LegacyCheckpoint,
    SourceEvent
}


/// <summary>A source event and the paired concern and legacy heads observed by its read statement.</summary>
public sealed record ConcernCheckpointRow(long ConcernHead, long LegacyHead, long CheckpointVersion,
    bool IsLegacy, bool IsCheckpoint, EventStreamReadModel? Event);
