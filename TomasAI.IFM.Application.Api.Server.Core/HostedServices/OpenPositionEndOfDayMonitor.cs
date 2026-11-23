using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Seals and audits every open position at startup, session close and rollover, independently of feed intent.</summary>
public sealed class OpenPositionEndOfDayMonitor(OpenPositionEndOfDaySweep sweep, IActorSupervisor supervisor,
    TimeProvider clock, ILogger<OpenPositionEndOfDayMonitor> logger) : BackgroundService, IHealthCheck
{
    sealed record State(OpenPositionEndOfDayAudit? Audit, string Error);
    State state = new(null, string.Empty);
    readonly SemaphoreSlim admission = new(1, 1);

    public async Task AuditAsync(CancellationToken token)
    {
        if (!await admission.WaitAsync(0, token).ConfigureAwait(false)) return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var audit = await sweep.RunAsync(deadline.Token).ConfigureAwait(false);
            Volatile.Write(ref state, new(audit, string.Empty));
            logger.LogInformation("Open-position EOD audit: ValueDate={ValueDate}; Positions={Positions}; CatchUpAttempts={Attempts}; Recovered={Recovered}; Failed={Failed}.",
                audit.ValueDate, audit.OpenPositions, audit.CatchUpAttempts, audit.Recovered, audit.FailedPositions);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Volatile.Write(ref state, new(Volatile.Read(ref state).Audit, error.Message));
            logger.LogError(error, "Open-position EOD sweep failed; readiness remains unqualified.");
        }
        finally { admission.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        DateTimeOffset next = DateTimeOffset.MinValue;
        try
        {
            do
            {
                if (!supervisor.IsReady) continue;
                var current = Volatile.Read(ref state).Audit;
                if (clock.GetUtcNow() < next && current?.ValueDate == sweep.LastCompletedValueDate()) continue;
                await AuditAsync(stoppingToken).ConfigureAwait(false);
                next = clock.GetUtcNow().AddSeconds(30);
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var current = Volatile.Read(ref state);
        return Task.FromResult(Evaluate(current.Audit, current.Error, sweep.LastCompletedValueDate(), clock.GetUtcNow()));
    }

    public static HealthCheckResult Evaluate(OpenPositionEndOfDayAudit? audit, string error, DateOnly expected, DateTimeOffset now)
    {
        var data = new Dictionary<string, object>
        {
            ["expectedValueDate"] = expected.ToString("yyyy-MM-dd"), ["error"] = error,
            ["auditedAtUtc"] = audit?.AuditedAtUtc.ToString("O") ?? string.Empty,
            ["auditedValueDate"] = audit?.ValueDate.ToString("yyyy-MM-dd") ?? string.Empty,
            ["openPositions"] = audit?.OpenPositions ?? 0, ["catchUpAttempts"] = audit?.CatchUpAttempts ?? 0,
            ["recovered"] = audit?.Recovered ?? 0, ["notRequired"] = audit?.NotRequired ?? 0,
            ["failedPositions"] = audit?.FailedPositions ?? 0, ["issues"] = audit?.Issues ?? []
        };
        if (error.Length > 0) return HealthCheckResult.Unhealthy("Open-position EOD audit failed: " + error, data: data);
        if (audit is null || audit.ValueDate != expected || now - audit.AuditedAtUtc > TimeSpan.FromMinutes(2))
            return HealthCheckResult.Degraded("Open-position EOD has not been audited for the latest completed session.", data: data);
        if (audit.FailedPositions > 0)
            return HealthCheckResult.Unhealthy(string.Join("; ", audit.Issues.Select(issue => $"{issue.PositionId} {issue.ValueDate:yyyy-MM-dd} {issue.Code}: {issue.Detail}")), data: data);
        return HealthCheckResult.Healthy("Every applicable open position has a durable EOD seal and qualified closing observations.", data);
    }

    public override void Dispose() { base.Dispose(); admission.Dispose(); }
}
