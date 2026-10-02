using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatabentoHardAttemptResult(
    bool Succeeded,
    bool SafeToRetry,
    string Stage,
    Exception? Failure,
    IReadOnlyList<DatasetWorkerProcessSnapshot> Workers)
{
    public Exception? CleanupFailure { get; init; }
}

/// <summary>
/// Owns one Databento-only attempt. It never reconciles contracts, starts the host publisher,
/// publishes status, or admits candidate records downstream.
/// </summary>
public sealed class SupervisedDatabentoHardRecoveryRuntime(
    DatasetWorkerProcessRecoveryService workers,
    DatabentoSupervisedWorkerOptions workerOptions,
    TimeProvider timeProvider,
    ITickAggregationPublisherDiagnostics? publisherDiagnostics = null)
{
    /// <summary>Derives live-versus-quiet proof from the authoritative market session.</summary>
    public Task<DatabentoHardAttemptResult> AttemptAsync(
        DatasetRecoveryManifestSnapshot frozen,
        IFuturesMarketSessionAuthority sessions,
        TimeSpan maximumManifestAge,
        TimeSpan containmentTimeout,
        TimeSpan qualificationTimeout,
        TimeSpan maximumInputAge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(sessions);
        var session = sessions.Current;
        if (!session.IsValid || session.OperationalValueDate != frozen.ValueDate)
            return Task.FromResult(new DatabentoHardAttemptResult(false, true, "MarketSession",
                new InvalidOperationException("The authoritative market session does not match the frozen recovery value date."),
                workers.Current));
        return AttemptAsync(frozen, session.IsLiveTrading, maximumManifestAge,
            containmentTimeout, qualificationTimeout, maximumInputAge, cancellationToken);
    }

    /// <summary>Attempts recovery from one time-bounded coherent snapshot, without contract or database I/O.</summary>
    public Task<DatabentoHardAttemptResult> AttemptAsync(
        DatasetRecoveryManifestSnapshot frozen,
        bool liveTrading,
        TimeSpan maximumManifestAge,
        TimeSpan containmentTimeout,
        TimeSpan qualificationTimeout,
        TimeSpan maximumInputAge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        if (maximumManifestAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumManifestAge));
        var age = timeProvider.GetUtcNow() - frozen.CapturedAtUtc;
        if (age < TimeSpan.Zero || age > maximumManifestAge)
            return Task.FromResult(new DatabentoHardAttemptResult(false, true, "FrozenManifest",
                new InvalidOperationException("Recovery manifest snapshot is stale or has a future capture time."),
                workers.Current));
        if (frozen.Manifests.Any(manifest => !workers.DesiredSubscriptions.IsCurrent(manifest)))
            return Task.FromResult(new DatabentoHardAttemptResult(false, true, "FrozenManifest",
                new InvalidOperationException("Recovery manifest snapshot is no longer current."),
                workers.Current));
        return AttemptAsync(frozen.Manifests, liveTrading, containmentTimeout,
            qualificationTimeout, maximumInputAge, cancellationToken);
    }

    public async Task<DatabentoHardAttemptResult> AttemptAsync(
        IReadOnlyList<DatasetSubscriptionManifest> frozen,
        bool liveTrading,
        TimeSpan containmentTimeout,
        TimeSpan qualificationTimeout,
        TimeSpan maximumInputAge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        if (frozen.Count == 0 || frozen.Count > DatasetDesiredSubscriptionRegistry.MaximumDatasets
            || frozen.Select(item => item.Dataset).Distinct(StringComparer.Ordinal).Count() != frozen.Count
            || frozen.Any(item => item.ValueDate != frozen[0].ValueDate))
            throw new ArgumentException("A hard attempt requires one coherent, bounded manifest set.", nameof(frozen));
        if (containmentTimeout <= TimeSpan.Zero || qualificationTimeout <= TimeSpan.Zero
            || maximumInputAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(qualificationTimeout));

        // Replacing the host publisher while a cancellation-ignoring NATS send is still live
        // cannot guarantee that its stale event will not arrive after the next generation.
        if (PublisherIsolationLost())
            return new(false, false, "PublisherIsolation",
                new InvalidOperationException("An old NATS send remains uncontained; no replacement generation may start."),
                workers.Current);

        var stage = "ContainOldWorkers";
        var containment = await workers.ContainForHardRecoveryAsync(containmentTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!containment.Isolated)
            return new(false, false, stage, containment.Failure, workers.Current);

        // A send already in flight may become non-cooperative while old workers are being
        // contained. Recheck before creating a candidate; the pre-containment snapshot alone
        // does not establish exclusive generation ownership.
        if (PublisherIsolationLost())
            return PublisherIsolationFailure();

        try
        {
            stage = "StartCandidates";
            var generations = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var manifest in frozen)
            {
                if (!workers.DesiredSubscriptions.IsCurrent(manifest))
                    throw new InvalidOperationException("Frozen recovery manifest changed during the attempt.");
                var candidate = await workers.StartCandidateAsync(
                    workerOptions.CreateStartRequest(manifest), cancellationToken).ConfigureAwait(false);
                generations.Add(manifest.Dataset, candidate.GenerationId);
            }
            stage = "LocalQualification";
            var qualified = await QualifyCandidatesAsync(frozen, generations, liveTrading, qualificationTimeout,
                    maximumInputAge, cancellationToken)
                .ConfigureAwait(false);
            if (PublisherIsolationLost())
                return PublisherIsolationFailure();
            return new(true, true, stage, null, qualified);
        }
        catch (Exception failure)
        {
            var failedWorkers = workers.Current;
            var cleanup = await workers.ContainForHardRecoveryAsync(containmentTimeout, CancellationToken.None)
                .ConfigureAwait(false);
            return new(false, cleanup.Isolated, stage, failure, failedWorkers)
            {
                CleanupFailure = cleanup.Failure
            };
        }
    }

    DatabentoHardAttemptResult PublisherIsolationFailure() =>
        new(false, false, "PublisherIsolation",
            new InvalidOperationException("An old NATS send remains uncontained; no replacement generation may be admitted."),
            workers.Current);

    bool PublisherIsolationLost()
    {
        var snapshot = publisherDiagnostics?.GetSnapshot();
        return snapshot?.UncontainedSend == true
            || snapshot?.Failure == RealtimeTickPublisherFailure.NonCooperativeSend;
    }

    /// <summary>
    /// Qualifies already-started candidate generations using only worker evidence.
    /// Polls within the supplied deadline and throws with per-dataset diagnostics on failure.
    /// It never starts, stops, or admits workers.
    /// </summary>
    public async Task<IReadOnlyList<DatasetWorkerProcessSnapshot>> QualifyCandidatesAsync(
        IReadOnlyList<DatasetSubscriptionManifest> frozen,
        IReadOnlyDictionary<string, Guid> generations, bool liveTrading,
        TimeSpan timeout, TimeSpan maximumInputAge, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var synthetic = workerOptions.DeploymentProfile == FeedDeploymentProfile.SyntheticCi;
        var evaluator = new DatabentoLocalQualificationEvaluator(frozen, generations,
            liveTrading, synthetic, maximumInputAge, timeProvider);
        IReadOnlyList<DatasetWorkerProcessSnapshot> last = [];
        try
        {
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var current = await workers.GetHealthAsync(TimeSpan.FromSeconds(2), deadline.Token)
                .ConfigureAwait(false);
            last = current;
            if (frozen.All(workers.DesiredSubscriptions.IsCurrent)
                && evaluator.IsQualified(current))
                return current;
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeProvider, deadline.Token)
                .ConfigureAwait(false);
        }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            var detail = string.Join("; ", last.Select(worker =>
                $"{worker.Dataset}: running={worker.Running}, control={worker.ControlResponsive}, "
                + $"native={worker.Diagnostics?.DatabentoLocallyReady}, "
                + $"produced={worker.Diagnostics?.RecordsProduced}, consumed={worker.Diagnostics?.RecordsConsumed}, "
                + $"heartbeat={worker.Diagnostics?.HeartbeatCount}, providerAge={worker.Diagnostics?.LastProviderMessageAgeTicks}"));
            throw new TimeoutException($"Databento local qualification timed out: {detail}");
        }
    }

}
