using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server;

public static class LivePipelineEndpoints
{
    public static MarketDataOperationsHealthReadModel OperationsSnapshot(MarketDataOperationsHealthService operations, LivePipelineMonitor monitor)
    {
        var current = operations.GetReadModel();
        var pipeline = monitor.Current;
        var rows = pipeline.Checks.Select(check => new MarketDataOperationStageReadModel
        {
            Stage = check.Component + "/" + check.Scope, Status = Map(check.Status), Required = check.Required,
            ReasonCode = check.RecoveryState,
            Reason = check.Reason + (check.RecoveryAttempts > 0 ? $" Recovery attempts: {check.RecoveryAttempts}/3; {check.RecoveryState}." : ""),
            LastObservedUtc = check.ObservedUtc, LastSucceededUtc = check.LastProgressUtc
        });
        return current with
        {
            OverallStatus = Rank(current.OverallStatus) >= Rank(Map(pipeline.Status)) ? current.OverallStatus : Map(pipeline.Status),
            Stages = current.Stages.Concat(rows).ToArray()
        };
    }
    static int Rank(string status) => status switch { "Red" => 4, "Orange" => 3, "Yellow" => 2, "Green" => 1, _ => 0 };
    static string Map(string status) => status switch
    { "Healthy" => "Green", "Inactive" => "Inactive", "Unhealthy" => "Red", "Degraded" => "Yellow", _ => "Orange" };

    public static void MapLivePipelineHealth(this WebApplication app)
    {
        app.MapGet("/api/market-data/live-health", (LivePipelineMonitor health) => Results.Ok(health.Current));
        app.MapPost("/api/market-data/live-health/ui", (LiveUiHealthReport report, LivePipelineEvidence evidence)
            => evidence.ReportUi(report) ? Results.Ok() : Results.BadRequest());
    }
}
