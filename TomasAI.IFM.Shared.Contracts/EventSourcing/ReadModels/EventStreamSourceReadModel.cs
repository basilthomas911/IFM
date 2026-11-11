namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

/// <summary>A persisted event with its stream name for explicitly scoped maintenance replay.</summary>
/// <param name="Event">The original persisted event and committed revision.</param>
/// <param name="Stream">The original owning stream identity.</param>
public sealed record EventStreamSourceReadModel(EventLogReadModel Event, string Stream);
