using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Supervises indicator initialization deadlines even while historical acquisition or feed readiness is blocked.</summary>
public sealed class IndicatorHealthMonitor(IIndicatorHealthReader reader, IFuturesMarketSessionAuthority sessions,
    TimeProvider clock, IStatusConsoleEventProducer console, IHostEnvironment environment,
    ILogger<IndicatorHealthMonitor> logger) : BackgroundService, IHealthCheck
{
    /// <summary>The full audit cadence after initialization has qualified.</summary>
    public static readonly TimeSpan AuditInterval = TimeSpan.FromMinutes(5);
    public const int ErrorId = 26901;
    readonly object gate = new();
    IndicatorHealthEntry[] entries = [];
    DateTimeOffset? auditedAt;
    string status = "Initializing";
    string failure = string.Empty;
    string persistenceFailure = string.Empty;
    double auditMilliseconds;
    readonly string statePath = Path.Combine(environment.ContentRootPath, ".artifacts", "indicator-health", environment.EnvironmentName + ".json");

    /// <summary>Persists unresolved incident age and notification identity across a process restart.</summary>
    public sealed record Incident(DateOnly ValueDate, DateTimeOffset StartedAtUtc, Guid Id, bool Notified, bool Qualified,
        IndicatorHealthEntry[]? Failures = null);

    /// <summary>Defines the exact five-minute startup deadline; periodic regressions fail in the discovering audit.</summary>
    public static bool IsOverdue(Incident incident, DateTimeOffset now) => incident.Qualified || now - incident.StartedAtUtc >= AuditInterval;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Incident? incident = Load();
        // A previously healthy process gets a fresh startup qualification budget.
        // Only an unresolved incident retains its age across restart.
        if (incident is { Qualified: true, Notified: false }) incident = null;
        if (incident?.Failures is { Length: > 0 } retained) entries = retained;
        Task<IndicatorHealthEntry[]>? audit = null;
        Task<IndicatorHealthEntry[]>? outstandingRead = null;
        DateOnly? auditDate = null;
        long auditStarted = 0;
        long auditFinished = 0;
        CancellationTokenSource? auditBudget = null;
        var nextAudit = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        try
        {
            do
            {
                var now = clock.GetUtcNow();
                var date = sessions.Current.ActiveValueDate;
                if (date is null)
                {
                    lock (gate) status = "ScheduledStopped";
                    continue;
                }
                if (incident?.ValueDate != date)
                {
                    if (auditBudget is not null) await auditBudget.CancelAsync().ConfigureAwait(false);
                    incident = new(date.Value, now, Guid.NewGuid(), false, false, []);
                    Save(incident);
                    nextAudit = DateTimeOffset.MinValue;
                    lock (gate) { status = "Initializing"; entries = []; auditedAt = null; failure = string.Empty; }
                }
                if (audit is { IsCompleted: true })
                {
                    try
                    {
                        var read = await audit.ConfigureAwait(false);
                        if (auditDate != date) continue;
                        var ready = read.Length > 0 && read.All(row => row.Ready);
                        lock (gate) { entries = read; auditedAt = now; auditMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(auditStarted, Interlocked.Read(ref auditFinished)).TotalMilliseconds;
                            failure = string.Empty; status = ready ? "Warm" : IsOverdue(incident, now) ? "Failed" : "Initializing"; }
                        logger.LogInformation("Indicator health audit completed: ValueDate={ValueDate}; Ready={Ready}; Total={Total}; ElapsedMilliseconds={ElapsedMilliseconds}; Failures={Failures}.",
                            date, read.Count(row => row.Ready), read.Length, auditMilliseconds, read.Where(row => !row.Ready).Select(row => $"{row.ContractId}/{row.TimeFrame}/{row.Indicator}: {row.Reason}").ToArray());
                        if (ready)
                        {
                            if (incident.Notified) await PublishAsync(incident, true, "All active indicator observations recovered and qualified.", stoppingToken).ConfigureAwait(false);
                            incident = incident with { Qualified = true, Notified = false, Failures = [],
                                Id = incident.Notified ? Guid.NewGuid() : incident.Id };
                            Save(incident);
                        }
                        else
                        {
                            incident = incident with { Failures = read.Where(row => !row.Ready).ToArray() };
                            Save(incident);
                        }
                        nextAudit = now + (ready ? AuditInterval : TimeSpan.FromSeconds(30));
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error)
                    {
                        logger.LogError(error, "Indicator health audit failed for {ValueDate}.", date);
                        lock (gate) { failure = error.Message; status = IsOverdue(incident, now) ? "Failed" : "Initializing"; }
                        nextAudit = now.AddSeconds(30);
                    }
                    finally { audit = null; auditBudget?.Dispose(); auditBudget = null; }
                }
                string currentStatus;
                lock (gate)
                {
                    if (status != "Warm" && IsOverdue(incident, now)) status = "Failed";
                    if (persistenceFailure.Length > 0) status = "Failed";
                    currentStatus = status;
                }
                if (currentStatus == "Failed" && !incident.Notified)
                {
                    var details = DescribeFailures();
                    if (await PublishAsync(incident, false, details, stoppingToken).ConfigureAwait(false))
                    { incident = incident with { Notified = true }; Save(incident); }
                }
                if (audit is null && (outstandingRead is null || outstandingRead.IsCompleted) && now >= nextAudit)
                {
                    auditBudget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    auditBudget.CancelAfter(TimeSpan.FromSeconds(30));
                    // WaitAsync bounds the monitor even if an implementation ignores cancellation.
                    auditDate = date;
                    auditStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    outstandingRead = ReadMeasuredAsync(date.Value, auditBudget.Token);
                    audit = outstandingRead.WaitAsync(auditBudget.Token);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            if (auditBudget is not null) { await auditBudget.CancelAsync().ConfigureAwait(false); auditBudget.Dispose(); }
            if (audit is not null) _ = audit.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        async Task<IndicatorHealthEntry[]> ReadMeasuredAsync(DateOnly valueDate, CancellationToken token)
        {
            try { return await reader.ReadAsync(valueDate, token).ConfigureAwait(false); }
            finally { Interlocked.Exchange(ref auditFinished, System.Diagnostics.Stopwatch.GetTimestamp()); }
        }
    }

    string DescribeFailures()
    {
        lock (gate)
            return persistenceFailure.Length > 0 ? "Cannot persist indicator health incidents: " + persistenceFailure
                : failure.Length > 0 ? failure : entries.Length == 0 ? "No completed indicator audit; initialization deadline exceeded."
                : string.Join("; ", entries.Where(row => !row.Ready).Select(row => $"{row.ContractId} {row.TimeFrame} {row.Indicator}: {row.Reason}; source={row.SourceAsOfUtc:O}, expected>={row.ExpectedAsOfUtc:O}"));
    }

    async Task<bool> PublishAsync(Incident incident, bool recovered, string message, CancellationToken token)
    {
        var at = clock.GetUtcNow();
        var source = LogSourceType.MarketDataFeedEvent;
        var text = recovered ? message : "Indicator initialization/health failure: " + message;
        if (!recovered) logger.LogError("{IndicatorFailure}", text);
        try
        {
            using var delivery = CancellationTokenSource.CreateLinkedTokenSource(token);
            delivery.CancelAfter(TimeSpan.FromSeconds(3));
            var id = recovered ? Guid.NewGuid() : incident.Id;
            var notice = new StatusConsoleLoggedEvent
            {
                Id = id, CommandId = id, EntityId = new ActorEntityId(source.ToString()),
                Subject = new(ActorType.Notify, StatusConsoleLoggedEvent.Actor, StatusConsoleLoggedEvent.Verb, source.ToString()),
                StatusConsoleLog = new(at.UtcDateTime, recovered ? 0 : ErrorId, source, text,
                    recovered ? "IndicatorHealthRecovered.v1" : "IndicatorHealthFailure.v1", incident.Id.ToString())
            };
            if (!console.IsRunning) await console.StartAsync(notice.Subject.ActorId, delivery.Token).AsTask().WaitAsync(delivery.Token).ConfigureAwait(false);
            await console.PublishConsoleAsync(notice).AsTask().WaitAsync(delivery.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { logger.LogWarning(error, "Indicator health notification delivery remains pending."); return false; }
    }

    Incident? Load()
    {
        try { return File.Exists(statePath) ? JsonSerializer.Deserialize<Incident>(File.ReadAllText(statePath)) : null; }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        { logger.LogError(error, "Indicator incident history could not be restored."); return null; }
    }

    void Save(Incident incident)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath + ".tmp", JsonSerializer.Serialize(incident));
            File.Move(statePath + ".tmp", statePath, true);
            lock (gate) persistenceFailure = string.Empty;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            lock (gate) persistenceFailure = error.Message;
            logger.LogError(error, "Indicator health incident persistence failed; monitoring and independent startup remain running.");
        }
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var data = new Dictionary<string, object> { ["state"] = status, ["auditedAtUtc"] = auditedAt?.ToString("O") ?? "",
                ["auditIntervalSeconds"] = AuditInterval.TotalSeconds, ["auditElapsedMilliseconds"] = auditMilliseconds,
                ["persistenceFailure"] = persistenceFailure,
                ["entries"] = entries, ["timeFrameCoverage"] = IndicatorHealthRegistry.TimeFrameCoverage,
                ["inactiveMetrics"] = IndicatorHealthRegistry.InactiveMetrics };
            return Task.FromResult(status switch
            {
                "Warm" or "ScheduledStopped" => HealthCheckResult.Healthy($"Indicators {status}.", data),
                "Failed" => HealthCheckResult.Unhealthy(DescribeFailures(), data: data),
                _ => HealthCheckResult.Degraded("Indicator initialization has not yet qualified every active output.", data: data)
            });
        }
    }
}
