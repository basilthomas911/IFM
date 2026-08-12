using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalBackupGovernanceStore(
    DatabaseBackupPublicationOptions options,
    IBackupPathPolicy paths,
    IManifestSignatureService signatures,
    IDatabaseBackupCatalog catalog)
    : IDatabaseRetentionCapability, IDatabaseRecoveryEvidenceStore
{
    public async ValueTask<DatabaseRetentionPlan> CreatePlanAsync(
        DatabaseRetentionEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePlanIdentity(request.PlanId, request.Revision, request.EvaluationBoundaryUtc);
        var points = await catalog.EnumerateAsync(request.ReplicaId, cancellationToken).ConfigureAwait(false);
        var protectedIds = request.ProtectedRestorePoints
            .Concat(request.LegalHolds)
            .Concat(request.ActiveRestorePoints)
            .ToHashSet();
        var newest = points.MaxBy(static point => point.Entry.PublishedUtc);
        if (newest is not null) protectedIds.Add(newest.Entry.RestorePointId);
        var eligible = points
            .Where(point => point.Entry.PublishedUtc <= request.EvaluationBoundaryUtc
                && !protectedIds.Contains(point.Entry.RestorePointId))
            .ToDictionary(static point => point.Entry.RestorePointId);
        var retained = points.Where(point => !eligible.ContainsKey(point.Entry.RestorePointId)).ToArray();
        var dependencyProtected = ResolveDependencyClosure(retained, points);
        foreach (var dependency in dependencyProtected) eligible.Remove(dependency);

        var entries = eligible.Values
            .OrderBy(static point => point.Entry.PublishedUtc)
            .Select(point => new DatabaseRetentionPlanEntry(
                point.Entry.RestorePointId,
                ExactPaths(point).Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
        var plan = new DatabaseRetentionPlan(
            1,
            request.PlanId,
            request.Revision,
            request.ReplicaId,
            request.EvaluationBoundaryUtc,
            DateTimeOffset.UtcNow,
            entries,
            dependencyProtected.OrderBy(static value => value.Value, StringComparer.Ordinal).ToArray());
        await LocalBackupJson.WriteSignedCreateNewAsync(
            paths.Resolve(RootForReplica(request.ReplicaId), PlanRelativePath(request.PlanId, request.Revision)),
            plan,
            signatures,
            cancellationToken).ConfigureAwait(false);
        return plan;
    }

    public async ValueTask<DatabaseRetentionExecutionResult> ExecuteAsync(
        DatabaseRetentionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Revision <= 0 || string.IsNullOrWhiteSpace(request.ApprovalReference)
            || request.ApprovalReference.Any(char.IsControl))
            throw new ArgumentException("A revision-bound approved retention request is required.", nameof(request));
        var root = RootForReplica(request.ReplicaId);
        var plan = await LocalBackupJson.ReadSignedAsync<DatabaseRetentionPlan>(
            paths.Resolve(root, PlanRelativePath(request.PlanId, request.Revision)),
            signatures,
            cancellationToken).ConfigureAwait(false);
        if (plan.SchemaVersion != 1 || plan.PlanId != request.PlanId
            || plan.Revision != request.Revision || plan.ReplicaId != request.ReplicaId)
            throw new InvalidDataException("The signed retention plan does not match the approved execution request.");

        var current = await catalog.EnumerateAsync(request.ReplicaId, cancellationToken).ConfigureAwait(false);
        var planned = plan.Entries.Select(static entry => entry.RestorePointId).ToHashSet();
        var currentDependencyProtected = ResolveDependencyClosure(
            current.Where(point => !planned.Contains(point.Entry.RestorePointId)), current);
        if (planned.Overlaps(currentDependencyProtected))
            throw new InvalidOperationException("Retention execution would break a retained restore dependency.");

        var deletedFiles = 0;
        long deletedBytes = 0;
        foreach (var entry in plan.Entries)
        {
            foreach (var relativePath in entry.ExactRelativePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = paths.Resolve(root, relativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("A revision-bound retention path no longer exists.", path);
                LocalBackupPathPolicy.RejectLink(new FileInfo(path));
                deletedBytes = checked(deletedBytes + new FileInfo(path).Length);
                File.Delete(path);
                deletedFiles++;
            }
        }
        return new DatabaseRetentionExecutionResult(
            request.PlanId, request.Revision, plan.Entries.Length, deletedFiles, deletedBytes);
    }

    public async ValueTask<string> WriteDrillEvidenceAsync(
        DatabaseRestoreDrillEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.OperationId.Value == Guid.Empty
            || evidence.CompletedUtc < evidence.StartedUtc
            || evidence.StartedUtc.Offset != TimeSpan.Zero
            || evidence.CompletedUtc.Offset != TimeSpan.Zero
            || evidence.AchievedRpo < TimeSpan.Zero
            || evidence.AchievedRto < TimeSpan.Zero
            || !evidence.NativeValidationSucceeded
            || !evidence.ApplicationValidationSucceeded
            || string.IsNullOrWhiteSpace(evidence.SafeTargetReference))
            throw new ArgumentException("Successful bounded restore-drill evidence is required.", nameof(evidence));
        _ = await catalog.ResolveAsync(evidence.RestorePointId, evidence.ReplicaId, cancellationToken)
            .ConfigureAwait(false);
        var relative = $"{EnvironmentPrefix()}/drill-evidence/{evidence.OperationId.Format()}/final.json";
        await LocalBackupJson.WriteSignedCreateNewAsync(
            paths.Resolve(RootForReplica(evidence.ReplicaId), relative),
            evidence,
            signatures,
            cancellationToken).ConfigureAwait(false);
        return relative;
    }

    public async ValueTask<string> WriteBreakGlassRecordAsync(
        DatabaseBreakGlassRecoveryRecord record,
        CancellationToken cancellationToken)
    {
        Validate(record);
        _ = await catalog.ResolveAsync(record.RestorePointId, record.ReplicaId, cancellationToken)
            .ConfigureAwait(false);
        var relative = RecoveryRecordRelativePath(record.RecoveryOperationId);
        await LocalBackupJson.WriteSignedCreateNewAsync(
            paths.Resolve(RootForReplica(record.ReplicaId), relative),
            record,
            signatures,
            cancellationToken).ConfigureAwait(false);
        return relative;
    }

    public async ValueTask<DatabaseBreakGlassRecoveryRecord> ReconcileBreakGlassRecordAsync(
        DatabaseRecoveryOperationId operationId,
        CancellationToken cancellationToken)
    {
        if (operationId.Value == Guid.Empty) throw new ArgumentException("A recovery operation is required.", nameof(operationId));
        foreach (var root in CandidateRoots())
        {
            var path = paths.Resolve(root, RecoveryRecordRelativePath(operationId));
            if (!File.Exists(path)) continue;
            var record = await LocalBackupJson.ReadSignedAsync<DatabaseBreakGlassRecoveryRecord>(
                path, signatures, cancellationToken).ConfigureAwait(false);
            Validate(record);
            if (record.RecoveryOperationId != operationId)
                throw new InvalidDataException("The recovery record operation identity conflicts with its path.");
            _ = await catalog.ResolveAsync(record.RestorePointId, record.ReplicaId, cancellationToken)
                .ConfigureAwait(false);
            return record;
        }
        throw new FileNotFoundException("No signed break-glass recovery record exists for the operation.");
    }

    HashSet<DatabaseRestorePointId> ResolveDependencyClosure(
        IEnumerable<DatabaseCatalogRestorePoint> retained,
        IReadOnlyList<DatabaseCatalogRestorePoint> all)
    {
        var byId = all.ToDictionary(static point => point.Entry.RestorePointId);
        var protectedIds = new HashSet<DatabaseRestorePointId>();
        var pending = new Queue<DatabaseRestorePointId>(retained.SelectMany(static point => point.Manifest.Dependencies));
        while (pending.TryDequeue(out var dependency))
        {
            if (!protectedIds.Add(dependency)) continue;
            if (!byId.TryGetValue(dependency, out var point))
                throw new InvalidDataException("A retained restore point has a missing dependency.");
            foreach (var parent in point.Manifest.Dependencies) pending.Enqueue(parent);
        }
        return protectedIds;
    }

    IEnumerable<string> ExactPaths(DatabaseCatalogRestorePoint point)
    {
        foreach (var artifact in point.Manifest.Artifacts) yield return artifact.RelativePath;
        yield return point.Entry.ManifestRelativePath;
        yield return point.Entry.ManifestRelativePath + ".sig";
        yield return point.Entry.CommitRelativePath;
        yield return point.Entry.CommitRelativePath + ".sig";
        // The entry path is deterministic from the immutable entry fields.
        var prefix = point.Entry.ManifestRelativePath[..point.Entry.ManifestRelativePath.IndexOf("/protection-sets/", StringComparison.Ordinal)];
        yield return $"{prefix}/catalog/entries/{point.Entry.PublishedUtc:yyyyMMdd}/{point.Entry.RestorePointId.Value}/{point.Entry.ManifestId}-{point.Entry.ReplicaId.Value}.json";
        yield return $"{prefix}/catalog/entries/{point.Entry.PublishedUtc:yyyyMMdd}/{point.Entry.RestorePointId.Value}/{point.Entry.ManifestId}-{point.Entry.ReplicaId.Value}.json.sig";
        if (options.OfflineMedia.Enabled
            && point.Entry.ReplicaId == new DatabaseArtifactReplicaId(options.OfflineMedia.ReplicaId))
        {
            yield return $"{prefix}/media-seals/{point.Entry.ManifestId}/1.json";
            yield return $"{prefix}/media-seals/{point.Entry.ManifestId}/1.json.sig";
        }
    }

    DatabaseApprovedStorageRoot RootForReplica(DatabaseArtifactReplicaId replicaId)
        => paths.GetReplicaRoot(replicaId);

    IEnumerable<DatabaseApprovedStorageRoot> CandidateRoots()
    {
        yield return paths.GetReplicaRoot(new DatabaseArtifactReplicaId(options.OnlineVault.ReplicaId));
        if (options.OfflineMedia.Enabled)
            yield return paths.GetReplicaRoot(new DatabaseArtifactReplicaId(options.OfflineMedia.ReplicaId));
    }

    string EnvironmentPrefix() => $"vault/schema-v1/environments/{options.EnvironmentId}";

    string PlanRelativePath(DatabaseRetentionPlanId planId, long revision)
        => $"{EnvironmentPrefix()}/retention/plans/{planId.Value:N}/{revision}.json";

    string RecoveryRecordRelativePath(DatabaseRecoveryOperationId operationId)
        => $"{EnvironmentPrefix()}/recovery-records/{operationId.Format()}/final.json";

    static void ValidatePlanIdentity(
        DatabaseRetentionPlanId planId,
        long revision,
        DateTimeOffset boundary)
    {
        if (planId.Value == Guid.Empty || revision <= 0 || boundary == default || boundary.Offset != TimeSpan.Zero)
            throw new ArgumentException("A revision-bound UTC retention evaluation is required.");
    }

    static void Validate(DatabaseBreakGlassRecoveryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.SchemaVersion != 1
            || record.RecoveryOperationId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(record.RestorePointId.Value)
            || string.IsNullOrWhiteSpace(record.MediaId)
            || string.IsNullOrWhiteSpace(record.AuthorizationReference)
            || string.IsNullOrWhiteSpace(record.OperatorIdentity)
            || string.IsNullOrWhiteSpace(record.RecoveryHostId)
            || string.IsNullOrWhiteSpace(record.ManifestId)
            || record.ArtifactVersions.Length == 0
            || record.CompletedUtc < record.StartedUtc
            || record.StartedUtc.Offset != TimeSpan.Zero
            || record.CompletedUtc.Offset != TimeSpan.Zero
            || record.AchievedRpo < TimeSpan.Zero
            || record.AchievedRto < TimeSpan.Zero
            || !record.NativeValidationSucceeded
            || !record.ApplicationValidationSucceeded
            || string.IsNullOrWhiteSpace(record.CutoverDecision))
            throw new InvalidDataException("The break-glass recovery record is incomplete or invalid.");
    }
}
