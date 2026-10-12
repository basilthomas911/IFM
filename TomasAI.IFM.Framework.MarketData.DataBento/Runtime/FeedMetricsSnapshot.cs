namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedMetricsSnapshot
{
    public required DateTimeOffset ObservedAt { get; init; }
    public required FeedHealthSnapshot Health { get; init; }
    public required FeedMetricSeverity Severity { get; init; }
    public required IReadOnlyList<string> Conditions { get; init; }
    public required FeedReadinessState Readiness { get; init; }
    public bool EntryGateOpen => Readiness == FeedReadinessState.Ready;
}
