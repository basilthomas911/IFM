namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Result of applying a source event through an idempotent target operation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum EventProjectionApplyOutcome : byte
{
    Applied = 0,
    AlreadyApplied = 1,
    Superseded = 2,
    Failed = 3
}
