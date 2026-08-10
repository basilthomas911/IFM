namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Signals that a durable projector delivery must be retried because an earlier event in the same source stream
/// remains unresolved. This is flow control, not a failed projection attempt.
/// </summary>
public sealed class EventProjectorStreamOrderDeferredException(
    string projectorName,
    long eventId) : EventProjectorDeliveryDeferredException(
        projectorName,
        eventId,
        "stream-order",
        $"Projection of event {eventId} by '{projectorName}' is deferred until the earlier event in its source stream is resolved.");
