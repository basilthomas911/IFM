namespace TomasAI.IFM.Shared.EventSourcing.ViewModels;

/// <summary>
/// Represents the identifier and name of an event stream for use in view models.
/// </summary>
/// <param name="EventStreamId">The unique identifier of the event stream. Must be greater than 0.</param>
/// <param name="EventStream">The name of the event stream. Cannot be null or empty.</param>
public record EventStreamIdReadModel(
    long EventStreamId,
    string EventStream)
{
    public bool IsValid => EventStreamId > 0 && !string.IsNullOrEmpty(EventStream);
}
