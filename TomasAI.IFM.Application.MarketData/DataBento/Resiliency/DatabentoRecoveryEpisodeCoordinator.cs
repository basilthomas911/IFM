namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Joins concurrent requests to one recovery episode and closes after a terminal result.</summary>
public sealed class DatabentoRecoveryEpisodeCoordinator
{
    readonly System.Threading.Lock gate = new();
    readonly Func<DatabentoHardRecoveryRequest, CancellationToken, Task<DatabentoHardRecoveryResult>> execute;
    readonly CancellationToken applicationStopping;
    readonly TimeProvider time;
    readonly DatabentoRecoveryEpisodePolicy policy;
    readonly List<string> contributingReasons = [];
    Task<DatabentoHardRecoveryResult>? active;
    DatabentoHardRecoveryResult? terminal;
    (DateOnly ValueDate, Guid ExpectedGeneration, DatabentoHardRecoveryResult Result)? satisfied;
    DatabentoRecoveryEpisodeState state;

    public DatabentoRecoveryEpisodeState State { get { lock (gate) return state; } }
    public IReadOnlyList<string> ContributingReasons { get { lock (gate) return contributingReasons.ToArray(); } }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
    public DatabentoRecoveryEpisodeCoordinator(
        Func<DatabentoHardRecoveryRequest, CancellationToken, Task<DatabentoHardRecoveryResult>> execute,
        CancellationToken applicationStopping = default, TimeProvider? time = null,
        DatabentoRecoveryEpisodePolicy? policy = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.applicationStopping = applicationStopping;
        this.time = time ?? TimeProvider.System;
        this.policy = (policy ?? new DatabentoRecoveryEpisodePolicy()).Validate();
    }

    public Task<DatabentoHardRecoveryResult> RequestAsync(
        DatabentoHardRecoveryRequest request, CancellationToken callerCancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CorrelationId == Guid.Empty || request.ValueDate == default
            || string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 128
            || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2048)
            throw new ArgumentException("A hard recovery request requires bounded identity, value date, source and reason.",
                nameof(request));
        Task<DatabentoHardRecoveryResult> episode;
        lock (gate)
        {
            if (terminal is { } completed) return Task.FromResult(completed);
            if (active is not null)
            {
                AddReason(request);
                episode = active;
                return callerCancellation.CanBeCanceled ? episode.WaitAsync(callerCancellation) : episode;
            }
            if (applicationStopping.IsCancellationRequested)
            {
                TransitionTo(DatabentoRecoveryEpisodeState.ApplicationStopping);
                return Task.FromResult(new DatabentoHardRecoveryResult(request.CorrelationId,
                    Guid.Empty, 0, DatabentoHardRecoveryOutcome.ApplicationStopping,
                    "ApplicationStopping", "The API host is already stopping."));
            }
            if (satisfied is { } prior && prior.ValueDate == request.ValueDate
                && prior.ExpectedGeneration != Guid.Empty
                && prior.ExpectedGeneration == request.ExpectedGenerationId)
                return Task.FromResult(prior.Result);
            satisfied = null;
            contributingReasons.Clear();
            AddReason(request);
            TransitionTo(DatabentoRecoveryEpisodeState.Running);
            var completion = new TaskCompletionSource<DatabentoHardRecoveryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            active = completion.Task;
            _ = RunAsync(request, completion);
            episode = active;
        }
        return callerCancellation.CanBeCanceled ? episode.WaitAsync(callerCancellation) : episode;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
    async Task RunAsync(DatabentoHardRecoveryRequest request,
        TaskCompletionSource<DatabentoHardRecoveryResult> completion)
    {
        await Task.Yield();
        DatabentoHardRecoveryResult result;
        using var timeout = new CancellationTokenSource(policy.EpisodeTimeout, time);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping, timeout.Token);
        try
        {
            var operation = execute(request, lifetime.Token);
            try
            {
                result = await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                ObserveLateFailure(operation);
                throw;
            }
            if (result.CorrelationId != request.CorrelationId)
                throw new InvalidOperationException("Recovery result identity does not match the request.");
        }
        catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
        {
            result = new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty, 0,
                DatabentoHardRecoveryOutcome.ApplicationStopping,
                "ApplicationStopping", "The API host stopped during recovery.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            result = new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty, 0,
                DatabentoHardRecoveryOutcome.Unrecoverable, "EpisodeDeadline",
                "The bounded recovery episode deadline expired.");
        }
        catch (Exception exception)
        {
            result = new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty, 0,
                DatabentoHardRecoveryOutcome.Unrecoverable, "EpisodeBoundary", Bound(exception.Message));
        }
        lock (gate)
        {
            result = result with
            {
                ContributingReasons = Array.AsReadOnly(contributingReasons.ToArray())
            };
            if (result.Outcome == DatabentoHardRecoveryOutcome.Unrecoverable) terminal = result;
            if (result.Outcome == DatabentoHardRecoveryOutcome.DatabentoHealthy)
                satisfied = (request.ValueDate, request.ExpectedGenerationId, result);
            TransitionTo(result.Outcome switch
            {
                DatabentoHardRecoveryOutcome.DatabentoHealthy => DatabentoRecoveryEpisodeState.Healthy,
                DatabentoHardRecoveryOutcome.Unrecoverable => DatabentoRecoveryEpisodeState.Unrecoverable,
                _ => DatabentoRecoveryEpisodeState.ApplicationStopping
            });
            active = null;
            completion.SetResult(result);
        }
    }

    void AddReason(DatabentoHardRecoveryRequest request)
    {
        var reason = $"{request.Source}: {request.Reason}";
        if (contributingReasons.Count < policy.MaximumContributingReasons
            && !contributingReasons.Contains(reason, StringComparer.Ordinal))
            contributingReasons.Add(reason);
    }

    void TransitionTo(DatabentoRecoveryEpisodeState next)
    {
        if (state == next && next == DatabentoRecoveryEpisodeState.ApplicationStopping) return;
        var allowed = (state, next) switch
        {
            (DatabentoRecoveryEpisodeState.Idle, DatabentoRecoveryEpisodeState.Running) => true,
            (DatabentoRecoveryEpisodeState.Idle, DatabentoRecoveryEpisodeState.ApplicationStopping) => true,
            (DatabentoRecoveryEpisodeState.Healthy, DatabentoRecoveryEpisodeState.Running) => true,
            (DatabentoRecoveryEpisodeState.Healthy, DatabentoRecoveryEpisodeState.ApplicationStopping) => true,
            (DatabentoRecoveryEpisodeState.Running, DatabentoRecoveryEpisodeState.Healthy) => true,
            (DatabentoRecoveryEpisodeState.Running, DatabentoRecoveryEpisodeState.Unrecoverable) => true,
            (DatabentoRecoveryEpisodeState.Running, DatabentoRecoveryEpisodeState.ApplicationStopping) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Invalid recovery episode transition: {state} to {next}.");
        state = next;
    }

    static void ObserveLateFailure(Task<DatabentoHardRecoveryResult> operation)
    {
        if (operation.IsCompleted) return;
        _ = operation.ContinueWith(task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    static string Bound(string value)
    {
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (!string.IsNullOrEmpty(key)) value = value.Replace(key, "[redacted]", StringComparison.Ordinal);
        return value.Length <= 2048 ? value : value[..2048];
    }
}
