using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed record AwsStoredRetentionPlan(
    DatabaseRetentionEvaluationRequest Request, AwsRetentionPlanDocument Plan, AwsSignatureEnvelope Signature);

/// <summary>Plans and executes normal expired-version retention. Never changes Object Lock or legal hold.</summary>
public sealed class AwsCloudRetentionCapability(
    AwsRetentionReadClient primaryRead,
    AwsRecoveryVaultClient recovery,
    AwsRetentionExecutionClients deletionClients,

    IAwsDocumentSignatureService signatures,
    AwsRecoveryEvidenceStore evidence,
    AwsRetentionPlanAuthorizationService authorization,
    IAwsDatabaseBackupExecutionJournal journal,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider clock) : IDatabaseRetentionCapability
{
    async ValueTask EnsureIdleAsync(CancellationToken token)
    {
        await foreach (var operation in journal.ReadRecoverableOperationsAsync(token).ConfigureAwait(false))
            if (operation.Intent.ExecutionEvent.Source.OperationKind != DatabaseRecoveryOperationKind.Retention)
                throw new InvalidOperationException("Retention is deferred while backup or restore operations are active.");
    }

    (IAmazonS3 Client, S3DatabaseBackupCatalog Catalog) Vault(DatabaseArtifactReplicaId replica)
    {
        if (replica.Value == "aws-primary")
        {
            var primary = primaryRead.Client;
            return (primary, new S3DatabaseBackupCatalog(primary, new S3ImmutableObjectStore(primary, options, clock), signatures, options));
        }
        if (replica.Value != "aws-recovery") throw new InvalidDataException("Unknown retention replica.");

        // The immutable store verifies the descriptor's bucket/region/key, so its upload defaults are unused.
        var store = new S3ImmutableObjectStore(recovery.Client, options, clock);
        return (recovery.Client, new S3DatabaseBackupCatalog(recovery.Client, store, signatures, options,
            new AwsVaultLocation(options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn, replica)));
    }

    public async ValueTask<DatabaseRetentionPlan> CreatePlanAsync(DatabaseRetentionEvaluationRequest request, CancellationToken token)
    {
        await EnsureIdleAsync(token).ConfigureAwait(false);
        var (client, source) = Vault(request.ReplicaId);
        try
        {
            var existing = await evidence.ReadRetentionPlanAsync(request.PlanId, request.Revision, request.ReplicaId, token).ConfigureAwait(false);
            if (existing.Request.PolicyRevision != request.PolicyRevision
                || existing.Request.ProtectionSetId != request.ProtectionSetId
                || existing.Request.EvaluationBoundaryUtc != request.EvaluationBoundaryUtc)
                throw new InvalidDataException("A retention plan ID/revision cannot be reused for a different request.");
            return Map(existing.Plan);
        }
        catch (FileNotFoundException) { }
        var points = await source.EnumerateAwsAsync(request.ReplicaId, token, request.ProtectionSetId).ConfigureAwait(false);
        if (request.ProtectionSetId is { } protectionSet)
            points = points.Where(point => point.Entry.ProtectionSetId == protectionSet).ToArray();
        var protectedIds = request.ProtectedRestorePoints.ToHashSet();
        foreach (var point in points)
            foreach (var item in point.ImmutableObjects ?? [])
                if (item.RetainUntilUtc > clock.GetUtcNow() || !await IsExplicitlyUnheldAsync(client, item, token).ConfigureAwait(false))
                { protectedIds.Add(point.Entry.RestorePointId); break; }
        var effective = request with { ProtectedRestorePoints = protectedIds.ToArray() };
        var plan = points.Count == 0
            ? new AwsRetentionPlanDocument
            {
                PlanId = request.PlanId, Revision = request.Revision, PolicyRevision = request.PolicyRevision,
                ReplicaId = request.ReplicaId, EvaluationBoundaryUtc = request.EvaluationBoundaryUtc,
                CreatedUtc = clock.GetUtcNow(), RestorePoints = [], DependencyProtectedRestorePoints = [],
                RetainedRestorePointCount = 0, ExpectedReclaimedBytes = 0
            }
            : AwsRetentionPlanPolicy.Create(effective, request.PolicyRevision, points, clock.GetUtcNow());
        var signature = await signatures.SignAsync(DatabaseBackupCanonicalJson.Serialize(plan), token).ConfigureAwait(false);
        await evidence.WriteRetentionPlanAsync(new AwsStoredRetentionPlan(effective, plan, signature), token).ConfigureAwait(false);
        return Map(plan);
    }

    public async ValueTask<DatabaseRetentionExecutionResult> ExecuteAsync(DatabaseRetentionExecutionRequest request, CancellationToken token)
    {
        await EnsureIdleAsync(token).ConfigureAwait(false);
        var stored = await evidence.ReadRetentionPlanAsync(request.PlanId, request.Revision, request.ReplicaId, token).ConfigureAwait(false);
        var plan = stored.Plan;
        if (plan.PlanId != request.PlanId || plan.Revision != request.Revision || plan.ReplicaId != request.ReplicaId)
            throw new InvalidDataException("Retention plan does not match the execution request.");
        var (client, source) = Vault(request.ReplicaId);
        // A verified signed empty plan authorizes no deletion; avoid downloading the catalog twice.
        if (plan.RestorePoints.Length == 0) return new(request.PlanId, request.Revision, 0, 0, 0);
        var current = await source.EnumerateAwsAsync(request.ReplicaId, token, stored.Request.ProtectionSetId).ConfigureAwait(false);
        if (stored.Request.ProtectionSetId is { } protectionSet)
            current = current.Where(point => point.Entry.ProtectionSetId == protectionSet).ToArray();
        var refreshed = AwsRetentionPlanPolicy.Create(stored.Request, plan.PolicyRevision, current, clock.GetUtcNow());
        if (!DatabaseBackupCanonicalJson.Serialize(refreshed.RestorePoints).AsSpan()
            .SequenceEqual(DatabaseBackupCanonicalJson.Serialize(plan.RestorePoints)))
            throw new InvalidOperationException("Retention catalog/dependency eligibility changed; request a new evaluation.");
        var retained = current.Where(point => !plan.RestorePoints.Any(candidate => candidate.RestorePointId == point.Entry.RestorePointId)).ToArray();
        if (retained.Length == 0) throw new InvalidOperationException("Retention must preserve a verified restore point.");

        var otherReplica = new DatabaseArtifactReplicaId(request.ReplicaId.Value == "aws-primary" ? "aws-recovery" : "aws-primary");
        var (_, otherCatalog) = Vault(otherReplica);
        foreach (var point in retained)
            _ = await otherCatalog.ResolveAwsAsync(point.Entry.RestorePointId, otherReplica, token).ConfigureAwait(false);
        var observations = new List<AwsRetentionObjectObservation>();
        foreach (var item in plan.RestorePoints.SelectMany(point => point.Objects))
        {
            var retain = await client.GetObjectRetentionAsync(new GetObjectRetentionRequest
            { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token).ConfigureAwait(false);
            var hold = await client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
            { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token).ConfigureAwait(false);
            var observedDate = retain.Retention?.RetainUntilDate
                ?? throw new InvalidDataException("Exact-version retention is unknown.");
            observations.Add(new(item.BucketName, item.Region, item.ObjectKey, item.VersionId, item.Length, item.Sha256,
                new DateTimeOffset(observedDate.ToUniversalTime()), hold.LegalHold?.Status?.Value != "OFF", true));
        }
        var approval = new AwsRetentionExecutionApproval(plan.PlanId, plan.Revision, plan.PolicyRevision, request.ApprovalReference);
        var approved = await authorization.AuthorizeAsync(DatabaseBackupCanonicalJson.Serialize(plan), stored.Signature,
            approval, observations, token).ConfigureAwait(false);
        await EnsureIdleAsync(token).ConfigureAwait(false);
        AwsRetentionDeletionResult result;
        try
        {
            result = await new AwsExactVersionDeletionExecutor(deletionClients.For(request.ReplicaId)).ExecuteAsync(approved, token).ConfigureAwait(false);
        }
        catch (AwsPartialRetentionExecutionException failure)
        {
            await evidence.WriteRetentionFailureAsync(new AwsRetentionFailureEvidence(plan.PlanId, plan.Revision,
                plan.ReplicaId, failure.Completed.ToArray(), failure.InnerException?.GetType().Name ?? failure.GetType().Name,
                clock.GetUtcNow()), token).ConfigureAwait(false);
            throw;
        }
        await evidence.WriteRetentionEvidenceAsync(new AwsRetentionExecutionEvidence(
            new DatabaseRecoveryOperationId(plan.PlanId.Value), plan, stored.Signature, approval, result,
            retained.MaxBy(point => point.Entry.PublishedUtc)!.Entry.RestorePointId, true, clock.GetUtcNow()), token).ConfigureAwait(false);
        return new(plan.PlanId, plan.Revision, plan.RestorePoints.Length, result.DeletedObjectVersionCount, result.DeletedBytes);
    }

    static async ValueTask<bool> IsExplicitlyUnheldAsync(IAmazonS3 client, AwsImmutableObjectVersion item, CancellationToken token)
    {
        try
        {
            var hold = await client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
            { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token).ConfigureAwait(false);
            return hold.LegalHold?.Status?.Value == "OFF";
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchObjectLockConfiguration")
        { return false; }
    }

    static DatabaseRetentionPlan Map(AwsRetentionPlanDocument plan) => new(1, plan.PlanId, plan.Revision,
        plan.ReplicaId, plan.EvaluationBoundaryUtc, plan.CreatedUtc,
        plan.RestorePoints.Select(point => new DatabaseRetentionPlanEntry(point.RestorePointId,
            point.Objects.Select(item => $"{item.ObjectKey}?versionId={Uri.EscapeDataString(item.VersionId)}").ToArray())).ToArray(),
        plan.DependencyProtectedRestorePoints);
}