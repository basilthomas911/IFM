namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Durable delivery state for one projector publication effect.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum EventProjectorOutboxStatus : byte
{
    Pending = 0,
    Publishing = 1,
    Retrying = 2,
    Published = 3,
    Failed = 4
}
