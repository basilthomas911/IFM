using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public interface ILivePipelineProbe
{
    Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token);
    Task HardResetAsync(LivePipelineHealthSnapshot unhealthySnapshot, CancellationToken token);
    Task RecoverDownstreamAsync(LivePipelineCheck unhealthyCheck, DateOnly valueDate, CancellationToken token);
}

/// <summary>
/// Single owner of the minute audit. Dataset-owned upstream failures receive a five-minute
/// confirmation window. Downstream failures receive a one-minute confirmation window and
/// are recovered through their own lifecycle owner.
/// </summary>
public sealed class LivePipelineMonitor(ILivePipelineProbe probe, TimeProvider time,
    ILogger<LivePipelineMonitor> logger, LivePipelineEvidence? evidence = null,
    IStatusConsoleWriter? statusConsole = null, LivePipelineMonitorOptions? configuredOptions = null) : BackgroundService
{
    static readonly TimeSpan DownstreamRecoveryDelay = TimeSpan.FromMinutes(1);
    readonly LivePipelineMonitorOptions options = (configuredOptions ?? new()).Validate();
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<string, UpstreamRecovery> upstreamRecoveries = new(StringComparer.Ordinal);
    readonly Dictionary<string, DownstreamRecovery> downstreamRecoveries = new(StringComparer.Ordinal);
    string? lastAlert;
    LivePipelineHealthSnapshot current = new(default, null, "Unknown", []);
    readonly DateTime monitorStartedUtc = time.GetUtcNow().UtcDateTime;
    bool forcedHardResetRequested;
    DateTime? lastHardResetUtc;
    string lastHardResetState = "NotRequired";
    string? lastHardResetError;

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
            var now = time.GetUtcNow().UtcDateTime;

            if (next.ValueDate is null || next.Status == "Inactive")
            {
                upstreamRecoveries.Clear();
                downstreamRecoveries.Clear();
                lastHardResetUtc = null;
                lastHardResetState = "NotRequired";
                lastHardResetError = null;
                Publish(next);
                lastAlert = null;
                return;
            }

            var upstream = next.Checks.Where(IsHardResetTrigger).ToArray();
            UpdateUpstreamWindows(upstream, now);


            var recoverable = next.Checks
                .Where(check => DownstreamTarget(check) is not null)
                .GroupBy(check => DownstreamTarget(check)!, StringComparer.Ordinal)
                .Select(group => group.FirstOrDefault(check => !IsHealthy(check)) ?? group.First())
                .OrderBy(check => DownstreamRank(DownstreamTarget(check)!))
                .ToArray();
            UpdateDownstreamWindows(recoverable, now);
            Publish(Decorate(next));

            var dueUpstream = upstream
                .Where(check => !IsHealthy(check)
                    && upstreamRecoveries.TryGetValue(CheckKey(check), out var recovery)
                    && recovery.Attempts == 0
                    && now - recovery.UnhealthySinceUtc >= options.HardResetDelay)
                .ToArray();
            var forcedResetDue = options.ForceOneHardResetAfterStartup
                && !forcedHardResetRequested
                && now - monitorStartedUtc >= options.HardResetDelay;
            if (dueUpstream.Length > 0 || forcedResetDue)
            {
                forcedHardResetRequested = true;
                if (forcedResetDue)
                    logger.LogWarning(
                        "The configured five-minute startup boundary was reached. Performing the one-shot complete hard reset.");
                else
                    logger.LogWarning(
                        "Required live-pipeline health remained unhealthy for five minutes ({Components}). Performing a complete hard reset.",
                        string.Join(", ", dueUpstream.Select(CheckKey)));
                lastHardResetUtc = now;
                lastHardResetState = "HardResetInProgress";
                lastHardResetError = null;
                try
                {
                    using var resetDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    resetDeadline.CancelAfter(options.RecoveryObservationWindow);
                    await probe.HardResetAsync(next, resetDeadline.Token)
                        .WaitAsync(resetDeadline.Token).ConfigureAwait(false);
                    lastHardResetState = "HardResetCompletedAwaitingRecovery";
                    AdvanceUpstreamRecovery(now, lastHardResetState, null);
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    lastHardResetState = "HardResetFailed";
                    lastHardResetError = ex.Message;
                    AdvanceUpstreamRecovery(now, lastHardResetState, lastHardResetError);
                    logger.LogWarning(ex,
                        "Complete live-pipeline hard reset failed.");
                }
            }
            await RecoverDueDownstreamAsync(recoverable, next.ValueDate.Value, now, token).ConfigureAwait(false);
            MarkExpiredRecoveryWindows(upstream, now);

            await ReportStatusAsync(next, deadline.Token).ConfigureAwait(false);
            Publish(Decorate(next));
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            var now = time.GetUtcNow().UtcDateTime;
            var failed = new LivePipelineHealthSnapshot(now, current.ValueDate, "Unknown",
            [.. current.Checks.Where(x => x.Component != "Health monitor"),
                new("Health monitor", "minute cycle", "Unknown", ex.Message, now)]);
            Publish(failed);
            logger.LogWarning(ex, "Live pipeline audit or recovery failed; health remains unconfirmed.");
        }
        finally { gate.Release(); }
    }

    async Task RecoverDueDownstreamAsync(
        IReadOnlyList<LivePipelineCheck> checks,
        DateOnly valueDate,
        DateTime now,
        CancellationToken token)
    {
        foreach (var check in checks.Where(check => !IsHealthy(check)))
        {
            var target = DownstreamTarget(check)!;
            if (!downstreamRecoveries.TryGetValue(target, out var state)
                || state.Attempts > 0
                || now - state.UnhealthySinceUtc < DownstreamRecoveryDelay
                || state.LastAttemptUtc is { } last && now - last < DownstreamRecoveryDelay)
                continue;

            var attempt = state.Attempts + 1;
            try
            {
                using var recoveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                recoveryDeadline.CancelAfter(TimeSpan.FromSeconds(40));
                await probe.RecoverDownstreamAsync(check, valueDate, recoveryDeadline.Token)
                    .WaitAsync(recoveryDeadline.Token).ConfigureAwait(false);
                downstreamRecoveries[target] = state with
                {
                    Attempts = attempt,
                    LastAttemptUtc = now,
                    State = "Requested",
                    LastError = null
                };
                logger.LogWarning(
                    "Requested targeted downstream recovery for {Target} after its one-minute confirmation window. Attempt={Attempt}.",
                    target, attempt);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                downstreamRecoveries[target] = state with
                {
                    Attempts = attempt,
                    LastAttemptUtc = now,
                    State = "Failed",
                    LastError = ex.Message
                };
                logger.LogWarning(ex,
                    "Targeted downstream recovery failed for {Target}. Attempt={Attempt}.", target, attempt);
            }
        }
    }

    void UpdateUpstreamWindows(IReadOnlyList<LivePipelineCheck> checks, DateTime now)
    {
        var observed = checks.Select(CheckKey).ToHashSet(StringComparer.Ordinal);
        foreach (var key in upstreamRecoveries.Keys.Where(key => !observed.Contains(key)).ToArray())
            upstreamRecoveries.Remove(key);
        foreach (var check in checks)
        {
            var key = CheckKey(check);
            if (IsHealthy(check)) upstreamRecoveries.Remove(key);
            else upstreamRecoveries.TryAdd(key, NewUpstreamRecovery(now));
        }
    }

    UpstreamRecovery NewUpstreamRecovery(DateTime now)
    {
        if (lastHardResetUtc is { } resetUtc
            && now - resetUtc < options.RecoveryObservationWindow)
            return new(resetUtc, 1, resetUtc, lastHardResetState, lastHardResetError);
        return new(now, 0, null, "PendingHardReset", null);
    }

    void AdvanceUpstreamRecovery(DateTime now, string state, string? error)
    {
        foreach (var (key, recovery) in upstreamRecoveries.ToArray())
            upstreamRecoveries[key] = recovery with
            {
                UnhealthySinceUtc = now,
                Attempts = recovery.Attempts + 1,
                LastAttemptUtc = now,
                State = state,
                LastError = error
            };
    }

    void MarkExpiredRecoveryWindows(IReadOnlyList<LivePipelineCheck> checks, DateTime now)
    {
        foreach (var check in checks.Where(check => !IsHealthy(check)))
        {
            var key = CheckKey(check);
            if (!upstreamRecoveries.TryGetValue(key, out var recovery)
                || recovery.Attempts == 0
                || recovery.LastAttemptUtc is not { } attempted
                || now - attempted < options.RecoveryObservationWindow
                || recovery.State is "HardResetFailed" or "HardResetRecoveryFailed")
                continue;
            upstreamRecoveries[key] = recovery with
            {
                State = "HardResetRecoveryFailed",
                LastError = "The required component did not recover within five minutes of the hard reset."
            };
        }
    }
    void UpdateDownstreamWindows(IReadOnlyList<LivePipelineCheck> checks, DateTime now)
    {
        var observed = checks.Select(DownstreamTarget).Where(target => target is not null)
            .Select(target => target!).ToHashSet(StringComparer.Ordinal);
        foreach (var target in downstreamRecoveries.Keys.Where(target => !observed.Contains(target)).ToArray())
            downstreamRecoveries.Remove(target);
        foreach (var check in checks)
        {
            var target = DownstreamTarget(check)!;
            if (IsHealthy(check)) downstreamRecoveries.Remove(target);
            else downstreamRecoveries.TryAdd(target, new(now, 0, null, "Pending", null));
        }
    }

    LivePipelineHealthSnapshot Decorate(LivePipelineHealthSnapshot snapshot)
        => snapshot with { Checks = snapshot.Checks.Select(Decorate).ToArray() };

    LivePipelineCheck Decorate(LivePipelineCheck check)
    {
        if (IsDatasetOwnedUpstream(check) && !IsHealthy(check)
            && upstreamRecoveries.TryGetValue(CheckKey(check), out var upstream)
            && (upstream.Attempts > 0 || !HasAttemptedDownstreamRecovery(check)))
            return check with
            {
                RecoveryAttempts = upstream.Attempts,
                RecoveryState = upstream.LastError is null
                    ? upstream.State
                    : $"{upstream.State}: {upstream.LastError}",
                NextRecoveryUtc = upstream.Attempts == 0
                    ? upstream.UnhealthySinceUtc + options.HardResetDelay
                    : upstream.LastAttemptUtc + options.RecoveryObservationWindow
            };

        var target = DownstreamTarget(check);
        if (target is not null && !IsHealthy(check)
            && downstreamRecoveries.TryGetValue(target, out var downstream))
            return check with
            {
                RecoveryAttempts = downstream.Attempts,
                RecoveryState = downstream.LastError is null
                    ? downstream.State
                    : $"{downstream.State}: {downstream.LastError}",
                NextRecoveryUtc = (downstream.LastAttemptUtc ?? downstream.UnhealthySinceUtc) + DownstreamRecoveryDelay
            };
        return check;
    }

    bool HasAttemptedDownstreamRecovery(LivePipelineCheck check)
    {
        var target = DownstreamTarget(check);
        return target is not null
            && downstreamRecoveries.TryGetValue(target, out var state)
            && state.Attempts > 0;
    }

    async Task ReportStatusAsync(LivePipelineHealthSnapshot snapshot, CancellationToken token)
    {
        if (snapshot.Status == "Healthy")
        {
            lastAlert = null;
            return;
        }
        var pendingUpstream = upstreamRecoveries.Count == 0
            ? "No hard reset is pending."
            : $"Next hard-reset or recovery deadline: {upstreamRecoveries.Values.Min(value => value.Attempts == 0
                ? value.UnhealthySinceUtc + options.HardResetDelay
                : value.LastAttemptUtc!.Value + options.RecoveryObservationWindow):O}.";
        var alert = $"Live pipeline {snapshot.Status}. {pendingUpstream} Downstream recovery is handled per component.";
        if (lastAlert == alert) return;
        lastAlert = alert;
        if (statusConsole is null) return;
        try
        {
            await statusConsole.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, alert)
                .WaitAsync(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        { logger.LogWarning(ex, "Status-console delivery failed; pipeline recovery remains independent."); }
    }

    void Publish(LivePipelineHealthSnapshot snapshot)
    {
        Volatile.Write(ref current, snapshot);
        evidence?.PublishAudit(snapshot);
    }

    static bool IsHealthy(LivePipelineCheck check) => check.Status is "Healthy" or "Inactive";
    static string CheckKey(LivePipelineCheck check) => check.Component + "/" + check.Scope;

    static bool IsHardResetTrigger(LivePipelineCheck check)
        => check.Required
            && !check.Component.StartsWith("UI ", StringComparison.Ordinal)
            && check.Component is not "Deployment identity" and not "Session authority";

    static bool IsDatasetOwnedUpstream(LivePipelineCheck check) => IsHardResetTrigger(check);

    static string? DownstreamTarget(LivePipelineCheck check) => check.Component switch
    {
        "Bar timer" or "Chart storage/query" => "Chart bars",
        "Analytics attachments" or "Analytics processing" =>
            check.Scope.Split('/', 2)[0] is "RSI" or "ATR" or "ADX" or "MACD"
                ? check.Scope
                : null,
        "ITI route" or "ITI" => "ITI",
        "Market Outlook inputs" or "Market Outlook publication" or "Market Outlook storage" => "Market Outlook",
        _ => null
    };

    static int DownstreamRank(string target)
    {
        if (target == "Chart bars") return 0;
        if (target.StartsWith("RSI/", StringComparison.Ordinal)) return 1;
        if (target.StartsWith("ATR/", StringComparison.Ordinal)) return 2;
        if (target.StartsWith("ADX/", StringComparison.Ordinal)) return 3;
        if (target.StartsWith("MACD/", StringComparison.Ordinal)) return 4;
        if (target == "ITI") return 5;
        return 6;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
    }

    sealed record DownstreamRecovery(
        DateTime UnhealthySinceUtc,
        int Attempts,
        DateTime? LastAttemptUtc,
        string State,
        string? LastError);

    sealed record UpstreamRecovery(
        DateTime UnhealthySinceUtc,
        int Attempts,
        DateTime? LastAttemptUtc,
        string State,
        string? LastError);
}
