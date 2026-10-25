namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Identifies whether the current projector delivery completed or must be delivered again later.
/// </summary>
public enum EventProjectorDeliveryDisposition
{
    Completed = 0,
    Deferred = 1
}
