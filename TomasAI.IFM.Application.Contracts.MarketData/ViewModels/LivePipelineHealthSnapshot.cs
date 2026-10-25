namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public sealed record LivePipelineHealthSnapshot(DateTime ObservedUtc, DateOnly? ValueDate,
    string Status, IReadOnlyList<LivePipelineCheck> Checks)
{
    public bool AllowsNewDecisions => Checks.Count > 0 && Checks.Where(x => x.Required && !x.Component.StartsWith("UI", StringComparison.Ordinal))
        .All(x => x.Status is "Healthy" or "Inactive");
}
