using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public interface ILivePipelineProbe
{
    Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token);
    Task RecoverAsync(LivePipelineCheck failure, CancellationToken token);
    bool CanRecover(LivePipelineCheck failure) => false;
}

/// <summary>Single owner of the minute audit. Recovery never marks evidence healthy.</summary>
public sealed class LivePipelineMonitor(ILivePipelineProbe probe, TimeProvider time,
    ILogger<LivePipelineMonitor> logger, LivePipelineEvidence? evidence = null,
    IStatusConsoleWriter? statusConsole = null) : BackgroundService
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<string, (int Attempts, DateTime Next)> retries = new();
    string? lastAlert;
    LivePipelineHealthSnapshot current = new(default, null, "Unknown", []);
    public LivePipelineHealthSnapshot Current
    {
        get
        {
            var value = Volatile.Read(ref current);
            if (time.GetUtcNow().UtcDateTime - value.ObservedUtc <= TimeSpan.FromSeconds(90)) return value;
            return value with { Status = "Unknown", Checks = value.Checks.Append(new LivePipelineCheck(
                "Health monitor", "minute cycle", "Unknown", "Health observation is overdue.", time.GetUtcNow().UtcDateTime)).ToArray() };
        }
    }
    public async Task CheckOnceAsync(CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token).ConfigureAwait(false)) return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            var next = await probe.CheckAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            foreach (var check in next.Checks.Where(x => x.Status == "Healthy")) retries.Remove(check.Component + "/" + check.Scope);
            next = next with { Checks = next.Checks.Select(check =>
            {
                var retry = retries.GetValueOrDefault(check.Component + "/" + check.Scope);
                return check.Status is "Degraded" or "Unhealthy" or "Unknown" ? check with
                {
                    RecoveryAttempts = retry.Attempts,
                    RecoveryState = !probe.CanRecover(check) || retry.Attempts >= 3 ? "OperatorRequired" : retry.Attempts > 0 ? "AwaitingProgress" : "Pending",
                    NextRecoveryUtc = retry.Attempts is > 0 and < 3 ? retry.Next : null
                } : check;
            }).ToArray() };
            Volatile.Write(ref current, next);
            evidence?.PublishAudit(next);
            // The probe orders dependencies. Repair only the first failed required stage.
            var failure = next.Checks.FirstOrDefault(x => x.Required && x.Status is "Degraded" or "Unhealthy")
                ?? next.Checks.FirstOrDefault(x => x.Required && x.Status == "Unknown");
            if (failure is null) { lastAlert = null; return; }
            var alert = $"Live pipeline {failure.Component}/{failure.Scope}: {failure.Status}. {failure.Reason} Recovery: {failure.RecoveryState}.";
            if (lastAlert != alert)
            {
                lastAlert = alert;
                if (statusConsole is not null)
                {
                    try { await statusConsole.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, alert).WaitAsync(TimeSpan.FromSeconds(2), deadline.Token).ConfigureAwait(false); }
                    catch (Exception ex) when (!deadline.IsCancellationRequested)
                    { logger.LogWarning(ex, "Status-console delivery failed; pipeline recovery remains independent."); }
                }
            }
            if (!probe.CanRecover(failure))
            {
                logger.LogWarning("Live pipeline {Component}/{Scope} requires operator attention: {Reason}", failure.Component, failure.Scope, failure.Reason);
                return;
            }
            var key = failure.Component + "/" + failure.Scope;
            var retry = retries.GetValueOrDefault(key);
            var now = time.GetUtcNow().UtcDateTime;
            if (retry.Attempts >= 3 || now < retry.Next) return;
            retries[key] = (retry.Attempts + 1, now.AddMinutes(Math.Pow(2, retry.Attempts)));
            Volatile.Write(ref current, next with { Checks = next.Checks.Select(check => check == failure
                ? check with { RecoveryAttempts = retry.Attempts + 1, RecoveryState = "AwaitingProgress", NextRecoveryUtc = retry.Attempts + 1 < 3 ? retries[key].Next : null } : check).ToArray() });
            logger.LogWarning("Live pipeline {Component}/{Scope}: {Reason}. Recovery attempt {Attempt} of 3.", failure.Component, failure.Scope, failure.Reason, retry.Attempts + 1);
            await probe.RecoverAsync(failure, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            var now = time.GetUtcNow().UtcDateTime;
            Volatile.Write(ref current, new(now, current.ValueDate, "Unknown", [.. current.Checks.Where(x => x.Component != "Health monitor"),
                new("Health monitor", "minute cycle", "Unknown", ex.Message, now)]));
            logger.LogWarning(ex, "Live pipeline audit or recovery failed; health remains unconfirmed.");
            evidence?.PublishAudit(current);
        }
        finally { gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
    }
}
