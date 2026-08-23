using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsScyllaProtectionSetPolicyTests
{
    [Fact]
    [Trait("Category", "Gate11")]
    public void Complete_deduplicated_snapshot_is_eligible_without_an_ifm_dependency_chain()
    {
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Incremental,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot,
            BaseRestorePointId = new DatabaseRestorePointId("base-1"),
            ParentRestorePointId = new DatabaseRestorePointId("parent-1"),
            ChainDepth = 2,
            NativeIdentity = "cluster-a:keyspace-a"
        };

        var action = () => AwsScyllaProtectionSetPolicy.ValidatePublication(
            [], lineage, Topology(), Snapshot());

        action.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Gate11")]
    public void Fabricated_ifm_dependency_for_a_deduplicated_snapshot_is_rejected()
    {
        var action = () => AwsScyllaProtectionSetPolicy.ValidatePublication(
            [new DatabaseRestorePointId("parent-1")],
            new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Incremental,
                ResolvedMode = DatabaseBackupMode.Incremental,
                NativeKind = DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot,
                BaseRestorePointId = new DatabaseRestorePointId("base-1"),
                ParentRestorePointId = new DatabaseRestorePointId("parent-1"),
                ChainDepth = 1
            },
            Topology(), Snapshot());

        action.Should().Throw<InvalidDataException>().WithMessage("*logically complete*");
    }

    [Theory]
    [InlineData(0, 768, true)]
    [InlineData(3, 0, true)]
    [InlineData(3, 768, false)]
    [Trait("Category", "Gate11")]
    public void Missing_node_token_or_schema_coverage_prevents_eligibility(
        int liveNodes, int tokenRanges, bool schemaAgreement)
    {
        var action = () => AwsScyllaProtectionSetPolicy.ValidatePublication(
            [],
            new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.ScyllaManagerSnapshot
            },
            Topology() with
            {
                LiveNodeCount = liveNodes,
                TokenRangeCount = tokenRanges,
                SchemaAgreement = schemaAgreement
            },
            Snapshot());

        action.Should().Throw<InvalidDataException>().WithMessage("*topology*");
    }

    [Fact]
    [Trait("Category", "Gate12")]
    public void Signed_publication_creates_an_exact_restore_expectation()
    {
        var topology = Topology();
        var snapshot = Snapshot();
        var record = new AwsPublicationRecord
        {
            OperationId = new DatabaseRecoveryOperationId(Guid.NewGuid()),
            RestorePointId = new DatabaseRestorePointId("restore-1"),
            ReplicaId = new DatabaseArtifactReplicaId("aws-recovery"),
            ProtectionSetId = new DatabaseProtectionSetId("scylla-core"),
            Engine = DatabaseEngine.ScyllaDb,
            Artifacts = [],
            EngineManifest = Object(),
            EngineManifestSha256 = new string('A', 64),
            EngineManifestSignature = Signature(),
            Dependencies = [],
            ScyllaTopology = topology,
            ScyllaSnapshot = snapshot,
            ProducingHostId = "host-1",
            BuildIdentity = "test",
            PublishedUtc = DateTimeOffset.UtcNow,
            VerifiedUtc = DateTimeOffset.UtcNow
        };

        var result = AwsScyllaProtectionSetPolicy.CreateRecoveryExpectation(record);

        result.Should().Be(new ScyllaRecoveryExpectation(topology, snapshot));
    }

    static ScyllaTopologyEvidence Topology() => new("cluster-a", 3, 768, true);

    static ScyllaSnapshotEvidence Snapshot() => new(
        "sm_20260823000000UTC",
        "backup/11111111-1111-1111-1111-111111111111",
        new string('A', 64),
        new string('B', 64),
        2,
        12,
        48,
        "2025.1.4",
        "3.11.2");

    static AwsImmutableObjectVersion Object() => new()
    {
        BucketName = "recovery",
        Region = "ca-west-1",
        ObjectKey = "v1/test",
        VersionId = "version-1",
        Length = 1,
        Sha256 = new string('A', 64),
        S3ChecksumSha256 = "checksum",
        EncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/key",
        EncryptionContextBase64 = "e30=",
        ObjectLockMode = "Governance",
        RetainUntilUtc = DateTimeOffset.UtcNow.AddDays(35),
        PublishedUtc = DateTimeOffset.UtcNow
    };

    static Signing.AwsSignatureEnvelope Signature() => new()
    {
        KeyArn = "arn:aws:kms:ca-central-1:107651266250:key/key",
        Algorithm = "ECDSA_SHA_256",
        DigestAlgorithm = "SHA-256",
        DigestBase64 = "digest",
        SignatureBase64 = "signature",
        SignedUtc = DateTimeOffset.UtcNow
    };
}
