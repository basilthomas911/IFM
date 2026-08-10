namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Describes how a target projection absorbs repeated delivery of the same source event.
/// </summary>
public enum EventProjectionIdempotencyStrategy : byte
{
    Unspecified = 0,
    NaturalKeyMutation = 1,
    TargetReceipt = 2,
    CommutativeOperation = 3
}
