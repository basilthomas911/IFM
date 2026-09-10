namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public sealed record LivePipelineCheck(string Component, string Scope, string Status, string Reason,
    DateTime ObservedUtc, DateTime? LastProgressUtc = null, bool Required = true)
{
    public int RecoveryAttempts { get; init; }
    public string RecoveryState { get; init; } = "NotRequired";
    public DateTime? NextRecoveryUtc { get; init; }
}

public sealed record LivePipelineHealthSnapshot(DateTime ObservedUtc, DateOnly? ValueDate,
    string Status, IReadOnlyList<LivePipelineCheck> Checks)
{
    public bool AllowsNewDecisions => Checks.Count > 0 && Checks.Where(x => x.Required && !x.Component.StartsWith("UI", StringComparison.Ordinal))
        .All(x => x.Status is "Healthy" or "Inactive");
}

// Receipt and rendering are reported separately. A server timestamp is used for heartbeat expiry.
public sealed record LiveUiHealthReport(Guid SiteId, DateOnly ValueDate,
    IReadOnlyList<LiveUiStreamEvidence> Streams);
public sealed record LiveUiStreamEvidence(string Symbol, DateTime? ReceivedBarUtc, DateTime? RenderedBarUtc,
    string ContractId = "");
