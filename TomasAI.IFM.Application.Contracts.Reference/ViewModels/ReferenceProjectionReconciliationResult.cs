namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Reports reconciliation counts for canonical and projected Reference data.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct ReferenceProjectionReconciliationResult(
    long SourceScheduledJobs,
    long ProjectedScheduledJobs,
    long MissingScheduledJobs,
    long UnexpectedScheduledJobs,
    long TokenlessScheduledJobReservations = 0)
{
    /// <summary>Gets whether the canonical and projected Reference data are consistent.</summary>
    public bool IsConsistent
        => MissingScheduledJobs == 0
            && UnexpectedScheduledJobs == 0
            && TokenlessScheduledJobReservations == 0;
}
