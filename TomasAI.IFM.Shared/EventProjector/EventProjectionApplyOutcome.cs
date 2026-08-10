namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Result of applying a source event through an idempotent target operation.
/// </summary>
public enum EventProjectionApplyOutcome : byte
{
    Applied = 0,
    AlreadyApplied = 1,
    Superseded = 2,
    Failed = 3
}
