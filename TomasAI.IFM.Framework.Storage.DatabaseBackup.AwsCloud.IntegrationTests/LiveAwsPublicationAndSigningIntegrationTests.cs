using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class LiveAwsPublicationAndSigningIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Development_vault_produces_exact_immutable_read_back_verified_evidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var s3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        var clock = TimeProvider.System;
        var store = new S3ImmutableObjectStore(s3, options, clock);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var key = new AwsBackupObjectKeyFactory("development").Evidence(operationId, "gate6-live-object");
        var document = DatabaseBackupCanonicalJson.Serialize(new
        {
            schemaVersion = 1,
            qualification = "gates-6-7",
            operationId = operationId.Format(),
            createdUtc = DateTimeOffset.UtcNow
        });
        var retainUntil = DateTimeOffset.UtcNow.AddDays(options.DefaultRetentionDays + 1);

        var published = await store.UploadAsync(
            key,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(document, writable: false)),
            document.LongLength,
            retainUntil,
            new Dictionary<string, string>
            {
                ["environment"] = "development",
                ["operationId"] = operationId.Format(),
                ["qualification"] = "gates-6-7"
            },
            CancellationToken.None);
        var downloaded = await store.DownloadBoundedAsync(published, options.MaximumSignedDocumentBytes, CancellationToken.None);
        downloaded.Should().Equal(document);

        output.WriteLine("OperationId={0}", operationId.Format());
        output.WriteLine("ObjectKey={0}", published.ObjectKey);
        output.WriteLine("VersionId={0}", published.VersionId);
        output.WriteLine("RetainUntilUtc={0:O}", published.RetainUntilUtc);
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Development_vault_resumes_multipart_upload_and_rejects_duplicate_and_corrupt_evidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        options.MultipartPartSizeBytes = 5 * 1024 * 1024;
        options.MultipartThresholdBytes = options.MultipartPartSizeBytes;
        using var s3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var dynamo = new AmazonDynamoDBClient(RegionEndpoint.CACentral1);
        var checkpoints = new DynamoDbMultipartCheckpointStore(dynamo, options);
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System, checkpoints);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var key = new AwsBackupObjectKeyFactory("development").Evidence(operationId, "gate6-live-multipart");
        var content = new byte[options.MultipartPartSizeBytes + 1];
        RandomNumberGenerator.Fill(content);
        var retainUntil = DateTimeOffset.UtcNow.AddDays(options.DefaultRetentionDays + 1);
        var encryptionContext = new Dictionary<string, string>
        {
            ["environment"] = "development",
            ["operationId"] = operationId.Format(),
            ["qualification"] = "gate-6-multipart-resume"
        };
        var encodedContext = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            encryptionContext.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToDictionary())));
        var initiated = await s3.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = options.PrimaryBucketName,
            Key = key.Value,
            ChecksumAlgorithm = ChecksumAlgorithm.SHA256,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
            ServerSideEncryptionKeyManagementServiceKeyId = options.PrimaryEncryptionKeyArn,
            ServerSideEncryptionKeyManagementServiceEncryptionContext = encodedContext,
            ObjectLockMode = ObjectLockMode.Governance,
            ObjectLockRetainUntilDate = retainUntil.UtcDateTime
        });
        var uploadId = initiated.UploadId ?? throw new InvalidOperationException("S3 returned no multipart upload ID.");
        var completed = false;
        try
        {
            await using (var firstPart = new MemoryStream(content, 0, options.MultipartPartSizeBytes, writable: false))
            {
                await s3.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = options.PrimaryBucketName,
                    Key = key.Value,
                    UploadId = uploadId,
                    PartNumber = 1,
                    PartSize = options.MultipartPartSizeBytes,
                    InputStream = firstPart,
                    ChecksumSHA256 = Convert.ToBase64String(SHA256.HashData(content.AsSpan(0, options.MultipartPartSizeBytes)))
                });
            }
            await checkpoints.WriteAsync(new AwsMultipartCheckpoint(
                options.PrimaryBucketName, key.Value, uploadId, 1, options.MultipartPartSizeBytes, DateTimeOffset.UtcNow),
                CancellationToken.None);

            var published = await store.UploadAsync(
                key,
                _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
                content.LongLength,
                retainUntil,
                encryptionContext,
                CancellationToken.None);
            completed = true;
            (await checkpoints.ReadAsync(key, CancellationToken.None)).Should().BeNull();

            var duplicate = () => store.UploadAsync(
                key,
                _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
                content.LongLength,
                retainUntil,
                encryptionContext,
                CancellationToken.None).AsTask();
            await duplicate.Should().ThrowAsync<InvalidOperationException>();

            var corrupt = () => store.VerifyAsync(
                published with { Sha256 = new string('0', 64) }, CancellationToken.None).AsTask();
            await corrupt.Should().ThrowAsync<InvalidDataException>();

            output.WriteLine("OperationId={0}", operationId.Format());
            output.WriteLine("ObjectKey={0}", published.ObjectKey);
            output.WriteLine("VersionId={0}", published.VersionId);
            output.WriteLine("Length={0}", published.Length);
            output.WriteLine("ResumedUploadId={0}", uploadId);
        }
        finally
        {
            if (!completed)
                await s3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = options.PrimaryBucketName,
                    Key = key.Value,
                    UploadId = uploadId
                });
        }
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Development_publication_catalog_rebuilds_from_signed_immutable_records()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var s3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var artifacts = new InMemoryNativeArtifactSource("native/postgresql-base.bin", RandomNumberGenerator.GetBytes(4096));
        var publisher = new S3DatabaseBackupPublicationCapability(
            artifacts, store, signer, options,
            new DatabaseBackupHostOptions { HostId = "gate6-live-publication" }, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(s3, store, signer, options);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var protectionSet = new DatabaseProtectionSetId("postgresql-core");
        var request = new DatabaseBackupPublicationRequest(
            operationId,
            protectionSet,
            DatabaseEngine.PostgreSql,
            "gate6-live-safe-boundary",
            [new DatabaseLogicalDestination("aws-primary", true)],
            BackupLineage: new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                BaseRestorePointId = new DatabaseRestorePointId(operationId.Format())
            });

        await publisher.ValidateAsync(new DatabaseBackupPublicationPreflightRequest(
            protectionSet, DatabaseEngine.PostgreSql, request.RequiredDestinations), CancellationToken.None);
        var published = await publisher.PublishAsync(request, CancellationToken.None);
        var replicaId = new DatabaseArtifactReplicaId("aws-primary");
        var resolved = await catalog.ResolveAsync(published.RestorePointId, replicaId, CancellationToken.None);
        var enumerated = await catalog.EnumerateAsync(replicaId, CancellationToken.None);
        var rebuilt = await catalog.RebuildAsync(CancellationToken.None);

        resolved.Entry.RestorePointId.Should().Be(published.RestorePointId);
        resolved.Manifest.OperationId.Should().Be(operationId);
        resolved.VerifiedArtifactCount.Should().Be(1);
        enumerated.Should().ContainSingle(item => item.Entry.RestorePointId == published.RestorePointId);
        rebuilt.Should().ContainSingle(item => item.Entry.RestorePointId == published.RestorePointId);
        rebuilt.Single(item => item.Entry.RestorePointId == published.RestorePointId).Manifest
            .Should().BeEquivalentTo(resolved.Manifest);

        output.WriteLine("OperationId={0}", operationId.Format());
        output.WriteLine("RestorePointId={0}", published.RestorePointId.Value);
        output.WriteLine("ManifestId={0}", published.ManifestId);
        output.WriteLine("VerifiedBytes={0}", resolved.VerifiedBytes);
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Development_publication_replicates_and_verifies_through_recovery_role()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var primaryS3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(primaryS3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(primaryKms, options, TimeProvider.System);
        var artifacts = new InMemoryNativeArtifactSource("native/recovery-probe.bin", RandomNumberGenerator.GetBytes(2048));
        var publisher = new S3DatabaseBackupPublicationCapability(
            artifacts, store, signer, options,
            new DatabaseBackupHostOptions { HostId = "gate6-live-replication" }, TimeProvider.System);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var protectionSet = new DatabaseProtectionSetId("postgresql-core");
        var published = await publisher.PublishAsync(new DatabaseBackupPublicationRequest(
            operationId,
            protectionSet,
            DatabaseEngine.PostgreSql,
            "gate6-live-replication-boundary",
            [new DatabaseLogicalDestination("aws-primary", true)],
            BackupLineage: new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                BaseRestorePointId = new DatabaseRestorePointId(operationId.Format())
            }), CancellationToken.None);

        var sourceCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(
            sourceCredentials,
            options.RecoveryReadRoleArn,
            $"gate6-recovery-{Environment.ProcessId}",
            new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recoveryS3 = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        var recoveryStore = new S3ImmutableObjectStore(recoveryS3, options, TimeProvider.System);
        var recoveryCatalog = new S3DatabaseBackupCatalog(
            recoveryS3,
            recoveryStore,
            signer,
            options,
            new AwsVaultLocation(
                options.RecoveryBucketName,
                options.RecoveryRegion,
                options.RecoveryEncryptionKeyArn,
                new DatabaseArtifactReplicaId("aws-recovery")));

        var started = DateTimeOffset.UtcNow;
        var deadline = started.AddMinutes(5);
        DatabaseCatalogRestorePoint? recovered = null;
        Exception? lastTransientFailure = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                recovered = await recoveryCatalog.ResolveAsync(
                    published.RestorePointId, new DatabaseArtifactReplicaId("aws-recovery"), CancellationToken.None);
                break;
            }
            catch (AmazonServiceException exception) when (exception.ErrorCode is "AccessDenied" or "AccessDeniedException")
            {
                throw;
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or AmazonS3Exception)
            {
                lastTransientFailure = exception;
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
        if (recovered is null)
            throw new TimeoutException("The signed restore point did not become recovery-readable within five minutes.", lastTransientFailure);

        recovered.Entry.ReplicaId.Should().Be(new DatabaseArtifactReplicaId("aws-recovery"));
        recovered.Manifest.OperationId.Should().Be(operationId);
        recovered.VerifiedArtifactCount.Should().Be(1);
        var catalogKey = new AwsBackupObjectKeyFactory("development")
            .Catalog(published.RestorePointId, new DatabaseArtifactReplicaId("aws-primary")).Value;
        var catalogVersions = await recoveryS3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = options.RecoveryBucketName,
            Prefix = catalogKey,
            MaxKeys = 2
        });
        var catalogVersion = (catalogVersions.Versions ?? []).Single(value =>
            value.IsDeleteMarker != true && StringComparer.Ordinal.Equals(value.Key, catalogKey));
        var delete = () => recoveryS3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = options.RecoveryBucketName,
            Key = catalogKey,
            VersionId = catalogVersion.VersionId
        });
        await delete.Should().ThrowAsync<AmazonS3Exception>()
            .Where(exception => exception.ErrorCode == "AccessDenied"
                || exception.ErrorCode == "AccessDeniedException");
        output.WriteLine("OperationId={0}", operationId.Format());
        output.WriteLine("RestorePointId={0}", published.RestorePointId.Value);
        output.WriteLine("RecoveryReplicaId={0}", recovered.Entry.ReplicaId.Value);
        output.WriteLine("DeniedDeleteVersionId={0}", catalogVersion.VersionId);
        output.WriteLine("ReplicationRecoverySeconds={0:F3}", (DateTimeOffset.UtcNow - started).TotalSeconds);
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Development_signing_key_produces_online_and_offline_verifiable_evidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var document = DatabaseBackupCanonicalJson.Serialize(new
        {
            schemaVersion = 1,
            qualification = "gate-7",
            operationId = operationId.Format(),
            createdUtc = DateTimeOffset.UtcNow
        });
        var signature = await signer.SignAsync(document, CancellationToken.None);
        await signer.VerifyAsync(document, signature, CancellationToken.None);
        var trustedKey = await signer.ExportTrustedPublicKeyAsync(
            signature.SignedUtc.AddMinutes(-1), signature.SignedUtc.AddDays(options.DefaultRetentionDays + 1),
            CancellationToken.None);
        var trustBundle = new AwsRecoveryTrustBundle
        {
            Environment = "development",
            Revision = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            Keys = [trustedKey]
        };
        AwsOfflineSignatureVerifier.Verify(document, signature, trustBundle, DateTimeOffset.UtcNow.AddSeconds(1));
        var changed = document.ToArray();
        changed[^1] ^= 0x01;
        var verifyChanged = () => AwsOfflineSignatureVerifier.Verify(
            changed, signature, trustBundle, DateTimeOffset.UtcNow.AddSeconds(1));
        verifyChanged.Should().Throw<CryptographicException>();

        output.WriteLine("OperationId={0}", operationId.Format());
        output.WriteLine("SigningKeyArn={0}", signature.KeyArn);
        output.WriteLine("PublicKeySha256={0}", trustedKey.PublicKeySha256);
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate7LiveQualification")]
    public async Task Development_recovery_read_role_is_denied_signing_key_use()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        var sourceCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(
            sourceCredentials,
            options.RecoveryReadRoleArn,
            $"gate7-denial-{Environment.ProcessId}",
            new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var deniedKms = new AmazonKeyManagementServiceClient(recoveryCredentials, RegionEndpoint.CACentral1);
        var signer = new KmsDocumentSignatureService(deniedKms, options, TimeProvider.System);
        var document = DatabaseBackupCanonicalJson.Serialize(new
        {
            schemaVersion = 1,
            qualification = "gate-7-recovery-role-denial",
            operationId = Guid.NewGuid().ToString("N")
        });

        var sign = () => signer.SignAsync(document, CancellationToken.None).AsTask();
        await sign.Should().ThrowAsync<AmazonKeyManagementServiceException>()
            .Where(exception => exception.ErrorCode == "AccessDenied"
                || exception.ErrorCode == "AccessDeniedException");
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate9LiveQualification")]
    [Trait("Category", "Gate10LiveQualification")]
    public async Task Development_full_plus_six_incrementals_stage_from_primary_and_recovery_vaults()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var primaryS3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(primaryS3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(primaryKms, options, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(primaryS3, store, signer, options);
        var walArchive = new AwsPostgreSqlWalArchive(primaryS3, store, signer, options, TimeProvider.System);
        var protectionSet = new DatabaseProtectionSetId($"postgresql-gate9-{Guid.NewGuid():N}");
        var baseOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var baseRestorePoint = new DatabaseRestorePointId(baseOperation.Format());
        var parent = baseRestorePoint;
        var expectedArtifacts = new Dictionary<DatabaseRestorePointId, byte[]>();

        for (var depth = 0; depth <= options.MaximumIncrementalChainDepth; depth++)
        {
            var operation = depth == 0 ? baseOperation : new DatabaseRecoveryOperationId(Guid.NewGuid());
            var restorePoint = new DatabaseRestorePointId(operation.Format());
            var content = RandomNumberGenerator.GetBytes(1024 + depth);
            expectedArtifacts.Add(restorePoint, content);
            var source = new InMemoryNativeArtifactSource($"native/depth-{depth}.bin", content);
            var publisher = new S3DatabaseBackupPublicationCapability(
                source, store, signer, options,
                new DatabaseBackupHostOptions { HostId = "gate9-live-chain" }, TimeProvider.System);
            var lineage = depth == 0
                ? new DatabaseBackupLineage
                {
                    RequestedMode = DatabaseBackupMode.Full,
                    ResolvedMode = DatabaseBackupMode.Full,
                    NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                    BaseRestorePointId = baseRestorePoint,
                    ChainDepth = 0
                }
                : new DatabaseBackupLineage
                {
                    RequestedMode = DatabaseBackupMode.Incremental,
                    ResolvedMode = DatabaseBackupMode.Incremental,
                    NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
                    BaseRestorePointId = baseRestorePoint,
                    ParentRestorePointId = parent,
                    ChainDepth = depth
                };
            await publisher.PublishAsync(new DatabaseBackupPublicationRequest(
                operation,
                protectionSet,
                DatabaseEngine.PostgreSql,
                $"gate9-live-chain-depth-{depth}",
                [new DatabaseLogicalDestination("aws-primary", true)],
                Dependencies: depth == 0 ? [] : [parent],
                BackupLineage: lineage), CancellationToken.None);
            parent = restorePoint;
        }

        var sourceCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(
            sourceCredentials,
            options.RecoveryReadRoleArn,
            $"gate10-recovery-{Environment.ProcessId}",
            new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recoveryVault = new AwsRecoveryVaultClient(
            new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1));

        var primarySink = new InMemoryRestoreArtifactSink();
        var primarySource = new S3DatabaseRestoreSourceCapability(
            primaryS3, catalog, primarySink, walArchive, options, recoveryVault, signer, TimeProvider.System);
        var primaryPrepared = await primarySource.PrepareAsync(new DatabaseRestoreSourceRequest(
            new DatabaseRecoveryOperationId(Guid.NewGuid()), parent, DatabaseEngine.PostgreSql,
            new DatabaseArtifactReplicaId("aws-primary")), CancellationToken.None);
        AssertPreparedChain(primaryPrepared, parent, expectedArtifacts, primarySink);

        var recoveryStarted = DateTimeOffset.UtcNow;
        var recoveryDeadline = recoveryStarted.AddMinutes(5);
        DatabasePreparedRestoreSource? recoveryPrepared = null;
        InMemoryRestoreArtifactSink? recoverySink = null;
        Exception? lastTransientFailure = null;
        while (DateTimeOffset.UtcNow < recoveryDeadline)
        {
            try
            {
                recoverySink = new InMemoryRestoreArtifactSink();
                var recoverySource = new S3DatabaseRestoreSourceCapability(
                    primaryS3, catalog, recoverySink, walArchive, options, recoveryVault, signer, TimeProvider.System);
                recoveryPrepared = await recoverySource.PrepareAsync(new DatabaseRestoreSourceRequest(
                    new DatabaseRecoveryOperationId(Guid.NewGuid()), parent, DatabaseEngine.PostgreSql,
                    new DatabaseArtifactReplicaId("aws-recovery")), CancellationToken.None);
                break;
            }
            catch (AmazonServiceException exception) when (exception.ErrorCode is "AccessDenied" or "AccessDeniedException")
            {
                throw;
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or AmazonS3Exception)
            {
                lastTransientFailure = exception;
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
        if (recoveryPrepared is null || recoverySink is null)
            throw new TimeoutException("The full incremental chain did not become recovery-readable within five minutes.", lastTransientFailure);
        AssertPreparedChain(recoveryPrepared, parent, expectedArtifacts, recoverySink);

        output.WriteLine("ProtectionSetId={0}", protectionSet.Value);
        output.WriteLine("BaseRestorePointId={0}", baseRestorePoint.Value);
        output.WriteLine("FinalRestorePointId={0}", parent.Value);
        output.WriteLine("IncrementalDepth={0}", options.MaximumIncrementalChainDepth);
        output.WriteLine("RecoveryReplicationSeconds={0:F3}", (DateTimeOffset.UtcNow - recoveryStarted).TotalSeconds);
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate9LiveQualification")]
    public async Task Development_signed_wal_archive_detects_gap_then_replication_verifies_continuity()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        var options = LiveOptions();
        using var primaryS3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(primaryS3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(primaryKms, options, TimeProvider.System);
        var archive = new AwsPostgreSqlWalArchive(primaryS3, store, signer, options, TimeProvider.System);
        var protectionSet = new DatabaseProtectionSetId($"postgresql-wal-{Guid.NewGuid():N}");
        const string timeline = "00000001";
        var completedUtc = DateTimeOffset.UtcNow;

        async Task<PostgreSqlWalArchiveRecord> PublishAsync(int ordinal)
        {
            var segment = timeline + ordinal.ToString("X16");
            var content = Encoding.UTF8.GetBytes($"gate9-live-wal-{protectionSet.Value}-{segment}");
            var request = new PostgreSqlWalArchiveRequest(
                protectionSet, timeline, segment, content.LongLength,
                Convert.ToHexString(SHA256.HashData(content)), completedUtc.AddSeconds(ordinal));
            return await archive.PublishAsync(
                request,
                _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
                CancellationToken.None);
        }

        var first = await PublishAsync(1);
        _ = await PublishAsync(3);
        var gap = await archive.InspectContinuityAsync(protectionSet, timeline, CancellationToken.None);
        gap.Contiguous.Should().BeFalse();
        gap.MissingSegments.Should().Equal(timeline + 2.ToString("X16"));
        var second = await PublishAsync(2);
        var contiguous = await archive.InspectContinuityAsync(protectionSet, timeline, CancellationToken.None);
        contiguous.Contiguous.Should().BeTrue();
        contiguous.SegmentCount.Should().Be(3);

        var replayContent = Encoding.UTF8.GetBytes($"gate9-live-wal-{protectionSet.Value}-{first.SegmentName}");
        var replay = await archive.PublishAsync(
            new PostgreSqlWalArchiveRequest(
                protectionSet, timeline, first.SegmentName, replayContent.LongLength,
                Convert.ToHexString(SHA256.HashData(replayContent)), completedUtc.AddSeconds(1)),
            _ => ValueTask.FromResult<Stream>(new MemoryStream(replayContent, writable: false)),
            CancellationToken.None);
        replay.Object.VersionId.Should().Be(first.Object.VersionId);

        var sourceCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(
            sourceCredentials,
            options.RecoveryReadRoleArn,
            $"gate9-wal-recovery-{Environment.ProcessId}",
            new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recoveryS3 = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        var recoveryStore = new S3ImmutableObjectStore(recoveryS3, options, TimeProvider.System);
        var recoveryArchive = new AwsPostgreSqlWalArchive(
            recoveryS3, recoveryStore, signer, options, TimeProvider.System,
            new AwsVaultLocation(
                options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn,
                new DatabaseArtifactReplicaId("aws-recovery")));
        var recoveryStarted = DateTimeOffset.UtcNow;
        var recoveryDeadline = recoveryStarted.AddMinutes(5);
        PostgreSqlWalContinuityStatus? recovered = null;
        Exception? lastTransientFailure = null;
        while (DateTimeOffset.UtcNow < recoveryDeadline)
        {
            try
            {
                var candidate = await recoveryArchive.InspectContinuityAsync(protectionSet, timeline, CancellationToken.None);
                if (candidate.Contiguous && candidate.SegmentCount == 3)
                {
                    recovered = candidate;
                    break;
                }
            }
            catch (AmazonServiceException exception) when (exception.ErrorCode is "AccessDenied" or "AccessDeniedException")
            {
                throw;
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or AmazonS3Exception)
            {
                lastTransientFailure = exception;
            }
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        if (recovered is null)
            throw new TimeoutException("The signed WAL stream did not become recovery-readable within five minutes.", lastTransientFailure);

        output.WriteLine("ProtectionSetId={0}", protectionSet.Value);
        output.WriteLine("Timeline={0}", timeline);
        output.WriteLine("FirstWalVersionId={0}", first.Object.VersionId);
        output.WriteLine("FilledGapWalVersionId={0}", second.Object.VersionId);
        output.WriteLine("RecoveryReplicationSeconds={0:F3}", (DateTimeOffset.UtcNow - recoveryStarted).TotalSeconds);
    }

    static void AssertPreparedChain(
        DatabasePreparedRestoreSource prepared,
        DatabaseRestorePointId expectedFinal,
        IReadOnlyDictionary<DatabaseRestorePointId, byte[]> expectedArtifacts,
        InMemoryRestoreArtifactSink sink)
    {
        prepared.NativeRestorePointId.Should().Be(expectedFinal);
        prepared.DependencyChain.Should().HaveCount(expectedArtifacts.Count - 1);
        prepared.VerifiedArtifactCount.Should().Be(expectedArtifacts.Count);
        prepared.VerifiedBytes.Should().Be(expectedArtifacts.Values.Sum(static value => value.LongLength));
        sink.Artifacts.Should().HaveCount(expectedArtifacts.Count);
        foreach (var expected in expectedArtifacts)
            sink.Artifacts[expected.Key].Single().Value.Should().Equal(expected.Value);
    }

    static AwsCloudDatabaseBackupOptions LiveOptions() => new()
    {
        Enabled = true,
        LiveAwsTestsEnabled = true,
        Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250",
        PrimaryVaultAccountId = "107651266250",
        RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-db-backup-development-primary-107651266250",
        RecoveryBucketName = "ifm-db-backup-development-recovery-107651266250",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-database-backup-upload-development",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-database-backup-recovery-read-development",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/4772d4b1-82d9-49fc-acca-b97e73fe93df",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/4277d9a7-5182-4299-a61a-19ca0c5cf404",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/2edd60e5-be19-483d-b4df-88df45aa2fb2"
    };

    sealed class InMemoryNativeArtifactSource(string relativePath, byte[] content) : IDatabaseNativeArtifactSource
    {
        public ValueTask<IReadOnlyList<DatabaseNativeArtifactDescriptor>> DescribeAsync(
            DatabaseEngine engine, DatabaseRecoveryOperationId operationId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<DatabaseNativeArtifactDescriptor>>(
                [new DatabaseNativeArtifactDescriptor(relativePath, content.LongLength)]);

        public ValueTask<Stream> OpenReadAsync(
            DatabaseEngine engine, DatabaseRecoveryOperationId operationId, string requestedRelativePath,
            CancellationToken cancellationToken)
        {
            if (!StringComparer.Ordinal.Equals(relativePath, requestedRelativePath))
                throw new FileNotFoundException("The requested live qualification artifact is unavailable.");
            return ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false));
        }
    }

    sealed class InMemoryRestoreArtifactSink : IDatabaseNativeRestoreArtifactSink
    {
        public Dictionary<DatabaseRestorePointId, Dictionary<string, byte[]>> Artifacts { get; } = [];

        public ValueTask PrepareFreshAsync(
            DatabaseEngine engine, DatabaseRestorePointId restorePointId, CancellationToken cancellationToken)
        {
            engine.Should().Be(DatabaseEngine.PostgreSql);
            Artifacts.Add(restorePointId, new Dictionary<string, byte[]>(StringComparer.Ordinal));
            return ValueTask.CompletedTask;
        }

        public async ValueTask WriteAsync(
            DatabaseEngine engine,
            DatabaseRestorePointId restorePointId,
            string relativePath,
            Stream source,
            long expectedLength,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            engine.Should().Be(DatabaseEngine.PostgreSql);
            using var content = new MemoryStream();
            await source.CopyToAsync(content, cancellationToken);
            var bytes = content.ToArray();
            bytes.LongLength.Should().Be(expectedLength);
            Convert.ToHexString(SHA256.HashData(bytes)).Should().BeEquivalentTo(expectedSha256);
            Artifacts[restorePointId].Add(relativePath, bytes);
        }
    }
}
