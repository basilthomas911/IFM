using System.Text.Json;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.SecurityToken;
using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed partial class LiveAwsPublicationAndSigningIntegrationTests
{
    /// <summary>Rechecks existing retention-drill catalogs and exact artifacts without publishing or deleting objects.</summary>
    [Fact]
    [Trait("Category", "LiveAwsGate14ReadOnly")]
    public async Task Existing_gate14_candidate_and_preserved_backup_catalogs_verify_in_both_vaults()
    {
        if (Environment.GetEnvironmentVariable("IFM_GATE14_READONLY_TESTS") != "1") return;
        var root = Environment.GetEnvironmentVariable("IFM_GATE14_READONLY_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("An explicit evidence directory is required.");
        Directory.CreateDirectory(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var options = LiveOptions();
        using var primary = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(credentials, options.RecoveryReadRoleArn,
            "gate14-readonly-" + Guid.NewGuid().ToString("N"), new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recovery = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        var observations = new List<object>();
        foreach (var replica in new[] { "aws-primary", "aws-recovery" })
        {
            var client = replica == "aws-primary" ? primary : recovery;
            var vault = replica == "aws-primary" ? null : new AwsVaultLocation(options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn, new DatabaseArtifactReplicaId(replica));
            var store = new S3ImmutableObjectStore(client, options, TimeProvider.System);
            var catalog = new S3DatabaseBackupCatalog(client, store, signer, options, vault);
            foreach (var id in new[] { "3291d2ff9099419aa6c9b3e08ecde95b", "b4c472227f044f419ac975f29a2463af" })
            {
                var point = await catalog.ResolveAwsAsync(new DatabaseRestorePointId(id), new DatabaseArtifactReplicaId(replica), timeout.Token);
                point.Entry.ProtectionSetId.Value.Should().Be("postgresql-gate14-disposable-051f030cd7ab4e3c88a7913ad6928127");
                point.RestorePoint.VerifiedArtifactCount.Should().Be(3);
                point.RestorePoint.VerifiedBytes.Should().Be(40586585);
                point.ImmutableObjects.Should().NotBeNull();
                observations.Add(new { restorePointId = id, replica, point.RestorePoint.VerifiedArtifactCount, point.RestorePoint.VerifiedBytes, point.Entry.PublishedUtc, exactObjects = point.ImmutableObjects });
            }
        }
        await File.WriteAllTextAsync(Path.Combine(root, "gate14-signed-catalog-verification.json"), JsonSerializer.Serialize(new { observedUtc = DateTimeOffset.UtcNow, mutationPerformed = false, deletionAuthorized = false, observations }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
    }
}
