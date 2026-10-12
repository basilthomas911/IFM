using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Runs at most three isolated hard attempts and converts exhaustion into one typed result.</summary>
public sealed class DatabentoHardRecoveryEngine
{
    readonly Func<int, CancellationToken, Task<DatabentoHardAttemptResult>> attempt;
    readonly TimeProvider time;
    readonly DatabentoHardRecoveryPolicy policy;
    readonly ILogger<DatabentoHardRecoveryEngine> logger;

    public DatabentoHardRecoveryEngine(
        Func<int, CancellationToken, Task<DatabentoHardAttemptResult>> attempt,
        TimeProvider time, DatabentoHardRecoveryPolicy policy,
        ILogger<DatabentoHardRecoveryEngine>? logger = null)
    {
        this.attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        this.policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Validate();
        this.logger = logger ?? NullLogger<DatabentoHardRecoveryEngine>.Instance;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task<DatabentoHardRecoveryResult> ExecuteAsync(
        DatabentoHardRecoveryRequest request, CancellationToken applicationStopping)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
        deadline.CancelAfter(policy.OverallTimeout);
        var evidence = new List<DatabentoHardAttemptEvidence>(3);
        for (var number = 1; number <= 3; number++)
        {
            Task<DatabentoHardAttemptResult>? operation = null;
            if (applicationStopping.IsCancellationRequested)
                return Stopping(request, evidence);
            var delay = number switch
            {
                2 => policy.AttemptTwoDelay,
                3 => policy.AttemptThreeDelay,
                _ => TimeSpan.Zero
            };
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, time, deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
                { return Stopping(request, evidence); }
                catch (OperationCanceledException)
                {
                    return Failed(request, evidence.Count, "EpisodeDeadline",
                        "The episode deadline expired before another attempt could start.", evidence);
                }
            }
            var attemptId = Guid.NewGuid();
            var startedUtc = time.GetUtcNow();
            var started = time.GetTimestamp();
            ApplicationDiagnosticLog.Write18(logger, null, nameof(DatabentoHardRecoveryEngine), nameof(ExecuteAsync), request.CorrelationId, attemptId, number);
            try
            {
                operation = attempt(number, deadline.Token);
                var outcome = await operation.WaitAsync(deadline.Token).ConfigureAwait(false);
                evidence.Add(ToEvidence(number, attemptId, startedUtc,
                    time.GetElapsedTime(started), outcome));
                if (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning) == true) { RemainingApplicationDiagnosticLog.Write6(logger, null, nameof(DatabentoHardRecoveryEngine), nameof(ExecuteAsync), request.CorrelationId, attemptId, number, outcome.Succeeded, outcome.SafeToRetry, outcome.Stage, time.GetElapsedTime(started).TotalMilliseconds); }
                if (outcome.Succeeded)
                {
                    if (outcome.Workers.Count == 0
                        || outcome.Workers.Any(worker => worker.GenerationId == Guid.Empty)
                        || outcome.Workers.Select(worker => worker.Dataset)
                            .Distinct(StringComparer.Ordinal).Count() != outcome.Workers.Count)
                        return Failed(request, number, "LocalQualification",
                            "Qualified worker generation evidence is incomplete.", evidence);
                    var generations = outcome.Workers.ToDictionary(worker => worker.Dataset,
                        worker => worker.GenerationId, StringComparer.Ordinal);
                    return new(request.CorrelationId, generations.Values.FirstOrDefault(), number,
                        DatabentoHardRecoveryOutcome.DatabentoHealthy, string.Empty, "Locally qualified")
                    {
                        DatasetGenerations = generations,
                        AttemptEvidence = evidence.ToArray()
                    };
                }
                if (!outcome.SafeToRetry)
                    return Failed(request, number, outcome.Stage, "Generation isolation could not be proven.", evidence);
            }
            catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
            {
                ObserveLateFailure(operation);
                return Stopping(request, evidence);
            }
            catch (Exception exception)
            {
                ApplicationDiagnosticLog.Write19(logger, exception, nameof(DatabentoHardRecoveryEngine), nameof(ExecuteAsync), request.CorrelationId, attemptId, number);
                ObserveLateFailure(operation);
                // A throwing or non-cooperative attempt has not proved worker isolation.
                evidence.Add(new DatabentoHardAttemptEvidence(number, false, "AttemptBoundary",
                    exception.GetType().Name, Bound(exception.Message), [])
                {
                    AttemptId = attemptId,
                    StartedUtc = startedUtc,
                    Elapsed = time.GetElapsedTime(started)
                });
                return Failed(request, number, "AttemptBoundary", "Attempt isolation is unknown.", evidence);
            }
        }
        return Failed(request, 3, "AttemptExhaustion", "Three hard attempts failed.", evidence);
    }

    static void ObserveLateFailure(Task<DatabentoHardAttemptResult>? operation)
    {
        if (operation is null || operation.IsCompleted) return;
        _ = operation.ContinueWith(task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    static DatabentoHardAttemptEvidence ToEvidence(int number, Guid attemptId,
        DateTimeOffset startedUtc, TimeSpan elapsed, DatabentoHardAttemptResult outcome) =>
        new(number, outcome.SafeToRetry, outcome.Stage,
            outcome.Failure?.GetType().Name ?? string.Empty,
            Bound(outcome.Failure?.Message ?? string.Empty),
            outcome.Workers.Select(worker => worker.ProcessId).ToArray())
        {
            AttemptId = attemptId,
            StartedUtc = startedUtc,
            Elapsed = elapsed,
            DatasetGenerations = outcome.Workers.DistinctBy(worker => worker.Dataset, StringComparer.Ordinal)
                .ToDictionary(worker => worker.Dataset,
                worker => worker.GenerationId, StringComparer.Ordinal),
            CleanupFailureType = outcome.CleanupFailure?.GetType().Name ?? string.Empty,
            CleanupDetail = Bound(outcome.CleanupFailure?.Message ?? string.Empty)
        };

    static DatabentoHardRecoveryResult Failed(DatabentoHardRecoveryRequest request,
        int attempts, string stage, string detail, List<DatabentoHardAttemptEvidence> evidence) =>
        new(request.CorrelationId, Guid.Empty, attempts,
            DatabentoHardRecoveryOutcome.Unrecoverable, stage, detail)
        {
            AttemptEvidence = evidence.ToArray()
        };

    static DatabentoHardRecoveryResult Stopping(DatabentoHardRecoveryRequest request,
        List<DatabentoHardAttemptEvidence> evidence) =>
        new(request.CorrelationId, Guid.Empty, evidence.Count,
            DatabentoHardRecoveryOutcome.ApplicationStopping,
            "ApplicationStopping", "The API host stopped during recovery.")
        {
            AttemptEvidence = evidence.ToArray()
        };

    static string Bound(string value)
    {
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (!string.IsNullOrEmpty(key)) value = value.Replace(key, "[redacted]", StringComparison.Ordinal);
        return value.Length <= 2048 ? value : value[..2048];
    }
}
