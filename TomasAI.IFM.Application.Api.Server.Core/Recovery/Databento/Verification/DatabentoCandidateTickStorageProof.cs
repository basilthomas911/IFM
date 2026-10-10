using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Verification;

/// <summary>Finite policy for a candidate-only durable tick-storage qualification.</summary>
public sealed record DatabentoCandidateTickStorageProofPolicy
{
    public int HoldingCapacityPerDataset { get; init; } = 2048;
    public TimeSpan ProofTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public DatabentoCandidateTickStorageProofPolicy Validate()
    {
        if (HoldingCapacityPerDataset is < 1 or > 100_000
            || ProofTimeout <= TimeSpan.Zero || ProofTimeout > TimeSpan.FromMinutes(5)
            || DrainTimeout <= TimeSpan.Zero || DrainTimeout > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("Candidate tick-storage proof requires finite capacity and deadlines.");
        return this;
    }
}

/// <summary>
/// Keeps normal candidate publications held while one tick-storage route proves its exact
/// generation through the real actor and an acknowledged database command.
/// </summary>
public sealed class DatabentoCandidateTickStorageProof
{
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly DatasetPublicationIngress ingress;
    readonly DatasetWorkerProcessRecoveryService workers;
    readonly TickStorageGenerationEvidence evidence;
    readonly DatabentoCandidateTickStorageProofPolicy policy;
    readonly TimeProvider time;
    readonly ILogger<DatabentoCandidateTickStorageProof> logger;
    readonly SemaphoreSlim operations = new(1, 1);
    CandidateSession? pending;

    public DatabentoCandidateTickStorageProof(DatasetWorkerAdmissionRegistry admissions,
        DatasetPublicationIngress ingress, DatasetWorkerProcessRecoveryService workers,
        TickAggregationRealtimeProjector projector, TickStorageGenerationEvidence evidence,
        DatabentoCandidateTickStorageProofPolicy policy, TimeProvider time,
        ILogger<DatabentoCandidateTickStorageProof>? logger = null)
    {
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        this.workers = workers ?? throw new ArgumentNullException(nameof(workers));
        this.evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        this.policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Validate();
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        this.logger = logger ?? NullLogger<DatabentoCandidateTickStorageProof>.Instance;
        (projector ?? throw new ArgumentNullException(nameof(projector))).AttachRecoveryEvidence(evidence);
    }

    /// <summary>Fences and removes an earlier held candidate before a new hard episode begins.</summary>
    public async Task AbandonAsync(CancellationToken cancellationToken)
    {
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (pending is not { } session) return;
            foreach (var identity in session.Identities)
            {
                if (admissions.HasProbe(identity))
                    await admissions.EndProbeAsync(identity, policy.DrainTimeout,
                        cancellationToken).ConfigureAwait(false);
                admissions.Close(identity.Dataset, identity.GenerationId);
                ingress.StopCandidateHolding(identity);
            }
            evidence.Disarm();
            pending = null;
        }
        finally { operations.Release(); }
    }

    /// <summary>Begins bounded holding for all locally qualified candidate datasets.</summary>
    public async Task PrepareAsync(DatasetRecoveryManifestSnapshot frozen,
        DatabentoHardRecoveryResult result, CancellationToken cancellationToken)
    {
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (pending is { } existing)
            {
                if (existing.CorrelationId != result.CorrelationId)
                    throw new InvalidOperationException("Another candidate holding session is active.");
                return;
            }
            if (result.Outcome != DatabentoHardRecoveryOutcome.DatabentoHealthy
                || frozen.ValueDate == default || frozen.Manifests.Count != result.DatasetGenerations.Count)
                throw new InvalidOperationException("Local qualification does not match the frozen dataset set.");
            var snapshots = workers.Current.ToDictionary(worker => worker.Dataset, StringComparer.Ordinal);
            var identities = new List<DatasetWorkerAdmission>(frozen.Manifests.Count);
            foreach (var manifest in frozen.Manifests)
            {
                if (!snapshots.TryGetValue(manifest.Dataset, out var worker)
                    || !result.DatasetGenerations.TryGetValue(manifest.Dataset, out var generation)
                    || generation != worker.GenerationId || !worker.Running || !worker.Healthy
                    || worker.ManifestRevision != manifest.Revision
                    || worker.ManifestFingerprint != manifest.Fingerprint)
                    throw new InvalidOperationException("Candidate worker does not match the locally qualified frozen manifest.");
                identities.Add(new DatasetWorkerAdmission(manifest.Dataset, frozen.ValueDate,
                    worker.WorkerInstanceId, generation, manifest.Revision));
            }
            var started = new List<DatasetWorkerAdmission>(identities.Count);
            try
            {
                foreach (var identity in identities)
                {
                    ingress.StartCandidateHolding(identity,
                        new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(
                            policy.HoldingCapacityPerDataset));
                    started.Add(identity);
                }
                pending = new CandidateSession(result.CorrelationId, frozen, identities);
            }
            catch
            {
                foreach (var identity in started) ingress.StopCandidateHolding(identity);
                throw;
            }
        }
        finally { operations.Release(); }
    }

    /// <summary>
    /// Proves the real actor/storage route for any candidate trade or quote already held.
    /// An idle market is not a recovery failure: local subscription acknowledgements,
    /// infrastructure probes, and actor reconciliation qualify that case independently.
    /// </summary>
    public async Task<bool> QualifyAsync(DatabentoHardRecoveryResult result,
        CancellationToken cancellationToken)
    {
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = pending;
            if (session is null || session.CorrelationId != result.CorrelationId)
                throw new InvalidOperationException("No matching candidate holding session exists.");
            if (session.Qualified) return true;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(policy.ProofTimeout);
            foreach (var identity in session.Identities)
            {
                logger.LogInformation("{Component}.{Method} "+"Candidate downstream proof starting. CorrelationId={CorrelationId}; Dataset={Dataset}; GenerationId={GenerationId}",nameof(DatabentoCandidateTickStorageProof),nameof(QualifyAsync),result.CorrelationId,identity.Dataset,identity.GenerationId);
                evidence.Arm(new TickStorageGenerationTarget(identity.Dataset, identity.GenerationId));
                var armed = false;
                var published = false;
                try
                {
                    admissions.BeginProbe(identity);
                    armed = true;
                    published = await ingress.PublishHeldCandidateTickProbeAsync(identity,
                        deadline.Token).ConfigureAwait(false);
                    if (!published)
                    {
                        logger.LogInformation(
                            "{Component}.{Method} "+"Candidate has no held trade or quote; downstream tick proof is inactive. CorrelationId={CorrelationId}; Dataset={Dataset}; GenerationId={GenerationId}",nameof(DatabentoCandidateTickStorageProof),nameof(QualifyAsync),                            result.CorrelationId,identity.Dataset,identity.GenerationId);
                        continue;
                    }
                    while (!deadline.IsCancellationRequested)
                    {
                        if (evidence.Capture().CompletedWrites > 0) break;
                        await Task.Delay(TimeSpan.FromMilliseconds(50), time, deadline.Token)
                            .ConfigureAwait(false);
                    }
                    if (deadline.IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"Candidate downstream proof timed out: dataset={identity.Dataset}; generation={identity.GenerationId:D}; heldTickPublished={published}; completedWrites={evidence.Capture().CompletedWrites}; timeout={policy.ProofTimeout}.");
                    }
                    logger.LogInformation("{Component}.{Method} "+"Candidate downstream proof completed. CorrelationId={CorrelationId}; Dataset={Dataset}; GenerationId={GenerationId}; CompletedWrites={CompletedWrites}",nameof(DatabentoCandidateTickStorageProof),nameof(QualifyAsync),result.CorrelationId,identity.Dataset,identity.GenerationId,evidence.Capture().CompletedWrites);
                }
                catch (OperationCanceledException error) when (deadline.IsCancellationRequested
                    && !cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Candidate downstream proof timed out: dataset={identity.Dataset}; generation={identity.GenerationId:D}; heldTickPublished={published}; completedWrites={evidence.Capture().CompletedWrites}; timeout={policy.ProofTimeout}.", error);
                }
                finally
                {
                    if (armed)
                        await admissions.EndProbeAsync(identity, policy.DrainTimeout,
                            CancellationToken.None).ConfigureAwait(false);
                    evidence.Disarm();
                }
            }
            session.Qualified = true;
            return true;
        }
        finally { operations.Release(); }
    }

    /// <summary>Admits all proved workers while holding remains active, then releases all datasets together.</summary>
    public async Task PromoteAsync(DatabentoHardRecoveryResult result,
        CancellationToken cancellationToken)
    {
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = pending;
            if (session is null || session.CorrelationId != result.CorrelationId || !session.Qualified)
                throw new InvalidOperationException("Only a fully proved candidate session can be promoted.");
            var admitted = new List<DatasetWorkerAdmission>(session.Identities.Count);
            try
            {
                foreach (var manifest in session.Frozen.Manifests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var identity = session.Identities.Single(item => item.Dataset == manifest.Dataset);
                    await workers.AdmitCandidateAsync(manifest, identity.GenerationId,
                        cancellationToken).ConfigureAwait(false);
                    admitted.Add(identity);
                }
                cancellationToken.ThrowIfCancellationRequested();
                ingress.PromoteCandidateHoldings(session.Identities);
                pending = null;
            }
            catch
            {
                foreach (var identity in admitted)
                    admissions.Close(identity.Dataset, identity.GenerationId);
                throw;
            }
        }
        finally { operations.Release(); }
    }

    sealed class CandidateSession(Guid correlationId, DatasetRecoveryManifestSnapshot frozen,
        IReadOnlyList<DatasetWorkerAdmission> identities)
    {
        public Guid CorrelationId { get; } = correlationId;
        public DatasetRecoveryManifestSnapshot Frozen { get; } = frozen;
        public IReadOnlyList<DatasetWorkerAdmission> Identities { get; } = identities;
        public bool Qualified;
    }
}
