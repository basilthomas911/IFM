namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Base signal for a durable projector delivery that remains valid but cannot run yet. Queue implementations must
/// redeliver it without consuming the projection failure budget.
/// </summary>
public class EventProjectorDeliveryDeferredException(
    string projectorName,
    long eventId,
    string reason,
    string message) : Exception(message)
{
    public string ProjectorName { get; } = projectorName;
    public long EventId { get; } = eventId;
    public string Reason { get; } = reason;
}
