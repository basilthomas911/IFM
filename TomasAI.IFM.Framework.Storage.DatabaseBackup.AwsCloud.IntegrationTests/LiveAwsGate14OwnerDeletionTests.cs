using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using FluentAssertions;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed partial class LiveAwsPublicationAndSigningIntegrationTests
{
    /// <summary>Exercises the real signed-plan executor against only the owner's 16 disposable versions.</summary>
    [Fact]
    [Trait("Category", "LiveAwsGate14OwnerDeletion")]
    public async Task Owner_approved_disposable_versions_are_deleted_and_newer_backup_is_preserved()
    {
        if (Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_DELETION") != "1") return;
        var approvalPath = Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_APPROVAL")
            ?? throw new InvalidOperationException("Explicit owner approval inventory required.");
        var root = Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_EVIDENCE")
            ?? throw new InvalidOperationException("Persistent evidence directory required.");
        Directory.CreateDirectory(root);
        const string candidate = "3291d2ff9099419aa6c9b3e08ecde95b";
        const string keep = "b4c472227f044f419ac975f29a2463af";
        const string owner = "arn:aws:iam::107651266250:user/basil.thomas@live.ca";
        const string reference = "UserApproval-Gate14-16DisposableVersions-20261010";
        using var inventory = JsonDocument.Parse(await File.ReadAllTextAsync(approvalPath));
        var approved = inventory.RootElement;
        approved.GetProperty("environment").GetString().Should().Be("Development");
        approved.GetProperty("candidateRestorePointId").GetString().Should().Be(candidate);
        approved.GetProperty("preservedRestorePointId").GetString().Should().Be(keep);
        approved.GetProperty("approvalReference").GetString().Should().Be(reference);
        var expiry = approved.GetProperty("exceptionExpiresUtc").GetDateTimeOffset();
        expiry.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(10));
        var objects = approved.GetProperty("objects").Deserialize<AwsImmutableObjectVersion[]>()!;
        objects.Length.Should().Be(16);
        objects.Select(value => (value.BucketName, value.ObjectKey, value.VersionId)).Distinct().Count().Should().Be(16);
        objects.Should().OnlyContain(value => value.ObjectKey.Contains(candidate) && !value.ObjectKey.Contains(keep));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var token = timeout.Token;
        var options = LiveOptions();
        using var sts = new AmazonSecurityTokenServiceClient(RegionEndpoint.CACentral1);
        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), token);
        identity.Account.Should().Be(options.WorkloadAccountId);
        identity.Arn.Should().Be(owner);
        using var primary = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var recovery = new AmazonS3Client(RegionEndpoint.CAWest1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var actions = new List<object>();
        var catalogs = new Dictionary<string, S3DatabaseBackupCatalog>();
        var pairs = new Dictionary<string, AwsResolvedCatalogRestorePoint[]>();
        async Task Save(string phase) => await File.WriteAllTextAsync(Path.Combine(root, "execution-progress.json"),
            JsonSerializer.Serialize(new { phase, observedUtc = DateTimeOffset.UtcNow, approvalReference = reference, actions },
                new JsonSerializerOptions { WriteIndented = true }), token);
        try
        {
            // Verify BOTH replicas and the preserved backup before any protection mutation.
            foreach (var replica in new[] { "aws-primary", "aws-recovery" })
            {
                var client = replica == "aws-primary" ? primary : recovery;
                var vault = replica == "aws-primary" ? null : new AwsVaultLocation(options.RecoveryBucketName,
                    options.RecoveryRegion, options.RecoveryEncryptionKeyArn, new DatabaseArtifactReplicaId(replica));
                var catalog = new S3DatabaseBackupCatalog(client, new S3ImmutableObjectStore(client, options, TimeProvider.System), signer, options, vault);
                catalogs.Add(replica, catalog);
                var older = await catalog.ResolveAwsAsync(new DatabaseRestorePointId(candidate), new DatabaseArtifactReplicaId(replica), token);
                var newer = await catalog.ResolveAwsAsync(new DatabaseRestorePointId(keep), new DatabaseArtifactReplicaId(replica), token);
                older.Entry.ProtectionSetId.Value.Should().Be("postgresql-gate14-disposable-051f030cd7ab4e3c88a7913ad6928127");
                newer.Entry.ProtectionSetId.Should().Be(older.Entry.ProtectionSetId);
                newer.Entry.PublishedUtc.Should().BeAfter(older.Entry.PublishedUtc);
                older.RestorePoint.Manifest.Dependencies.Should().BeEmpty();
                newer.RestorePoint.Manifest.Dependencies.Should().BeEmpty();
                older.ImmutableObjects.Should().HaveCount(8);
                newer.ImmutableObjects.Should().HaveCount(8);
                foreach (var item in older.ImmutableObjects!)
                    objects.Should().ContainSingle(value => value.BucketName == item.BucketName && value.ObjectKey == item.ObjectKey
                        && value.VersionId == item.VersionId && value.Length == item.Length && value.Sha256 == item.Sha256);
                pairs.Add(replica, [older, newer]);
                foreach (var item in older.ImmutableObjects!)
                {
                    _ = await client.GetObjectRetentionAsync(new GetObjectRetentionRequest
                    { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    try
                    {
                        _ = await client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
                        { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    }
                    catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchObjectLockConfiguration") { }
                }
            }
            await Save("BothVaultsVerifiedBeforeMutation");
            // Owner-authorized exception only. Normal production executor never bypasses Object Lock.
            var retainUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            foreach (var item in objects)
            {
                var client = item.BucketName == options.PrimaryBucketName ? primary : recovery;
                await client.PutObjectLegalHoldAsync(new PutObjectLegalHoldRequest
                {
                    BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId,
                    LegalHold = new ObjectLockLegalHold { Status = new ObjectLockLegalHoldStatus("OFF") }
                }, token);
                actions.Add(new { action = "LegalHoldOff", item.BucketName, item.ObjectKey, item.VersionId });
                await Save("ChangingDisposableProtection");
                await client.PutObjectRetentionAsync(new PutObjectRetentionRequest
                {
                    BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId,
                    BypassGovernanceRetention = true,
                    Retention = new ObjectLockRetention { Mode = ObjectLockRetentionMode.Governance, RetainUntilDate = retainUntil.UtcDateTime }
                }, token);
                actions.Add(new { action = "ShortenedRetention", item.BucketName, item.ObjectKey, item.VersionId, retainUntil });
                await Save("ChangingDisposableProtection");
            }
            output.WriteLine("Waiting until disposable retention expires: {0:O}", retainUntil);
            while (DateTimeOffset.UtcNow <= retainUntil.AddSeconds(2)) await Task.Delay(TimeSpan.FromSeconds(1), token);
            foreach (var replica in new[] { "aws-primary", "aws-recovery" })
            {
                var client = replica == "aws-primary" ? primary : recovery;
                var points = pairs[replica];
                var observations = new List<AwsRetentionObjectObservation>();
                var revisedObjects = new List<AwsImmutableObjectVersion>();
                foreach (var item in points[0].ImmutableObjects!)
                {
                    var hold = await client.GetObjectLegalHoldAsync(new GetObjectLegalHoldRequest
                    { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    hold.LegalHold.Status.Should().Be(new ObjectLockLegalHoldStatus("OFF"));
                    var retention = await client.GetObjectRetentionAsync(new GetObjectRetentionRequest
                    { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    var observedRetainUntil = new DateTimeOffset(retention.Retention.RetainUntilDate!.Value.ToUniversalTime());
                    observedRetainUntil.Should().BeBefore(DateTimeOffset.UtcNow);
                    using var response = await client.GetObjectAsync(new GetObjectRequest
                    { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(response.ResponseStream, token));
                    hash.Should().Be(item.Sha256);
                    response.ContentLength.Should().Be(item.Length);
                    // Replica health was verified in both signed catalogs before the exception.
                    observations.Add(new AwsRetentionObjectObservation(item.BucketName, item.Region, item.ObjectKey,
                        item.VersionId, item.Length, hash, observedRetainUntil, false, true));
                    revisedObjects.Add(item with { RetainUntilUtc = observedRetainUntil });
                }
                // Freshly observed protection metadata replaces the original lock dates; content/lineage remain verified.
                var revised = points[0] with { ImmutableObjects = revisedObjects.ToArray() };
                var request = new DatabaseRetentionEvaluationRequest(new DatabaseRetentionPlanId(Guid.NewGuid()), 1,
                    points[0].Entry.PublishedUtc, new DatabaseArtifactReplicaId(replica), [new DatabaseRestorePointId(keep)], [], []);
                var plan = AwsRetentionPlanPolicy.Create(request, 1, [revised, points[1]], DateTimeOffset.UtcNow);
                plan.RestorePoints.Should().ContainSingle(point => point.RestorePointId.Value == candidate);
                plan.RetainedRestorePointCount.Should().Be(1);
                var bytes = DatabaseBackupCanonicalJson.Serialize(plan);
                var signature = await signer.SignAsync(bytes, token);
                await File.WriteAllBytesAsync(Path.Combine(root, replica + "-canonical-plan.json"), bytes, token);
                await File.WriteAllBytesAsync(Path.Combine(root, replica + "-signature.json"), DatabaseBackupCanonicalJson.Serialize(signature), token);
                var ownerApproval = new AwsRetentionExecutionApproval(plan.PlanId, 1, 1, reference);
                await File.WriteAllBytesAsync(Path.Combine(root, replica + "-approval.json"), DatabaseBackupCanonicalJson.Serialize(ownerApproval), token);
                var authorized = await new AwsRetentionPlanAuthorizationService(signer, options, TimeProvider.System)
                    .AuthorizeAsync(bytes, signature, ownerApproval, observations, token);
                var deleted = await new AwsExactVersionDeletionExecutor(client).ExecuteAsync(authorized, token);
                deleted.DeletedObjectVersionCount.Should().Be(8);
                actions.Add(new { action = "ExactVersionDeletion", replica, deleted });
                await Save("DeletionExecuted");
                foreach (var item in authorized.Objects)
                {
                    try
                    {
                        _ = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                        { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                        throw new InvalidOperationException("Deleted object version is still readable.");
                    }
                    catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound) { }
                }
                var preserved = await catalogs[replica].ResolveAwsAsync(new DatabaseRestorePointId(keep), new DatabaseArtifactReplicaId(replica), token);
                preserved.RestorePoint.VerifiedBytes.Should().Be(40586585);
                preserved.ImmutableObjects.Should().HaveCount(8);
                actions.Add(new { action = "PreservedBackupReverified", replica, preserved.RestorePoint.VerifiedBytes });
                await Save("PreservedBackupVerified");
            }
            await Save("Succeeded");
            await File.WriteAllTextAsync(Path.Combine(root, "completed.json"), JsonSerializer.Serialize(new
            {
                completedUtc = DateTimeOffset.UtcNow, environment = "Development", deletedVersions = 16,
                candidate, preservedRestorePointId = keep, preservedReplicasReverified = 2,
                applicationBackupsModified = false, normalExecutorUsed = true, ownerExceptionReference = reference
            }, new JsonSerializerOptions { WriteIndented = true }), token);
        }
        catch (Exception exception)
        {
            await File.WriteAllTextAsync(Path.Combine(root, "failed.json"), JsonSerializer.Serialize(new
            { failedUtc = DateTimeOffset.UtcNow, exception = exception.ToString(), actions }, new JsonSerializerOptions { WriteIndented = true }));
            throw;
        }
    }
    /// <summary>Publishes immutable signed audit evidence after rechecking deletion and the preserved catalogs.</summary>
    [Fact]
    [Trait("Category", "LiveAwsGate14OwnerEvidence")]
    public async Task Completed_owner_deletion_has_immutable_aws_audit_evidence()
    {
        if (Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_EVIDENCE_PUBLISH") != "1") return;
        var root = Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_EVIDENCE")
            ?? throw new InvalidOperationException("The completed deletion evidence directory is required.");
        using var completed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "completed.json")));
        completed.RootElement.GetProperty("deletedVersions").GetInt32().Should().Be(16);
        using var progress = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "execution-progress.json")));
        progress.RootElement.GetProperty("phase").GetString().Should().Be("Succeeded");
        var options = LiveOptions();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        using var sts = new AmazonSecurityTokenServiceClient(RegionEndpoint.CACentral1);
        (await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), token)).Arn.Should()
            .Be("arn:aws:iam::107651266250:user/basil.thomas@live.ca");
        using var primary = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var recovery = new AmazonS3Client(RegionEndpoint.CAWest1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var store = new S3ImmutableObjectStore(primary, options, TimeProvider.System);
        var evidence = new AwsRecoveryEvidenceStore(primary, store, signer, options, TimeProvider.System);
        var published = new List<object>();
        foreach (var replica in new[] { "aws-primary", "aws-recovery" })
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, replica + "-canonical-plan.json"), token);
            var plan = DatabaseBackupCanonicalJson.Deserialize<AwsRetentionPlanDocument>(bytes);
            var signature = DatabaseBackupCanonicalJson.Deserialize<AwsSignatureEnvelope>(
                await File.ReadAllBytesAsync(Path.Combine(root, replica + "-signature.json"), token));
            var approval = DatabaseBackupCanonicalJson.Deserialize<AwsRetentionExecutionApproval>(
                await File.ReadAllBytesAsync(Path.Combine(root, replica + "-approval.json"), token));
            await signer.VerifyAsync(bytes, signature, token);
            plan.RestorePoints.Should().ContainSingle(point => point.RestorePointId.Value == "3291d2ff9099419aa6c9b3e08ecde95b");
            var objects = plan.RestorePoints.Single().Objects;
            objects.Should().HaveCount(8);
            var client = replica == "aws-primary" ? primary : recovery;
            foreach (var item in objects)
            {
                try
                {
                    _ = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    { BucketName = item.BucketName, Key = item.ObjectKey, VersionId = item.VersionId }, token);
                    throw new InvalidOperationException("The deleted version became readable again.");
                }
                catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound) { }
            }
            var vault = replica == "aws-primary" ? null : new AwsVaultLocation(options.RecoveryBucketName,
                options.RecoveryRegion, options.RecoveryEncryptionKeyArn, new DatabaseArtifactReplicaId(replica));
            var catalog = new S3DatabaseBackupCatalog(client, new S3ImmutableObjectStore(client, options, TimeProvider.System), signer, options, vault);
            var keep = new DatabaseRestorePointId("b4c472227f044f419ac975f29a2463af");
            var remaining = await catalog.ResolveAwsAsync(keep, new DatabaseArtifactReplicaId(replica), token);
            remaining.ImmutableObjects.Should().HaveCount(8);
            var outcome = progress.RootElement.GetProperty("actions").EnumerateArray().Single(action =>
                action.GetProperty("action").GetString() == "ExactVersionDeletion" && action.GetProperty("replica").GetString() == replica).GetProperty("deleted");
            var result = new AwsRetentionDeletionResult(new DatabaseRetentionPlanId(outcome.GetProperty("PlanId").GetProperty("Value").GetGuid()),
                outcome.GetProperty("Revision").GetInt64(), outcome.GetProperty("DeletedObjectVersionCount").GetInt32(), outcome.GetProperty("DeletedBytes").GetInt64());
            result.PlanId.Should().Be(plan.PlanId);
            result.Revision.Should().Be(approval.Revision);
            result.DeletedBytes.Should().Be(plan.ExpectedReclaimedBytes);
            var evidenceReference = await evidence.WriteRetentionEvidenceAsync(new AwsRetentionExecutionEvidence(
                new DatabaseRecoveryOperationId(plan.PlanId.Value), plan, signature, approval, result, keep, true, DateTimeOffset.UtcNow), token);
            published.Add(new { replica, planId = plan.PlanId, evidenceReference, result });
        }
        await File.WriteAllTextAsync(Path.Combine(root, "immutable-audit-evidence.json"), JsonSerializer.Serialize(
            new { verifiedUtc = DateTimeOffset.UtcNow, published }, new JsonSerializerOptions { WriteIndented = true }), token);
    }}