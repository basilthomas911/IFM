namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Durable delivery state for one projector publication effect.
/// </summary>
public enum EventProjectorOutboxStatus : byte
{
    Pending = 0,
    Publishing = 1,
    Retrying = 2,
    Published = 3,
    Failed = 4
}
