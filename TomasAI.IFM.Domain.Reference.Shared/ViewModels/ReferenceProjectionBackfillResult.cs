namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Reports the number of scheduled-job projections backfilled from canonical storage.</summary>
public readonly record struct ReferenceProjectionBackfillResult(long ScheduledJobs);
