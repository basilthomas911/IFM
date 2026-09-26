namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Describes the legacy v2 trade-strategy-family storage shape used during catalog migration.</summary>
public sealed record LegacyTradeStrategyFamily(
    int TradeStrategyFamilyId,
    long DefinitionVersion,
    string SystemKey,
    string Name,
    TradeStrategyFamilyState State,
    DateTime CreatedOnUtc,
    string CreatedBy);

/// <summary>Reports the number of scheduled-job projections backfilled from canonical storage.</summary>
public readonly record struct ReferenceProjectionBackfillResult(long ScheduledJobs);

/// <summary>Reports reconciliation counts for canonical and projected Reference data.</summary>
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
