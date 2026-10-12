namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Bounded operator view over non-successful projector work.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum EventProjectorOperationalStatus : byte
{
    Pending = 0,
    Failed = 1,
    Blocked = 2
}
