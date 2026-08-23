using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed record AwsRetentionPlanObject(
    string BucketName,
    string Region,
    string ObjectKey,
    string VersionId,
    long Length,
    string Sha256,
    DateTimeOffset ObservedRetainUntilUtc);

public sealed record AwsRetentionPlanRestorePoint(
    DatabaseRestorePointId RestorePointId,
    DatabaseEngine Engine,
    AwsRetentionPlanObject[] Objects);

public sealed record AwsRetentionPlanDocument
{
    public int SchemaVersion { get; init; } = 1;
    public required DatabaseRetentionPlanId PlanId { get; init; }
    public required long Revision { get; init; }
    public required long PolicyRevision { get; init; }
    public required DatabaseArtifactReplicaId ReplicaId { get; init; }
    public required DateTimeOffset EvaluationBoundaryUtc { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required AwsRetentionPlanRestorePoint[] RestorePoints { get; init; }
    public required DatabaseRestorePointId[] DependencyProtectedRestorePoints { get; init; }
    public required int RetainedRestorePointCount { get; init; }
    public required long ExpectedReclaimedBytes { get; init; }
}

public sealed record AwsRetentionExecutionApproval(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    long PolicyRevision,
    string ApprovalReference);

public sealed record AwsRetentionObjectObservation(
    string BucketName,
    string Region,
    string ObjectKey,
    string VersionId,
    long Length,
    string Sha256,
    DateTimeOffset RetainUntilUtc,
    bool LegalHold,
    bool RequiredReplicaComplete);

public sealed class AwsApprovedRetentionExecution
{
    internal AwsApprovedRetentionExecution(
        DatabaseRetentionPlanId planId,
        long revision,
        string approvalReference,
        AwsRetentionPlanObject[] objects)
    {
        PlanId = planId;
        Revision = revision;
        ApprovalReference = approvalReference;
        Objects = objects;
    }

    public DatabaseRetentionPlanId PlanId { get; }
    public long Revision { get; }
    public string ApprovalReference { get; }
    public IReadOnlyList<AwsRetentionPlanObject> Objects { get; }
}

public sealed class AwsRetentionPlanAuthorizationService(
    IAwsDocumentSignatureService signatures,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider timeProvider)
{
    public async ValueTask<AwsApprovedRetentionExecution> AuthorizeAsync(
        byte[] immutablePlan,
        AwsSignatureEnvelope signature,
        AwsRetentionExecutionApproval approval,
        IReadOnlyCollection<AwsRetentionObjectObservation> current,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(immutablePlan);
        ArgumentNullException.ThrowIfNull(signature);
        if (immutablePlan.Length == 0 || immutablePlan.Length > options.MaximumSignedDocumentBytes)
            throw new InvalidDataException("The immutable AWS retention plan exceeds its signed-document bound.");
        await signatures.VerifyAsync(immutablePlan, signature, cancellationToken).ConfigureAwait(false);
        var plan = DatabaseBackupCanonicalJson.Deserialize<AwsRetentionPlanDocument>(immutablePlan);
        return AwsRetentionPlanPolicy.ValidateExecution(plan, approval, current, options, timeProvider.GetUtcNow());
    }
}

public static class AwsRetentionPlanPolicy
{
    public static AwsRetentionPlanDocument Create(
        DatabaseRetentionEvaluationRequest request,
        long policyRevision,
        IReadOnlyList<AwsResolvedCatalogRestorePoint> catalog,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PlanId.Value == Guid.Empty || request.Revision <= 0 || policyRevision <= 0
            || request.EvaluationBoundaryUtc == default || request.EvaluationBoundaryUtc.Offset != TimeSpan.Zero
            || createdUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("A revision-bound UTC AWS retention evaluation is required.", nameof(request));
        if (catalog.Count == 0)
            throw new InvalidOperationException("AWS retention planning requires a non-empty verified catalog.");
        if (catalog.Any(point => point.Entry.ReplicaId != request.ReplicaId))
            throw new InvalidDataException("AWS retention planning cannot mix logical replicas.");

        var protectedIds = request.ProtectedRestorePoints
            .Concat(request.LegalHolds)
            .Concat(request.ActiveRestorePoints)
            .ToHashSet();
        foreach (var engineGroup in catalog.GroupBy(static point => point.Entry.Engine))
        {
            var newest = engineGroup.MaxBy(static point => point.Entry.PublishedUtc);
            if (newest is not null) protectedIds.Add(newest.Entry.RestorePointId);
        }

        var eligible = catalog
            .Where(point => point.Entry.PublishedUtc <= request.EvaluationBoundaryUtc
                && !protectedIds.Contains(point.Entry.RestorePointId))
            .ToDictionary(static point => point.Entry.RestorePointId);
        var retained = catalog.Where(point => !eligible.ContainsKey(point.Entry.RestorePointId)).ToArray();
        var dependencyProtected = ResolveDependencyClosure(retained, catalog);
        foreach (var dependency in dependencyProtected) eligible.Remove(dependency);

        var restorePoints = eligible.Values
            .OrderBy(static point => point.Entry.PublishedUtc)
            .ThenBy(static point => point.Entry.RestorePointId.Value, StringComparer.Ordinal)
            .Select(point => new AwsRetentionPlanRestorePoint(
                point.Entry.RestorePointId,
                point.Entry.Engine,
                ExactObjects(point)))
            .ToArray();
        return new AwsRetentionPlanDocument
        {
            PlanId = request.PlanId,
            Revision = request.Revision,
            PolicyRevision = policyRevision,
            ReplicaId = request.ReplicaId,
            EvaluationBoundaryUtc = request.EvaluationBoundaryUtc,
            CreatedUtc = createdUtc,
            RestorePoints = restorePoints,
            DependencyProtectedRestorePoints = dependencyProtected
                .OrderBy(static value => value.Value, StringComparer.Ordinal).ToArray(),
            RetainedRestorePointCount = catalog.Count - restorePoints.Length,
            ExpectedReclaimedBytes = restorePoints.Sum(static point => point.Objects.Sum(static value => value.Length))
        };
    }

    public static AwsApprovedRetentionExecution ValidateExecution(
        AwsRetentionPlanDocument plan,
        AwsRetentionExecutionApproval approval,
        IReadOnlyCollection<AwsRetentionObjectObservation> current,
        AwsCloudDatabaseBackupOptions options,
        DateTimeOffset executionUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(options);
        if (plan.SchemaVersion != 1 || plan.PlanId != approval.PlanId || plan.Revision != approval.Revision
            || plan.PolicyRevision != approval.PolicyRevision || string.IsNullOrWhiteSpace(approval.ApprovalReference)
            || approval.ApprovalReference.Any(char.IsControl))
            throw new InvalidOperationException("The approved AWS retention execution does not match the immutable plan revision.");
        if (executionUtc.Offset != TimeSpan.Zero || plan.RetainedRestorePointCount <= 0)
            throw new InvalidOperationException("AWS retention must preserve an independently recoverable restore point.");
        var expectedBucket = plan.ReplicaId == new DatabaseArtifactReplicaId("aws-primary")
            ? options.PrimaryBucketName
            : plan.ReplicaId == new DatabaseArtifactReplicaId("aws-recovery")
                ? options.RecoveryBucketName
                : throw new InvalidOperationException("The AWS retention plan names an unknown logical replica.");
        var expectedRegion = plan.ReplicaId == new DatabaseArtifactReplicaId("aws-primary")
            ? options.PrimaryRegion : options.RecoveryRegion;
        var observations = current.ToDictionary(
            static value => (value.BucketName, value.ObjectKey, value.VersionId),
            static value => value);
        var exact = plan.RestorePoints.SelectMany(static point => point.Objects).ToArray();
        if (exact.Length == 0 || exact.Select(static value => (value.BucketName, value.ObjectKey, value.VersionId)).Distinct().Count() != exact.Length)
            throw new InvalidDataException("The AWS retention plan is empty or contains duplicate object versions.");
        foreach (var item in exact)
        {
            if (!StringComparer.Ordinal.Equals(item.BucketName, expectedBucket)
                || !StringComparer.Ordinal.Equals(item.Region, expectedRegion)
                || !observations.TryGetValue((item.BucketName, item.ObjectKey, item.VersionId), out var observed)
                || observed.Length != item.Length
                || !StringComparer.OrdinalIgnoreCase.Equals(observed.Sha256, item.Sha256)
                || observed.RetainUntilUtc != item.ObservedRetainUntilUtc
                || observed.LegalHold
                || observed.RetainUntilUtc > executionUtc
                || !observed.RequiredReplicaComplete)
                throw new InvalidOperationException(
                    "AWS retention execution stopped because exact-version state, retention, legal hold, or replica evidence drifted.");
        }
        return new AwsApprovedRetentionExecution(
            plan.PlanId, plan.Revision, approval.ApprovalReference, exact);
    }

    static AwsRetentionPlanObject[] ExactObjects(AwsResolvedCatalogRestorePoint point)
    {
        var objects = point.ImmutableObjects ?? throw new InvalidDataException(
            "The verified AWS catalog did not provide a complete immutable object-version set.");
        if (objects.Length == 0)
            throw new InvalidDataException("An AWS restore point contains no exact immutable object versions.");
        return objects.Select(static value => new AwsRetentionPlanObject(
                value.BucketName,
                value.Region,
                value.ObjectKey,
                value.VersionId,
                value.Length,
                value.Sha256,
                value.RetainUntilUtc))
            .OrderBy(static value => value.ObjectKey, StringComparer.Ordinal)
            .ThenBy(static value => value.VersionId, StringComparer.Ordinal)
            .ToArray();
    }

    static HashSet<DatabaseRestorePointId> ResolveDependencyClosure(
        IEnumerable<AwsResolvedCatalogRestorePoint> retained,
        IReadOnlyList<AwsResolvedCatalogRestorePoint> all)
    {
        var byId = all.ToDictionary(static point => point.Entry.RestorePointId);
        var protectedIds = new HashSet<DatabaseRestorePointId>();
        var pending = new Queue<DatabaseRestorePointId>(
            retained.SelectMany(static point => point.RestorePoint.Manifest.Dependencies));
        while (pending.TryDequeue(out var dependency))
        {
            if (!protectedIds.Add(dependency)) continue;
            if (!byId.TryGetValue(dependency, out var point))
                throw new InvalidDataException("A retained AWS restore point has a missing dependency.");
            foreach (var parent in point.RestorePoint.Manifest.Dependencies) pending.Enqueue(parent);
        }
        return protectedIds;
    }
}
