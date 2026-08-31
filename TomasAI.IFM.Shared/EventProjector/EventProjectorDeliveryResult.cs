namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Describes how a durable projector queue should settle the current delivery.
/// </summary>
public readonly record struct EventProjectorDeliveryResult(
    EventProjectorDeliveryDisposition Disposition,
    string Reason)
{
    /// <summary>
    /// The projector finished handling the delivery and the queue may acknowledge it.
    /// </summary>
    public static EventProjectorDeliveryResult Completed { get; } = new(
        EventProjectorDeliveryDisposition.Completed,
        string.Empty);

    /// <summary>
    /// The projector cannot handle the delivery yet and the queue must request redelivery.
    /// </summary>
    public static EventProjectorDeliveryResult Deferred(string reason) => new(
        EventProjectorDeliveryDisposition.Deferred,
        reason ?? string.Empty);

    public bool IsDeferred => Disposition == EventProjectorDeliveryDisposition.Deferred;
}

/// <summary>
/// Identifies whether the current projector delivery completed or must be delivered again later.
/// </summary>
public enum EventProjectorDeliveryDisposition
{
    Completed = 0,
    Deferred = 1
}
