namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public readonly record struct ReferenceProjectionBackfillResult(long ScheduledJobs);

public readonly record struct ReferenceProjectionReconciliationResult(
    long SourceScheduledJobs,
    long ProjectedScheduledJobs,
    long MissingScheduledJobs,
    long UnexpectedScheduledJobs,
    long TokenlessScheduledJobReservations = 0)
{
    public bool IsConsistent
        => MissingScheduledJobs == 0
            && UnexpectedScheduledJobs == 0
            && TokenlessScheduledJobReservations == 0;
}
