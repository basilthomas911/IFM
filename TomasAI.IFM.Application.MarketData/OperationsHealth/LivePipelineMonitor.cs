using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;
/// <summary>
/// Single owner of the minute audit. Dataset-owned upstream failures receive a five-minute
/// confirmation window. Downstream failures receive a one-minute confirmation window and
/// are recovered through their own lifecycle owner.
/// </summary>
public sealed class LivePipelineMonitor(ILivePipelineProbe probe, TimeProvider time, ILogger<LivePipelineMonitor> logger, LivePipelineEvidence? evidence = null, IStatusConsoleWriter? statusConsole = null, LivePipelineMonitorOptions? configuredOptions = null) : BackgroundService
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
    bool recoveryEpisodeTerminal;
    int optionHardResetAttempts;
    DateTime? optionHealthySinceUtc;
    public LivePipelineHealthSnapshot Current
    {
        get
        {
            var value = Volatile.Read(ref current);
            if (time.GetUtcNow().UtcDateTime - value.ObservedUtc <= TimeSpan.FromSeconds(90))
                return value;
            return value with
            {
                Status = "Unknown",
                Checks = value.Checks.Append(new LivePipelineCheck("Health monitor", "minute cycle", "Unknown", "Health observation is overdue.", time.GetUtcNow().UtcDateTime)).ToArray()
            };
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task CheckOnceAsync(CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token).ConfigureAwait(false))
            return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            var next = await probe.CheckAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            var now = time.GetUtcNow().UtcDateTime;
            if (next.ValueDate is null || string.Equals(next.Status, "Inactive", StringComparison.Ordinal))
            {
                upstreamRecoveries.Clear();
                downstreamRecoveries.Clear();
                lastHardResetUtc = null;
                lastHardResetState = "NotRequired";
                lastHardResetError = null;
                optionHardResetAttempts = 0;
                optionHealthySinceUtc = null;
                Publish(next);
                lastAlert = null;
                return;
            }

            var upstream = next.Checks.Where(IsHardResetTrigger).ToArray();
            UpdateUpstreamWindows(upstream, now);
            var recoverable = next.Checks.Where(check => check.Required && !string.Equals(check.Component, "Market Outlook inputs", StringComparison.Ordinal) && DownstreamTarget(check)is not null).GroupBy(check => DownstreamTarget(check)!, StringComparer.Ordinal).Select(group => group.FirstOrDefault(check => !IsHealthy(check)) ?? group.First()).OrderBy(check => DownstreamRank(DownstreamTarget(check)!)).ToArray();
            UpdateDownstreamWindows(recoverable, now);
            if (!recoveryEpisodeTerminal && optionHardResetAttempts >= options.MaximumHardResetAttempts
                && lastHardResetUtc is { } finalAttempt && now - finalAttempt >= options.RecoveryObservationWindow
                && recoverable.Any(check => check.Component == "Option quote processing" && !IsHealthy(check)))
            {
                recoveryEpisodeTerminal = true;
                await probe.ShutdownAsync(next, optionHardResetAttempts, token).ConfigureAwait(false);
                return;
            }
            Publish(Decorate(next));
            LogUnhealthyChecks(next);
            // A completed feed reset gets its full observation window. Old component retry
            // counters must not launch another reset or replace actors from pre-reset evidence.
            var observingReset = lastHardResetUtc is { } resetUtc && now - resetUtc < options.RecoveryObservationWindow;
            var dueUpstream = upstream.Where(check => !recoveryEpisodeTerminal && !IsHealthy(check) && upstreamRecoveries.TryGetValue(CheckKey(check), out var recovery) && recovery.Attempts < options.MaximumHardResetAttempts && (recovery.Attempts == 0 ? now - recovery.UnhealthySinceUtc >= options.HardResetDelay : recovery.State is "HardResetFailed" or "HardResetRecoveryFailed" && recovery.LastAttemptUtc is { } attempted && now - attempted >= options.RecoveryObservationWindow)).ToArray();
            var exhaustedDownstream = downstreamRecoveries.Where(pair => recoverable.Any(check => DownstreamTarget(check) == pair.Key && !IsHealthy(check)) && pair.Value.Attempts >= 3 && pair.Value.State is "Failed" or "Requested" && pair.Value.LastAttemptUtc is { } last && now - last >= DownstreamRecoveryDelay).Select(pair => pair.Key).ToArray();
            var forcedResetDue = options.ForceOneHardResetAfterStartup && !forcedHardResetRequested && now - monitorStartedUtc >= options.HardResetDelay;
            if (!recoveryEpisodeTerminal && !observingReset && (dueUpstream.Length > 0 || exhaustedDownstream.Length > 0 || forcedResetDue))
            {
                if (exhaustedDownstream.Contains("Option quotes", StringComparer.Ordinal))
                {
                    if (optionHardResetAttempts >= options.MaximumHardResetAttempts)
                    {
                        recoveryEpisodeTerminal = true;
                        await probe.ShutdownAsync(next, optionHardResetAttempts, token).ConfigureAwait(false);
                        return;
                    }
                    optionHardResetAttempts++;
                }
                forcedHardResetRequested = true;
                foreach (var target in exhaustedDownstream)
                    downstreamRecoveries[target] = downstreamRecoveries[target] with
                    {
                        State = "EscalatedToHardReset",
                        LastAttemptUtc = now
                    };
                if (exhaustedDownstream.Length > 0)
                    ApplicationDiagnosticLog.Write5(logger, null, nameof(CheckOnceAsync), string.Join(", ", exhaustedDownstream));
                if (forcedResetDue)
                    ApplicationDiagnosticLog.Write6(logger, null, nameof(LivePipelineMonitor), nameof(CheckOnceAsync));
                else if (dueUpstream.Length > 0)
                    ApplicationDiagnosticLog.Write7(logger, null, nameof(LivePipelineMonitor), nameof(CheckOnceAsync), string.Join(", ", dueUpstream.Select(CheckKey)));
                lastHardResetUtc = now;
                lastHardResetState = "HardResetInProgress";
                lastHardResetError = null;
                try
                {
                    using var resetDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    resetDeadline.CancelAfter(options.RecoveryObservationWindow);
                    await probe.HardResetAsync(next, resetDeadline.Token).WaitAsync(resetDeadline.Token).ConfigureAwait(false);
                    lastHardResetState = "HardResetCompletedAwaitingRecovery";
                    var completedUtc = time.GetUtcNow().UtcDateTime;
                    lastHardResetUtc = completedUtc;
                    AdvanceUpstreamRecovery(completedUtc, lastHardResetState, null);
                    downstreamRecoveries.Clear();
                }
                catch (DatabentoRecoveryEpisodeStatusException ex)when (ex.Result.Outcome == DatabentoRecoveryRequestOutcome.AlreadyInProgress)
                {
                    lastHardResetState = "HardResetInProgress";
                    AdvanceUpstreamRecovery(now, lastHardResetState, null);
                    ApplicationDiagnosticLog.Write8(logger, null, nameof(LivePipelineMonitor), nameof(CheckOnceAsync), ex.Result.CorrelationId);
                }
                catch (DatabentoRecoveryEpisodeStatusException ex)
                {
                    recoveryEpisodeTerminal = ex.Result.Outcome is DatabentoRecoveryRequestOutcome.Unrecoverable or DatabentoRecoveryRequestOutcome.ApplicationStopping;
                    lastHardResetState = ex.Result.Outcome switch
                    {
                        DatabentoRecoveryRequestOutcome.DatabentoHealthyDownstreamDegraded => "EpisodeDownstreamDegraded",
                        DatabentoRecoveryRequestOutcome.Unrecoverable => "EpisodeUnrecoverable",
                        _ => "EpisodeApplicationStopping"
                    };
                    lastHardResetError = ex.Result.Detail;
                    AdvanceUpstreamRecovery(now, lastHardResetState, lastHardResetError);
                    ApplicationDiagnosticLog.Write9(logger, ex, nameof(LivePipelineMonitor), nameof(CheckOnceAsync));
                }
                catch (Exception ex)when (!token.IsCancellationRequested)
                {
                    lastHardResetState = "HardResetFailed";
                    lastHardResetError = ex.Message;
                    AdvanceUpstreamRecovery(now, lastHardResetState, lastHardResetError);
                    ApplicationDiagnosticLog.Write10(logger, ex, nameof(LivePipelineMonitor), nameof(CheckOnceAsync));
                }
                finally
                {
                    if (!recoveryEpisodeTerminal && exhaustedDownstream.Contains("Option quotes", StringComparer.Ordinal))
                        downstreamRecoveries.Remove("Option quotes");
                }
            }

            if (!recoveryEpisodeTerminal && lastHardResetUtc is null)
                await RecoverDueDownstreamAsync(recoverable, next.ValueDate.Value, now, token).ConfigureAwait(false);
            else if (!recoveryEpisodeTerminal && lastHardResetUtc is { } lastReset && time.GetUtcNow().UtcDateTime - lastReset >= options.RecoveryObservationWindow)
                await RecoverDueDownstreamAsync(recoverable, next.ValueDate.Value, now, token).ConfigureAwait(false);
            MarkExpiredRecoveryWindows(upstream, now);
            await ReportStatusAsync(next, deadline.Token).ConfigureAwait(false);
            Publish(Decorate(next));
        }
        catch (Exception ex)when (!token.IsCancellationRequested)
        {
            var now = time.GetUtcNow().UtcDateTime;
            var failed = new LivePipelineHealthSnapshot(now, current.ValueDate, "Unknown", [..current.Checks.Where(x => !string.Equals(x.Component, "Health monitor", StringComparison.Ordinal)), new("Health monitor", "minute cycle", "Unknown", ex.Message, now)]);
            Publish(failed);
            ApplicationDiagnosticLog.Write11(logger, ex, nameof(LivePipelineMonitor), nameof(CheckOnceAsync));
        }
        finally
        {
            gate.Release();
        }
    }

    async Task RecoverDueDownstreamAsync(IReadOnlyList<LivePipelineCheck> checks, DateOnly valueDate, DateTime now, CancellationToken token)
    {
        foreach (var check in checks.Where(check => check.Required && !IsHealthy(check)))
        {
            var target = DownstreamTarget(check)!;
            if (!downstreamRecoveries.TryGetValue(target, out var state) || (state.Attempts >= 3 || string.Equals(state.State, "EscalatedToHardReset", StringComparison.Ordinal)) || now - state.UnhealthySinceUtc < DownstreamRecoveryDelay || state.LastAttemptUtc is { } last && now - last < DownstreamRecoveryDelay)
                continue;
            var attempt = state.Attempts + 1;
            if (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning) == true)
            {
                RemainingApplicationDiagnosticLog.Write2(logger, null, nameof(RecoverDueDownstreamAsync), target, attempt, valueDate, check.Component, check.Scope, check.Status, check.Reason, check.ObservedUtc, check.LastProgressUtc);
            }

            try
            {
                using var recoveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                recoveryDeadline.CancelAfter(TimeSpan.FromSeconds(40));
                await probe.RecoverDownstreamAsync(check, valueDate, recoveryDeadline.Token).WaitAsync(recoveryDeadline.Token).ConfigureAwait(false);
                downstreamRecoveries[target] = state with
                {
                    Attempts = attempt,
                    LastAttemptUtc = now,
                    State = "Requested",
                    LastError = null
                };
                ApplicationDiagnosticLog.Write12(logger, null, nameof(LivePipelineMonitor), nameof(RecoverDueDownstreamAsync), target, attempt);
            }
            catch (Exception ex)when (!token.IsCancellationRequested)
            {
                downstreamRecoveries[target] = state with
                {
                    Attempts = attempt,
                    LastAttemptUtc = now,
                    State = "Failed",
                    LastError = ex.Message
                };
                ApplicationDiagnosticLog.Write13(logger, ex, nameof(LivePipelineMonitor), nameof(RecoverDueDownstreamAsync), target, attempt);
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
            if (IsHealthy(check))
                upstreamRecoveries.Remove(key);
            else
                upstreamRecoveries.TryAdd(key, NewUpstreamRecovery(now));
        }
    }

    UpstreamRecovery NewUpstreamRecovery(DateTime now)
    {
        if (lastHardResetUtc is { } resetUtc && now - resetUtc < options.RecoveryObservationWindow)
            return new(resetUtc, 1, resetUtc, lastHardResetState, lastHardResetError);
        return new(now, 0, null, "PendingHardReset", null);
    }

    void AdvanceUpstreamRecovery(DateTime now, string state, string? error)
    {
        foreach (var(key, recovery)in upstreamRecoveries.ToArray())
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
            if (!upstreamRecoveries.TryGetValue(key, out var recovery) || recovery.Attempts == 0 || recovery.LastAttemptUtc is not { } attempted || now - attempted < options.RecoveryObservationWindow || recovery.State is "HardResetFailed" or "HardResetRecoveryFailed" or "EpisodeDownstreamDegraded" or "EpisodeUnrecoverable" or "EpisodeApplicationStopping")
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
        var observed = checks.Select(DownstreamTarget).Where(target => target is not null).Select(target => target!).ToHashSet(StringComparer.Ordinal);
        foreach (var target in downstreamRecoveries.Keys.Where(target => !observed.Contains(target)).ToArray())
            downstreamRecoveries.Remove(target);
        foreach (var check in checks)
        {
            var target = DownstreamTarget(check)!;
            if (IsHealthy(check))
            {
                if (target == "Option quotes")
                {
                    var attempted = optionHardResetAttempts > 0 || downstreamRecoveries.TryGetValue(target, out var priorOption) && priorOption.Attempts > 0;
                    if (attempted)
                    {
                        optionHealthySinceUtc ??= now;
                        if (now - optionHealthySinceUtc.Value < options.RecoveryObservationWindow) continue;
                    }
                    optionHardResetAttempts = 0;
                    optionHealthySinceUtc = null;
                }
                if ((downstreamRecoveries.Remove(target, out var prior) && prior.Attempts > 0) && (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information) == true))
                {
                    RemainingApplicationDiagnosticLog.Write3(logger, null, nameof(CheckOnceAsync), target, prior.Attempts, check.Component, check.Scope, check.Reason, check.LastProgressUtc);
                }
            }
            else
            {
                if (target == "Option quotes") optionHealthySinceUtc = null;
                downstreamRecoveries.TryAdd(target, new(now, 0, null, "Pending", null));
            }
        }
    }

    LivePipelineHealthSnapshot Decorate(LivePipelineHealthSnapshot snapshot)
    {
        var checks = snapshot.Checks.Select(Decorate).ToList();
        if (lastHardResetState is "EpisodeDownstreamDegraded" or "EpisodeUnrecoverable" or "EpisodeApplicationStopping")
        {
            checks.Add(new LivePipelineCheck("Recovery episode", "Databento downstream", "Degraded", lastHardResetError ?? lastHardResetState, snapshot.ObservedUtc) { RecoveryState = lastHardResetState });
            return snapshot with
            {
                Status = "Degraded",
                Checks = checks
            };
        }

        return snapshot with
        {
            Checks = checks
        };
    }

    LivePipelineCheck Decorate(LivePipelineCheck check)
    {
        if (IsDatasetOwnedUpstream(check) && !IsHealthy(check) && upstreamRecoveries.TryGetValue(CheckKey(check), out var upstream) && (upstream.Attempts > 0 || !HasAttemptedDownstreamRecovery(check)))
            return check with
            {
                RecoveryAttempts = upstream.Attempts,
                RecoveryState = upstream.LastError is null ? upstream.State : $"{upstream.State}: {upstream.LastError}",
                NextRecoveryUtc = (upstream.Attempts >= options.MaximumHardResetAttempts) switch
                {
                    true => null,
                    false => (upstream.Attempts == 0) switch
                    {
                        true => upstream.UnhealthySinceUtc + options.HardResetDelay,
                        false => upstream.LastAttemptUtc + options.RecoveryObservationWindow
                    }}
            };
        var target = DownstreamTarget(check);
        if (target is not null && !IsHealthy(check) && downstreamRecoveries.TryGetValue(target, out var downstream))
            return check with
            {
                RecoveryAttempts = downstream.Attempts,
                RecoveryState = downstream.LastError is null ? downstream.State : $"{downstream.State}: {downstream.LastError}",
                NextRecoveryUtc = (downstream.LastAttemptUtc ?? downstream.UnhealthySinceUtc) + DownstreamRecoveryDelay
            };
        return check;
    }

    bool HasAttemptedDownstreamRecovery(LivePipelineCheck check)
    {
        var target = DownstreamTarget(check);
        return target is not null && downstreamRecoveries.TryGetValue(target, out var state) && state.Attempts > 0;
    }

    async Task ReportStatusAsync(LivePipelineHealthSnapshot snapshot, CancellationToken token)
    {
        if (string.Equals(snapshot.Status, "Healthy", StringComparison.Ordinal))
        {
            lastAlert = null;
            return;
        }

        var pendingUpstream = (upstreamRecoveries.Count == 0) switch
        {
            true => "No hard reset is pending.",
            false => $"Next hard-reset or recovery deadline: {upstreamRecoveries.Values.Min(value => value.Attempts == 0 ? value.UnhealthySinceUtc + options.HardResetDelay : value.LastAttemptUtc!.Value + options.RecoveryObservationWindow):O}."};
        var alert = $"Live pipeline {snapshot.Status}. {pendingUpstream} Downstream recovery is handled per component.";
        if (string.Equals(lastAlert, alert, StringComparison.Ordinal))
            return;
        lastAlert = alert;
        if (statusConsole is null)
            return;
        try
        {
            await statusConsole.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, alert).WaitAsync(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }
        catch (Exception ex)when (!token.IsCancellationRequested)
        {
            ApplicationDiagnosticLog.Write14(logger, ex, nameof(LivePipelineMonitor), nameof(ReportStatusAsync));
        }
    }

    // One record per failing check per minute, never per tick. Preserve the evidence that
    // caused recovery rather than only logging that a replacement was requested.
    void LogUnhealthyChecks(LivePipelineHealthSnapshot snapshot)
    {
        foreach (var check in snapshot.Checks.Where(check => !IsHealthy(check)))
        {
            var detail = Decorate(check);
            if (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning) == true)
            {
                RemainingApplicationDiagnosticLog.Write4(logger, null, nameof(CheckOnceAsync), snapshot.ValueDate, check.Component, check.Scope, check.Status, check.Required, check.Reason, check.ObservedUtc, check.LastProgressUtc, detail.RecoveryAttempts, detail.RecoveryState, detail.NextRecoveryUtc);
            }
        }
    }

    void Publish(LivePipelineHealthSnapshot snapshot)
    {
        Volatile.Write(ref current, snapshot);
        evidence?.PublishAudit(snapshot);
    }

    static bool IsHealthy(LivePipelineCheck check) => check.Status is "Healthy" or "Inactive";
    static string CheckKey(LivePipelineCheck check) => check.Component + "/" + check.Scope;
    static bool IsHardResetTrigger(LivePipelineCheck check) => check.Required && !check.Component.StartsWith("UI ", StringComparison.Ordinal) && check.Component is not "Deployment identity" and not "Session authority" // Components with an explicit lifecycle owner are recovered independently. Escalating
    // them into a feed hard reset destroys accumulator progress and can create a permanent
    // warm-up loop while the upstream transport is otherwise healthy.
    && DownstreamTarget(check)is null;
    static bool IsDatasetOwnedUpstream(LivePipelineCheck check) => IsHardResetTrigger(check);
    static string? DownstreamTarget(LivePipelineCheck check) => check.Component switch
    {
        "Option quote processing" => "Option quotes",
        "Bar timer" or "Chart storage/query" => "Chart bars",
        "Analytics attachments" or "Analytics processing" => check.Scope.Split('/', 2)[0] is "RSI" or "ATR" or "ADX" or "MACD" ? check.Scope : null,
        "ITI route" or "ITI ingress" or "ITI durable completion" => "ITI",
        "Market Outlook inputs" or "Market Outlook publication" or "Market Outlook storage" => "Market Outlook",
        _ => null
    };
    static int DownstreamRank(string target)
    {
        if (string.Equals(target, "Chart bars", StringComparison.Ordinal))
            return 0;
        if (target.StartsWith("RSI/", StringComparison.Ordinal))
            return 1;
        if (target.StartsWith("ATR/", StringComparison.Ordinal))
            return 2;
        if (target.StartsWith("ADX/", StringComparison.Ordinal))
            return 3;
        if (target.StartsWith("MACD/", StringComparison.Ordinal))
            return 4;
        if (string.Equals(target, "ITI", StringComparison.Ordinal))
            return 5;
        return 6;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
    }

    sealed record DownstreamRecovery(DateTime UnhealthySinceUtc, int Attempts, DateTime? LastAttemptUtc, string State, string? LastError);
    sealed record UpstreamRecovery(DateTime UnhealthySinceUtc, int Attempts, DateTime? LastAttemptUtc, string State, string? LastError);
    /// <summary>Releases synchronization resources after the host stops this service.</summary>
    public override void Dispose()
    {
        base.Dispose();
        gate.Dispose();
    }
}
