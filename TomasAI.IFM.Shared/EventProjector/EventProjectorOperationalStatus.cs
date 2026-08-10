namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Bounded operator view over non-successful projector work.
/// </summary>
public enum EventProjectorOperationalStatus : byte
{
    Pending = 0,
    Failed = 1,
    Blocked = 2
}
