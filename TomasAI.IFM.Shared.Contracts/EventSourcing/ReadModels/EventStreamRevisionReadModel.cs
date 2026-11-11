namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

/// <summary>The exact persisted stream identity and committed revision.</summary>
public sealed record EventStreamRevisionReadModel(string Stream, long Revision);
