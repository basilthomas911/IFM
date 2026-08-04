namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public readonly record struct ReferenceProjectionBackfillResult(
    long EconomicCalendars,
    long ScheduledJobs);

public readonly record struct ReferenceProjectionReconciliationResult(
    long SourceEconomicCalendars,
    long ProjectedEconomicCalendars,
    long MissingEconomicCalendars,
    long UnexpectedEconomicCalendars,
    long SourceScheduledJobs,
    long ProjectedScheduledJobs,
    long MissingScheduledJobs,
    long UnexpectedScheduledJobs,
    long TokenlessScheduledJobReservations = 0)
{
    public bool IsConsistent
        => MissingEconomicCalendars == 0
            && UnexpectedEconomicCalendars == 0
            && MissingScheduledJobs == 0
            && UnexpectedScheduledJobs == 0
            && TokenlessScheduledJobReservations == 0;
}
