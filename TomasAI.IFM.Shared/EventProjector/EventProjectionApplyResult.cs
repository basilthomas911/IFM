namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Explicit result returned by a target projection operation.
/// </summary>
public sealed record EventProjectionApplyResult(
    EventProjectionApplyOutcome Outcome,
    string ErrorMessage = "")
{
    public bool Success => Outcome is EventProjectionApplyOutcome.Applied
        or EventProjectionApplyOutcome.AlreadyApplied
        or EventProjectionApplyOutcome.Superseded;
}
