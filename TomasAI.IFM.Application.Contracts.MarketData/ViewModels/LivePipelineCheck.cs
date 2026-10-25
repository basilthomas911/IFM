namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public sealed record LivePipelineCheck(string Component, string Scope, string Status, string Reason,
    DateTime ObservedUtc, DateTime? LastProgressUtc = null, bool Required = true)
{
    public int RecoveryAttempts { get; init; }
    public string RecoveryState { get; init; } = "NotRequired";
    public DateTime? NextRecoveryUtc { get; init; }
}
