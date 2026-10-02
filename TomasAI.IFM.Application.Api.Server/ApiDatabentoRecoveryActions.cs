using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Implements individual recovery actions; sequence and failure policy belong to the pipeline.</summary>
public sealed class ApiDatabentoRecoveryActions(
    DatasetDesiredSubscriptionRegistry desired,
    DatasetWorkerProcessRecoveryService workers,
    DatasetWorkerAdmissionRegistry admissions,
    IFuturesMarketSessionAuthority sessions,
    DatabentoSupervisedWorkerOptions workerOptions,
    SupervisedDatabentoHardRecoveryRuntime runtime,
    SupervisedDatabentoLifecycleRuntime lifecycle,
    ApiDatabentoHardRuntimePolicy runtimePolicy,
    ApiDatabentoRecoveryPipelinePolicy pipelinePolicy,
    ITickAggregationEventPublisher publisher,
    ITickAggregationPublisherDiagnostics publisherDiagnostics,
    DatabentoSoftRecoveryGate infrastructure,
    ISupervisorManagedActorLifecycle supervisor,
    SupervisorRecoveryCanaryProbe supervisorCanary,
    DatabentoCandidateTickStorageProof proof,
    IStatusConsoleWriter console,
    IApiFatalRecoveryShutdown shutdown,
    TimeProvider time) : IApiDatabentoRecoveryActions
{
    /// <inheritdoc />
    public Task CaptureRecoveryInputsAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var frozen = desired.CaptureCurrentRecoverySnapshot(context.Request.ValueDate, time);
        var session = sessions.Current;
        if (!session.IsValid || session.OperationalValueDate != frozen.ValueDate)
            throw new InvalidOperationException(
                $"CaptureRecoveryInputs: session value date {session.OperationalValueDate} does not match recovery {frozen.ValueDate}.");
        var age = time.GetUtcNow() - frozen.CapturedAtUtc;
        if (age < TimeSpan.Zero || age > runtimePolicy.MaximumManifestAge
            || frozen.Manifests.Any(manifest => !desired.IsCurrent(manifest)))
            throw new InvalidOperationException("CaptureRecoveryInputs: the frozen manifest set is stale or no longer current.");
        context.Frozen = frozen;
        context.LiveTrading = session.IsLiveTrading;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FenceFailedGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        foreach (var worker in workers.Current)
            admissions.Close(worker.Dataset, worker.GenerationId);
        RequirePublisherIsolation();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AbandonPreviousCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        proof.AbandonAsync(token);

    /// <inheritdoc />
    public async Task StopDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        var result = await workers.ContainForHardRecoveryAsync(runtimePolicy.ContainmentTimeout, token)
            .ConfigureAwait(false);
        if (!result.Isolated)
            throw new InvalidOperationException(
                $"StopDatabentoWorkers: worker containment failed after one bounded stop/kill action; PIDs={string.Join(",", result.ProcessIds)}.",
                result.Failure);
        RequirePublisherIsolation();
    }

    /// <inheritdoc />
    public async Task StartDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        RequirePublisherIsolation();
        foreach (var manifest in Frozen(context).Manifests)
        {
            token.ThrowIfCancellationRequested();
            if (!desired.IsCurrent(manifest))
                throw new InvalidOperationException($"StartDatabentoWorkers: manifest changed for {manifest.Dataset}.");
            try
            {
                var worker = await workers.StartCandidateAsync(workerOptions.CreateStartRequest(manifest), token)
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!worker.Running || worker.GenerationId == Guid.Empty)
                    throw new InvalidOperationException($"Candidate is not running; PID={worker.ProcessId}; detail={worker.Detail}.");
                context.Generations.Add(manifest.Dataset, worker.GenerationId);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"StartDatabentoWorkers failed for dataset {manifest.Dataset}, manifest revision {manifest.Revision}, start attempt 1.",
                    error);
            }
        }
    }

    /// <inheritdoc />
    public async Task QualifyDatabentoAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        var started = time.GetTimestamp();
        var startedUtc = time.GetUtcNow();
        var qualified = await runtime.QualifyCandidatesAsync(Frozen(context).Manifests,
            context.Generations, context.LiveTrading, runtimePolicy.QualificationTimeout,
            runtimePolicy.MaximumInputAge, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        RequirePublisherIsolation();
        if (qualified.Count == 0 || qualified.Count != context.Generations.Count)
            throw new InvalidOperationException("QualifyDatabento: qualification did not cover all candidate generations.");
        context.HardResult = new(context.Request.CorrelationId, context.Generations.Values.First(), 1,
            DatabentoHardRecoveryOutcome.DatabentoHealthy, string.Empty, "All replacement workers qualified locally.")
        {
            DatasetGenerations = new Dictionary<string, Guid>(context.Generations),
            AttemptEvidence =
            [
                new DatabentoHardAttemptEvidence(1, false, nameof(QualifyDatabentoAsync), string.Empty,
                    "Local qualification completed.", qualified.Select(worker => worker.ProcessId).ToArray())
                {
                    AttemptId = Guid.NewGuid(), StartedUtc = startedUtc,
                    Elapsed = time.GetElapsedTime(started),
                    DatasetGenerations = new Dictionary<string, Guid>(context.Generations)
                }
            ]
        };
    }

    /// <inheritdoc />
    public Task PrepareCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        proof.PrepareAsync(Frozen(context), LocalResult(context), token);

    /// <inheritdoc />
    public async Task QualifyInfrastructureAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        var result = await infrastructure.QualifyAsync(token).ConfigureAwait(false);
        if (!result.Qualified)
            throw new InvalidOperationException(
                $"QualifyInfrastructure failed after {result.Rounds} probe round(s): "
                + string.Join("; ", result.Probes.Where(item => !item.Qualified)
                    .Select(item => $"{item.Kind}/{item.Name}: {item.Detail}")));
    }

    /// <inheritdoc />
    public async Task ReconcileActorsAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        var result = await supervisor.ReconcileAsync(pipelinePolicy.SupervisorTimeout, token)
            .AsTask().WaitAsync(pipelinePolicy.SupervisorTimeout, token).ConfigureAwait(false);
        if (!result.Qualified)
            throw new InvalidOperationException(
                $"ReconcileActors failed: operation={result.OperationId}; healthy={result.Healthy}; "
                + $"degraded={result.Degraded}; critical={result.Critical}; unknown={result.Unknown}; {result.Detail}; "
                + string.Join("; ", result.Components.Select(item => $"{item.Component}: {item.Health}, {item.Action}, {item.Detail}")));
        foreach (var generation in context.Generations)
        {
            var canary = await supervisorCanary.ProbeAsync(context.Request.CorrelationId,
                generation.Value, context.Request.ValueDate, generation.Key,
                pipelinePolicy.SupervisorTimeout, token).ConfigureAwait(false);
            if (!canary.Qualified)
                throw new InvalidOperationException(
                    $"ReconcileActors failed control-plane canary for {generation.Key}/{generation.Value:D}: {canary.Detail}");
        }
    }

    /// <inheritdoc />
    public async Task StartPublisherAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        RequirePublisherIsolation();
        if (!publisher.IsRunning)
            await publisher.StartAsync(token).AsTask().WaitAsync(pipelinePolicy.DownstreamTimeout, token)
                .ConfigureAwait(false);
        if (!publisher.IsRunning)
            throw new InvalidOperationException("StartPublisher completed without a running publisher.");
    }

    /// <inheritdoc />
    public async Task ProveDownstreamWritesAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        if (!await proof.QualifyAsync(LocalResult(context), token)
                .WaitAsync(pipelinePolicy.DownstreamTimeout, token).ConfigureAwait(false))
            throw new InvalidOperationException(
                "ProveDownstreamWrites failed: durable generation-correlated proof did not qualify for "
                + string.Join(", ", context.Generations.Select(item => $"{item.Key}/{item.Value:D}")));
    }

    /// <inheritdoc />
    public async Task AdmitGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token)
    {
        await proof.PromoteAsync(LocalResult(context), token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        lifecycle.AdoptRecoveredSession(Frozen(context), context.Generations, admissions);
    }

    /// <inheritdoc />
    public Task NotifySystemConsoleAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure) =>
        console.WriteConsoleAsync(LogSourceType.System, 10042,
            $"API shutting down: hard reset recovery failed. Action={failure.FailedStage}; "
            + $"CorrelationId={context.Request.CorrelationId:D}; ValueDate={context.Request.ValueDate:yyyy-MM-dd}; "
            + failure.Detail);

    /// <inheritdoc />
    public async Task ShutdownApiAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure)
    {
        try
        {
            await shutdown.RequestAsync(new FatalRecoveryReport(time.GetUtcNow(),
                $"Hard reset recovery action {failure.FailedStage} failed", context.Request, failure))
                .WaitAsync(pipelinePolicy.DownstreamTimeout).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            shutdown.FailAndExit(error);
            throw;
        }
    }

    void RequirePublisherIsolation()
    {
        var snapshot = publisherDiagnostics.GetSnapshot();
        if (snapshot.UncontainedSend || snapshot.Failure == RealtimeTickPublisherFailure.NonCooperativeSend)
            throw new InvalidOperationException(
                $"Publisher isolation cannot be proven: failure={snapshot.Failure}; uncontainedSend={snapshot.UncontainedSend}. No replacement generation may be admitted.");
    }

    static DatasetRecoveryManifestSnapshot Frozen(ApiDatabentoRecoveryContext context) =>
        context.Frozen ?? throw new InvalidOperationException("Recovery inputs have not been captured.");

    static DatabentoHardRecoveryResult LocalResult(ApiDatabentoRecoveryContext context) =>
        context.HardResult is { Outcome: DatabentoHardRecoveryOutcome.DatabentoHealthy } result
            ? result : throw new InvalidOperationException("Replacement Databento generations have not qualified locally.");
}
