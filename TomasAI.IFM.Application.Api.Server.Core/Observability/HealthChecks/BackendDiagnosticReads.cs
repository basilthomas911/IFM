using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

/// <summary>Coalesces concurrent diagnostic requests only. Completed results are never cached or used for recovery decisions.</summary>
public sealed class BackendDiagnosticReads(IMarketDataApi market, IHostApplicationLifetime lifetime)
{
    readonly BoundedInFlight<int, MarketDataBackendStatus> status = new(1, TimeSpan.FromSeconds(3), lifetime.ApplicationStopping);
    readonly BoundedInFlight<int, MarketDataReadSnapshot> snapshot = new(1, TimeSpan.FromSeconds(3), lifetime.ApplicationStopping);

    public Task<MarketDataBackendStatus> StatusAsync(CancellationToken waiter)
        => status.RunAsync(0, token => market.GetBackendStatusAsync(token), waiter);

    public Task<MarketDataReadSnapshot> SnapshotAsync(CancellationToken waiter)
        => snapshot.RunAsync(0, token => market.GetBackendReadSnapshotAsync(token), waiter);
}
