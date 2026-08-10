namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Bounded recovery inventory outcome used to publish projector readiness.
/// </summary>
public sealed record EventProjectorRecoveryResult(
    long Discovered,
    long Queued,
    long ClaimConflicts,
    long TerminalFailures);
