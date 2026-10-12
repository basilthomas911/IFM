namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Describes how a target projection absorbs repeated delivery of the same source event.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum EventProjectionIdempotencyStrategy : byte
{
    Unspecified = 0,
    NaturalKeyMutation = 1,
    TargetReceipt = 2,
    CommutativeOperation = 3
}
