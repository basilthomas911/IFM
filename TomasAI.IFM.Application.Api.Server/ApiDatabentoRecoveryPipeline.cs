using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.Api.Server;

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
    /// Executes every required hard-reset recovery action sequentially. Each action must
    /// succeed or throw; an action may perform its own finite retries before throwing.
    /// <list type="number">
    /// <item><description>Acquire exclusive recovery ownership; ignore concurrent requests.</description></item>
    /// <item><description>Capture and validate the in-memory subscription manifests and market session.</description></item>
    /// <item><description>Fence failed generations and verify publisher isolation.</description></item>
    /// <item><description>Drain and discard any previous candidate proof session.</description></item>
    /// <item><description>Stop or kill old Databento workers and prove their containment.</description></item>
    /// <item><description>Start replacement workers from the frozen manifests.</description></item>
    /// <item><description>Qualify the exact replacement generations locally.</description></item>
    /// <item><description>Prepare bounded candidate holding for downstream qualification.</description></item>
    /// <item><description>Qualify NATS, Redis, PostgreSQL, and ScyllaDB.</description></item>
    /// <item><description>Reconcile required actors and projectors through the Supervisor.</description></item>
    /// <item><description>Ensure the isolated downstream publisher is running.</description></item>
    /// <item><description>Prove durable downstream writes for every replacement dataset.</description></item>
    /// <item><description>Admit all qualified generations, hand their session and publisher back to lifecycle management, and return success.</description></item>
    /// <item><description>On failure, log the failed action and exception, attempt a bounded System Console message, request API shutdown, and return failure.</description></item>
    /// <item><description>Release active ownership; retain terminal rejection after any failure.</description></item>
    /// </list>
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

            action = nameof(actions.AbandonPreviousCandidateAsync);
            started = LogActionStarting(request, action);
            // Drain and remove old probe ownership before creating a replacement generation.
            await actions.AbandonPreviousCandidateAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.StopDatabentoWorkersAsync);
            started = LogActionStarting(request, action);
            // Stop or kill the exact owned workers and prove that old handlers have drained.
            await actions.StopDatabentoWorkersAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.StartDatabentoWorkersAsync);
            started = LogActionStarting(request, action);
            // Start fresh workers with fresh generation identities from the frozen manifests.
            await actions.StartDatabentoWorkersAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.QualifyDatabentoAsync);
            started = LogActionStarting(request, action);
            // Require local connection, subscriptions, freshness, and progress for every candidate.
            await actions.QualifyDatabentoAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.PrepareCandidateAsync);
            started = LogActionStarting(request, action);
            // Hold replacement publications until the remaining qualification actions succeed.
            await actions.PrepareCandidateAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.QualifyInfrastructureAsync);
            started = LogActionStarting(request, action);
            // Require all four downstream infrastructure families to pass their bounded probes.
            await actions.QualifyInfrastructureAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.ReconcileActorsAsync);
            started = LogActionStarting(request, action);
            // Require Supervisor confirmation that all required actors and projectors are healthy.
            await actions.ReconcileActorsAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.StartPublisherAsync);
            started = LogActionStarting(request, action);
            // Ensure the isolated publisher can carry the candidate's downstream proof.
            await actions.StartPublisherAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.ProveDownstreamWritesAsync);
            started = LogActionStarting(request, action);
            // Prove durable writes for held candidate ticks; quiet datasets have no tick to prove.
            await actions.ProveDownstreamWritesAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            action = nameof(actions.AdmitGenerationAsync);
            started = LogActionStarting(request, action);
            // Open admission, release held publications, and establish lifecycle ownership of the recovered session.
            await actions.AdmitGenerationAsync(context, token).WaitAsync(token).ConfigureAwait(false);
            LogActionCompleted(request, action, started);

            return new(request.CorrelationId, DatabentoRecoveryRequestOutcome.FullyHealthy,
                context.HardResult, "Every hard-reset recovery action completed successfully.");
        }
        catch (Exception error)
        {
            // Preserve the failed action and original exception before attempting any notification.
            LogFailure(request, action, error);
            var failure = (context.HardResult ?? new DatabentoHardRecoveryResult(
                request.CorrelationId, Guid.Empty, 1, DatabentoHardRecoveryOutcome.Unrecoverable,
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
