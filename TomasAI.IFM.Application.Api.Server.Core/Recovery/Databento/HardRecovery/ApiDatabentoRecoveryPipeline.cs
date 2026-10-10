using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Contracts;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.HardRecovery;

/// <summary>Finite deadlines for the complete hard-reset recovery sequence.</summary>
public sealed record ApiDatabentoRecoveryPipelinePolicy
{
    /// <summary>Maximum duration of the accepted recovery sequence.</summary>
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromMinutes(7);
    /// <summary>Maximum duration of Supervisor reconciliation.</summary>
    public TimeSpan SupervisorTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum duration of downstream proof or a shutdown request.</summary>
    public TimeSpan DownstreamTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum wait for the failure message to reach the System Console writer.</summary>
    public TimeSpan SystemConsoleTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Rejects unbounded or inconsistent recovery deadlines at startup.</summary>
    public ApiDatabentoRecoveryPipelinePolicy Validate()
    {
        if (OverallTimeout <= TimeSpan.Zero || OverallTimeout > TimeSpan.FromHours(1)
            || SupervisorTimeout <= TimeSpan.Zero || SupervisorTimeout >= OverallTimeout
            || DownstreamTimeout <= TimeSpan.Zero || DownstreamTimeout >= OverallTimeout
            || SystemConsoleTimeout <= TimeSpan.Zero || SystemConsoleTimeout > TimeSpan.FromSeconds(5))
            throw new InvalidOperationException("Recovery pipeline deadlines must be finite and nested.");
        return this;
    }
}

/// <summary>Owns the single sequential hard-reset recovery path and its terminal failure decision.</summary>
public sealed class ApiDatabentoRecoveryPipeline : IDatabentoRecoveryRequester
{
    readonly object gate = new();
    readonly IApiDatabentoRecoveryActions actions;
    readonly CancellationToken applicationStopping;
    readonly ApiDatabentoRecoveryPipelinePolicy policy;
    readonly ILogger<ApiDatabentoRecoveryPipeline> logger;
    bool recoveryActive;
    Guid activeCorrelationId;
    DatabentoRecoveryRequestResult? terminal;

    static readonly Action<ILogger, Guid, string, Exception?> ActionStarted =
        LoggerMessage.Define<Guid, string>(LogLevel.Information, new EventId(17400, "RecoveryActionStarted"),
            "Hard reset action starting. CorrelationId={CorrelationId}; Action={Action}");
    static readonly Action<ILogger, Guid, string, double, Exception?> ActionCompleted =
        LoggerMessage.Define<Guid, string, double>(LogLevel.Information, new EventId(17401, "RecoveryActionCompleted"),
            "Hard reset action completed. CorrelationId={CorrelationId}; Action={Action}; ElapsedMs={ElapsedMs}");
    static readonly Action<ILogger, Guid, string, Exception?> ActionFailed =
        LoggerMessage.Define<Guid, string>(LogLevel.Critical, new EventId(17402, "RecoveryActionFailed"),
            "Hard reset action failed. CorrelationId={CorrelationId}; Action={Action}");
    static readonly Action<ILogger, Guid, string, Exception?> NotificationFailed =
        LoggerMessage.Define<Guid, string>(LogLevel.Warning, new EventId(17403, "RecoveryNotificationFailed"),
            "Hard reset failure notification did not complete; API shutdown will continue. CorrelationId={CorrelationId}; Action={Action}");

    /// <summary>Creates the coordinator with explicit action implementations and process lifetime.</summary>
    public ApiDatabentoRecoveryPipeline(IApiDatabentoRecoveryActions actions,
        CancellationToken applicationStopping, ApiDatabentoRecoveryPipelinePolicy policy,
        ILogger<ApiDatabentoRecoveryPipeline> logger)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        this.applicationStopping = applicationStopping;
        this.policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Validate();
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Restarts only the owned feed: capture subscriptions, fence old generations,
    /// contain old workers, start replacements, confirm local
    /// readiness, start publication, and admit the replacements. Storage probes, Supervisor
    /// canary events, and test tick writes are excluded; normal health checks observe them.
    /// Essential recovery failure requests the existing bounded API shutdown.
    /// </summary>
    /// <param name="request">Correlation, source, reason, and authoritative value date.</param>
    /// <param name="cancellationToken">Allows withdrawal before acceptance. Once accepted,
    /// recovery uses its own deadline and host lifetime; caller cancellation cannot abandon it.</param>
    /// <returns>Success only after all actions complete, an ignored-request result while busy,
    /// or the terminal failure result after shutdown has been requested.</returns>
    public async Task<DatabentoRecoveryRequestResult> HardResetRecoveryAsync(
        DatabentoHardRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
            return new(request.CorrelationId, DatabentoRecoveryRequestOutcome.ApplicationStopping,
                null, "Caller cancelled before recovery was accepted.");

        // Acquire one owner for the complete sequence; competing callers perform no recovery actions.
        if (!TryAcquireRecovery(request, out var rejected))
            return rejected!;

        var context = new ApiDatabentoRecoveryContext(request);
        var action = "InitializeRecoveryDeadline";
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
            deadline.CancelAfter(policy.OverallTimeout);
            var token = deadline.Token;

            action = nameof(actions.CaptureRecoveryInputsAsync);
            var started = LogActionStarting(request, action);
            // Freeze the required manifests and value date without database or upstream lookups.
            await actions.CaptureRecoveryInputsAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.FenceFailedGenerationAsync);
            started = LogActionStarting(request, action);
            // Block the failed generations and reject an uncontained outstanding publisher send.
            await actions.FenceFailedGenerationAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            // Retry only replacement/local-readiness failures. Each retry first contains the
            // entire previous candidate group; failed containment is immediately terminal.
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                context.Attempt = attempt;
                context.Generations.Clear();
                context.HardResult = null;
                action = nameof(actions.StopDatabentoWorkersAsync);
                started = LogActionStarting(request, action);
                await actions.StopDatabentoWorkersAsync(context, token).WaitAsync(token).ConfigureAwait(false);
                LogActionCompleted(request, action, started);
                try
                {
                    action = nameof(actions.StartDatabentoWorkersAsync);
                    started = LogActionStarting(request, action);
                    await actions.StartDatabentoWorkersAsync(context, token).WaitAsync(token).ConfigureAwait(false);
                    LogActionCompleted(request, action, started);
                    action = nameof(actions.QualifyDatabentoAsync);
                    started = LogActionStarting(request, action);
                    await actions.QualifyDatabentoAsync(context, token).WaitAsync(token).ConfigureAwait(false);
                    LogActionCompleted(request, action, started);
                    break;
                }
                catch (Exception error) when (attempt < 3 && !token.IsCancellationRequested)
                {
                    try
                    {
                        logger.LogWarning(error,
                            "Feed replacement will retry; Method={Method}; CorrelationId={CorrelationId}; Attempt={Attempt}; Action={Action}; ValueDate={ValueDate}",
                            nameof(HardResetRecoveryAsync), request.CorrelationId, attempt, action, request.ValueDate);
                    }
                    catch (Exception loggingError) { WriteFallback(loggingError); }
                }
            }

            action = nameof(actions.StartPublisherAsync);
            started = LogActionStarting(request, action);
            // Ensure the isolated publisher can carry the candidate's downstream proof.
            await actions.StartPublisherAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);



            action = nameof(actions.AdmitGenerationAsync);
            started = LogActionStarting(request, action);
            // Open admission, release held publications, and establish lifecycle ownership of the recovered session.
            await actions.AdmitGenerationAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            return new(request.CorrelationId, DatabentoRecoveryRequestOutcome.FullyHealthy,
                context.HardResult, "Feed workers recovered and admitted; downstream health is observed independently.");
        }
        catch (Exception error)
        {
            // Preserve the failed action and original exception before attempting any notification.
            LogFailure(request, action, error);
            var failure = (context.HardResult ?? new DatabentoHardRecoveryResult(
                request.CorrelationId, Guid.Empty, context.Attempt, DatabentoHardRecoveryOutcome.Unrecoverable,
                action, string.Empty)) with
            {
                Outcome = DatabentoHardRecoveryOutcome.Unrecoverable,
                FailedStage = action,
                Detail = Bound(error.ToString()),
                DatasetGenerations = new Dictionary<string, Guid>(context.Generations)
            };
            var result = new DatabentoRecoveryRequestResult(request.CorrelationId,
                DatabentoRecoveryRequestOutcome.Unrecoverable, failure, failure.Detail);
            // Permanently reject new episodes before notifications or shutdown can yield.
            LatchTerminalFailure(result);

            try
            {
                action = nameof(actions.NotifySystemConsoleAsync);
                var started = LogActionStarting(request, action);
                // Tell the user which recovery action failed before the API begins shutdown.
                await actions.NotifySystemConsoleAsync(context, failure)
                    .WaitAsync(policy.SystemConsoleTimeout).ConfigureAwait(false);
                LogActionCompleted(request, action, started);
            }
            catch (Exception notificationError)
            {
                // Console delivery is best effort and must never prevent the shutdown action.
                LogNotificationFailure(request, action, notificationError);
            }

            try
            {
                action = nameof(actions.ShutdownApiAsync);
                var started = LogActionStarting(request, action);
                // Request the existing bounded fatal API shutdown without the cancelled recovery token.
                await actions.ShutdownApiAsync(context, failure)
                    .WaitAsync(policy.DownstreamTimeout).ConfigureAwait(false);
                LogActionCompleted(request, action, started);
            }
            catch (Exception shutdownError)
            {
                LogFailure(request, action, shutdownError);
                WriteFallback(shutdownError);
            }
            return result;
        }
        finally
        {
            // Release active ownership; the terminal result continues rejecting later requests.
            ReleaseRecovery();
        }
    }

    bool TryAcquireRecovery(DatabentoHardRecoveryRequest request,
        out DatabentoRecoveryRequestResult? rejected)
    {
        lock (gate)
        {
            if (terminal is { } failure) { rejected = failure; return false; }
            if (recoveryActive)
            {
                rejected = new(request.CorrelationId, DatabentoRecoveryRequestOutcome.AlreadyInProgress,
                    null, $"Request ignored; recovery {activeCorrelationId:D} is already running.");
                return false;
            }
            recoveryActive = true;
            activeCorrelationId = request.CorrelationId;
            rejected = null;
            return true;
        }
    }

    void LatchTerminalFailure(DatabentoRecoveryRequestResult result)
    {
        lock (gate) terminal = result;
    }

    void ReleaseRecovery()
    {
        lock (gate) { recoveryActive = false; activeCorrelationId = Guid.Empty; }
    }

    long LogActionStarting(DatabentoHardRecoveryRequest request, string action)
    {
        try { ActionStarted(logger, request.CorrelationId, action, null); }
        catch (Exception error) { WriteFallback(error); }
        return Stopwatch.GetTimestamp();
    }

    void LogActionCompleted(DatabentoHardRecoveryRequest request, string action, long started)
    {
        try { ActionCompleted(logger, request.CorrelationId, action,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, null); }
        catch (Exception error) { WriteFallback(error); }
    }

    void LogFailure(DatabentoHardRecoveryRequest request, string action, Exception error)
    {
        try { ActionFailed(logger, request.CorrelationId, action,
            new InvalidOperationException(Bound(error.ToString()))); }
        catch (Exception loggingError) { WriteFallback(error); WriteFallback(loggingError); }
    }

    void LogNotificationFailure(DatabentoHardRecoveryRequest request, string action, Exception error)
    {
        try { NotificationFailed(logger, request.CorrelationId, action,
            new InvalidOperationException(Bound(error.ToString()))); }
        catch (Exception loggingError) { WriteFallback(loggingError); }
    }

    static void WriteFallback(Exception error)
    {
        try { Console.Error.WriteLine("Hard reset recovery: " + Bound(error.ToString())); }
        catch (Exception) { }
    }

    static string Bound(string value)
    {
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (!string.IsNullOrEmpty(key)) value = value.Replace(key, "[redacted]", StringComparison.Ordinal);
        return value.Length <= 4096 ? value : value[..4096];
    }
}
