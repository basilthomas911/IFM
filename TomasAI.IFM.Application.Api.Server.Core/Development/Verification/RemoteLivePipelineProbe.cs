using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
/// <summary>Observes the independent backend and API actors without owning provider recovery.</summary>
public sealed class RemoteLivePipelineProbe(IMarketDataApi market, ActorRuntimeHealthCheck actors,
    IFuturesMarketSessionAuthority sessions, TimeProvider time) : ILivePipelineProbe
{
    public async Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = sessions.Current;
        var checks = new List<LivePipelineCheck> { new("Session authority", "session", session.IsValid ? "Healthy" : "Unhealthy", "Authoritative exchange session.", now) };
        var backend = await market.GetBackendStatusAsync(token).ConfigureAwait(false);
        checks.Add(new("MarketData Host", "control", backend.Ready ? "Healthy" : "Degraded", "Independent backend control response.", now));
        foreach (var dataset in backend.Datasets)
            checks.Add(new("MarketData dataset", dataset.Role + "/" + dataset.Dataset,
                dataset.Healthy ? "Healthy" : !dataset.Running && session.ActiveValueDate is null ? "Inactive" : "Degraded",
                dataset.Error ?? dataset.Lifecycle, now, Required: dataset.Role == "Futures") { RecoveryState = dataset.Lifecycle });
        var actorHealth = await actors.CheckHealthAsync(new HealthCheckContext(), token).ConfigureAwait(false);
        checks.Add(new("Actor routing", "API", actorHealth.Status == HealthStatus.Healthy ? "Healthy" : "Degraded", actorHealth.Description ?? "Actor runtime observation.", now));
        return new(now, session.ActiveValueDate, session.ActiveValueDate is null ? "Inactive" : checks.Where(x => x.Required).All(x => x.Status is "Healthy" or "Inactive") ? "Healthy" : "Degraded", checks);
    }
    public Task HardResetAsync(LivePipelineHealthSnapshot snapshot, CancellationToken token)
        => Task.FromException(new InvalidOperationException("Dataset recovery belongs to MarketData Host."));
    public Task RecoverDownstreamAsync(LivePipelineCheck check, DateOnly date, CancellationToken token)
        => Task.FromException(new InvalidOperationException("Actor recovery belongs to its supervisor."));
}
