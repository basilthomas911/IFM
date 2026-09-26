namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>
/// Contains a bounded page of latest committed financial workflow snapshots.
/// </summary>
/// <param name="NextStreamId">The stream cursor for the next page.</param>
/// <param name="StreamsRead">The number of streams read.</param>
/// <param name="Snapshots">The recovered workflow snapshots.</param>
public sealed record FinancialWorkflowRecoveryPage(
    long NextStreamId,
    int StreamsRead,
    IReadOnlyList<WorkflowStrategyStateUpdatedEvent> Snapshots)
{
    /// <summary>Gets stream identifiers whose latest snapshot was invalid.</summary>
    public IReadOnlyList<long> InvalidStreamIds { get; init; } = [];
}
