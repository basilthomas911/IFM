using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsRetentionPlanPolicyTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Gate14")]
    public void Dry_run_protects_newest_and_the_complete_dependency_closure()
    {
        var replica = new DatabaseArtifactReplicaId("aws-primary");
        var parent = Point("parent", Now.AddDays(-10), []);
        var independent = Point("independent", Now.AddDays(-9), []);
        var newest = Point("newest", Now.AddDays(-1), [new DatabaseRestorePointId("parent")]);
        var request = new DatabaseRetentionEvaluationRequest(
            new DatabaseRetentionPlanId(Guid.NewGuid()),
            3,
            Now.AddDays(-2),
            replica,
            [],
            [],
            []);

        var plan = AwsRetentionPlanPolicy.Create(request, 17, [parent, independent, newest], Now);

        plan.RestorePoints.Select(static value => value.RestorePointId.Value).Should().Equal("independent");
        plan.DependencyProtectedRestorePoints.Select(static value => value.Value).Should().Equal("parent");
        plan.RetainedRestorePointCount.Should().Be(2);
        plan.ExpectedReclaimedBytes.Should().Be(16);
        plan.RestorePoints.Single().Objects.Should().OnlyContain(static value =>
            value.ObjectKey == "v1/independent" && value.VersionId == "version-independent");
    }

    [Fact]
    [Trait("Category", "Gate14")]
    public void Exact_revision_approved_expired_object_is_executable()
    {
        var (plan, approval, observations, options) = ExecutionFixture();

        var result = AwsRetentionPlanPolicy.ValidateExecution(plan, approval, observations, options, Now);

        result.Objects.Should().ContainSingle();
        result.Objects[0].VersionId.Should().Be("version-old");
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("policy")]
    [InlineData("approval")]
    [InlineData("legal-hold")]
    [InlineData("retention")]
    [InlineData("replica")]
    [InlineData("checksum")]
    [InlineData("bucket")]
    [InlineData("missing")]
    [Trait("Category", "Gate14")]
    public void Stale_or_drifted_execution_fails_before_deletion(string drift)
    {
        var (plan, approval, observations, options) = ExecutionFixture();
        if (drift == "revision") approval = approval with { Revision = approval.Revision + 1 };
        if (drift == "policy") approval = approval with { PolicyRevision = approval.PolicyRevision + 1 };
        if (drift == "approval") approval = approval with { ApprovalReference = "" };
        if (drift == "missing") observations = [];
        if (observations.Count != 0)
        {
            var observed = observations.Single();
            observed = drift switch
            {
                "legal-hold" => observed with { LegalHold = true },
                "retention" => observed with { RetainUntilUtc = Now.AddDays(1) },
                "replica" => observed with { RequiredReplicaComplete = false },
                "checksum" => observed with { Sha256 = new string('B', 64) },
                "bucket" => observed with { BucketName = options.RecoveryBucketName },
                _ => observed
            };
            observations = [observed];
        }

        var action = () => AwsRetentionPlanPolicy.ValidateExecution(plan, approval, observations, options, Now);

        action.Should().Throw<InvalidOperationException>();
    }

    static (AwsRetentionPlanDocument Plan, AwsRetentionExecutionApproval Approval,
        IReadOnlyCollection<AwsRetentionObjectObservation> Observations, AwsCloudDatabaseBackupOptions Options)
        ExecutionFixture()
    {
        var options = Options();
        var planId = new DatabaseRetentionPlanId(Guid.NewGuid());
        var objectPlan = new AwsRetentionPlanObject(
            options.PrimaryBucketName,
            options.PrimaryRegion,
            "v1/old",
            "version-old",
            16,
            new string('A', 64),
            Now.AddMinutes(-1));
        var plan = new AwsRetentionPlanDocument
        {
            PlanId = planId,
            Revision = 4,
            PolicyRevision = 17,
            ReplicaId = new DatabaseArtifactReplicaId("aws-primary"),
            EvaluationBoundaryUtc = Now.AddDays(-1),
            CreatedUtc = Now.AddHours(-1),
            RestorePoints = [new AwsRetentionPlanRestorePoint(
                new DatabaseRestorePointId("old"), DatabaseEngine.PostgreSql, [objectPlan])],
            DependencyProtectedRestorePoints = [],
            RetainedRestorePointCount = 1,
            ExpectedReclaimedBytes = 16
        };
        var approval = new AwsRetentionExecutionApproval(planId, 4, 17, "CAB-2026-001");
        IReadOnlyCollection<AwsRetentionObjectObservation> observations =
        [
            new AwsRetentionObjectObservation(
                objectPlan.BucketName,
                objectPlan.Region,
                objectPlan.ObjectKey,
                objectPlan.VersionId,
                objectPlan.Length,
                objectPlan.Sha256,
                objectPlan.ObservedRetainUntilUtc,
                LegalHold: false,
                RequiredReplicaComplete: true)
        ];
        return (plan, approval, observations, options);
    }

    static AwsResolvedCatalogRestorePoint Point(
        string id,
        DateTimeOffset published,
        DatabaseRestorePointId[] dependencies)
    {
        var restorePoint = new DatabaseRestorePointId(id);
        var replica = new DatabaseArtifactReplicaId("aws-primary");
        var protectionSet = new DatabaseProtectionSetId("postgresql-core");
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var descriptor = new AwsImmutableObjectVersion
        {
            BucketName = Options().PrimaryBucketName,
            Region = Options().PrimaryRegion,
            ObjectKey = $"v1/{id}",
            VersionId = $"version-{id}",
            Length = 16,
            Sha256 = new string('A', 64),
            S3ChecksumSha256 = "checksum",
            EncryptionKeyArn = Options().PrimaryEncryptionKeyArn,
            EncryptionContextBase64 = "e30=",
            ObjectLockMode = "Governance",
            RetainUntilUtc = published.AddDays(1),
            PublishedUtc = published
        };
        var manifest = new DatabaseBackupManifest
        {
            ManifestId = $"manifest-{id}",
            OperationId = operation,
            RestorePointId = restorePoint,
            Engine = DatabaseEngine.PostgreSql,
            ProtectionSetId = protectionSet,
            SafeBoundaryReference = $"boundary-{id}",
            CreatedUtc = published,
            Dependencies = dependencies,
            Artifacts = [new DatabaseArtifactDigest(descriptor.ObjectKey, descriptor.Length, descriptor.Sha256)],
            Replicas = [replica],
            BackupLineage = new DatabaseBackupLineage
            {
                RequestedMode = dependencies.Length == 0 ? DatabaseBackupMode.Full : DatabaseBackupMode.Incremental,
                ResolvedMode = dependencies.Length == 0 ? DatabaseBackupMode.Full : DatabaseBackupMode.Incremental,
                NativeKind = dependencies.Length == 0
                    ? DatabaseNativeBackupKind.PostgreSqlBase : DatabaseNativeBackupKind.PostgreSqlIncremental,
                BaseRestorePointId = dependencies.Length == 0 ? restorePoint : new DatabaseRestorePointId("parent"),
                ParentRestorePointId = dependencies.SingleOrDefault(),
                ChainDepth = dependencies.Length
            }
        };
        var entry = new AwsCatalogEntry
        {
            RestorePointId = restorePoint,
            ReplicaId = replica,
            ProtectionSetId = protectionSet,
            Engine = DatabaseEngine.PostgreSql,
            PublicationRecord = descriptor,
            PublicationRecordSha256 = new string('A', 64),
            PublishedUtc = published
        };
        var publication = new AwsPublicationRecord
        {
            OperationId = operation,
            RestorePointId = restorePoint,
            ReplicaId = replica,
            ProtectionSetId = protectionSet,
            Engine = DatabaseEngine.PostgreSql,
            Artifacts = [new AwsPublishedArtifact("native/data", descriptor)],
            EngineManifest = descriptor,
            EngineManifestSha256 = new string('A', 64),
            EngineManifestSignature = new AwsSignatureEnvelope
            {
                KeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing",
                Algorithm = "ECDSA_SHA_256",
                DigestAlgorithm = "SHA-256",
                DigestBase64 = "digest",
                SignatureBase64 = "signature",
                SignedUtc = published
            },
            Dependencies = dependencies,
            ProducingHostId = "host-1",
            BuildIdentity = "test",
            PublishedUtc = published,
            VerifiedUtc = published
        };
        return new AwsResolvedCatalogRestorePoint(
            entry,
            publication,
            new DatabaseCatalogRestorePoint(
                new DatabaseCatalogEntry(1, restorePoint, manifest.ManifestId, 1, DatabaseEngine.PostgreSql,
                    protectionSet, replica, descriptor.ObjectKey, descriptor.ObjectKey, published),
                manifest,
                descriptor.Length,
                1),
            [descriptor]);
    }

    static AwsCloudDatabaseBackupOptions Options() => new()
    {
        PrimaryBucketName = "ifm-primary-development",
        RecoveryBucketName = "ifm-recovery-development",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/primary",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/recovery"
    };
}
