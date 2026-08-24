using System.Security.Cryptography;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsImmutablePublicationTests
{
    [Fact]
    public void Generated_keys_reject_traversal_and_preserve_the_versioned_schema()
    {
        var factory = new AwsBackupObjectKeyFactory("development");
        var key = factory.EngineManifest(new DatabaseProtectionSetId("postgresql-core"),
            DatabaseEngine.PostgreSql, new DatabaseRestorePointId("restore-1"));

        key.Value.Should().Be("v1/environment/development/protection-set/postgresql-core/engine/postgresql/restore-point/restore-1/manifests/engine-manifest-v2.json");
        var action = () => factory.Artifact(new DatabaseProtectionSetId("postgresql-core"),
            DatabaseEngine.PostgreSql, new DatabaseRestorePointId("restore-1"),
            new DatabaseArtifactId("artifact-1"), "../secret");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Offline_trust_bundle_verifies_golden_signature_and_rejects_changed_document()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var content = "canonical-publication-v1"u8.ToArray();
        var digest = SHA256.HashData(content);
        var signature = key.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence);
        var arn = "arn:aws:kms:ca-central-1:107651266250:key/golden";
        var signedUtc = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var envelope = new AwsSignatureEnvelope
        {
            KeyArn = arn, Algorithm = "ECDSA_SHA_256", DigestAlgorithm = "SHA-256",
            DigestBase64 = Convert.ToBase64String(digest), SignatureBase64 = Convert.ToBase64String(signature), SignedUtc = signedUtc
        };
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var bundle = new AwsRecoveryTrustBundle
        {
            Environment = "development", Revision = 2, CreatedUtc = signedUtc,
            Keys =
            [
                new AwsRecoveryTrustedKey(arn, "ECC_NIST_P256", "SIGN_VERIFY", "ECDSA_SHA_256",
                    Convert.ToBase64String(publicKey), Convert.ToHexString(SHA256.HashData(publicKey)),
                    signedUtc.AddDays(-1), signedUtc.AddDays(35))
            ]
        };

        AwsOfflineSignatureVerifier.Verify(content, envelope, bundle, signedUtc.AddMinutes(1));

        var action = () => AwsOfflineSignatureVerifier.Verify("changed"u8, envelope, bundle, signedUtc.AddMinutes(1));
        action.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task Single_part_upload_captures_exact_version_encryption_retention_and_checksum()
    {
        var content = "immutable"u8.ToArray();
        var hash = SHA256.HashData(content);
        var options = Options();
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListVersionsAsync(Arg.Any<ListVersionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListVersionsResponse { Versions = [] });
        PutObjectRequest? put = null;
        s3.PutObjectAsync(Arg.Do<PutObjectRequest>(value => put = value), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { VersionId = "version-1", ChecksumSHA256 = Convert.ToBase64String(hash) });
        ConfigureReadBack(s3, options, content, "version-1", DateTimeOffset.UtcNow.AddDays(35));
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);

        var result = await store.UploadAsync(new AwsGeneratedObjectKey("v1/environment/development/test/object"),
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)), content.Length,
            DateTimeOffset.UtcNow.AddDays(35), new Dictionary<string, string> { ["operationId"] = "test" }, CancellationToken.None);

        result.VersionId.Should().Be("version-1");
        result.Sha256.Should().Be(Convert.ToHexString(hash));
        put!.ServerSideEncryptionKeyManagementServiceKeyId.Should().Be(options.PrimaryEncryptionKeyArn);
        put.ObjectLockMode.Should().Be(ObjectLockMode.Governance);
        put.ChecksumAlgorithm.Should().Be(ChecksumAlgorithm.SHA256);
        put.ChecksumSHA256.Should().Be(Convert.ToBase64String(hash));
    }

    [Fact]
    public async Task Lost_single_part_response_resolves_the_one_exact_immutable_version()
    {
        var content = "ambiguous-success"u8.ToArray();
        var hash = SHA256.HashData(content);
        var key = new AwsGeneratedObjectKey("v1/environment/development/test/ambiguous");
        var options = Options();
        var retainUntil = DateTimeOffset.UtcNow.AddDays(35);
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListVersionsAsync(Arg.Any<ListVersionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ListVersionsResponse { Versions = [] },
                new ListVersionsResponse
                {
                    Versions = [new S3ObjectVersion { Key = key.Value, VersionId = "resolved-version" }]
                });
        s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutObjectResponse>>(_ => throw new AmazonS3Exception("response lost")
            {
                StatusCode = HttpStatusCode.InternalServerError
            });
        ConfigureReadBack(s3, options, content, "resolved-version", retainUntil);
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);

        var result = await store.UploadAsync(key,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)), content.Length,
            retainUntil, new Dictionary<string, string> { ["operationId"] = "ambiguous" }, CancellationToken.None);

        result.VersionId.Should().Be("resolved-version");
        result.Sha256.Should().Be(Convert.ToHexString(hash));
        await s3.Received(2).ListVersionsAsync(Arg.Any<ListVersionsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exact_version_verification_rejects_wrong_encryption_key_and_short_retention()
    {
        var content = "metadata"u8.ToArray();
        var options = Options();
        var expected = Expected(options, content);
        var s3 = Substitute.For<IAmazonS3>();
        s3.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse
            {
                ContentLength = content.Length, VersionId = expected.VersionId,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                ServerSideEncryptionKeyManagementServiceKeyId = options.RecoveryEncryptionKeyArn,
                ObjectLockMode = ObjectLockMode.Governance,
                ObjectLockRetainUntilDate = expected.RetainUntilUtc.AddDays(-1).UtcDateTime,
                ChecksumSHA256 = expected.S3ChecksumSha256
            });
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);

        var action = () => store.VerifyAsync(expected, CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<InvalidDataException>();
        await s3.DidNotReceive().GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Gate10Negative")]
    public async Task Exact_version_verification_rejects_truncated_content()
    {
        var content = "complete-content"u8.ToArray();
        var options = Options();
        var expected = Expected(options, content);
        var s3 = Substitute.For<IAmazonS3>();
        ConfigureReadBack(s3, options, content, expected.VersionId, expected.RetainUntilUtc);
        s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(content[..^1], writable: false),
                ContentLength = content.Length - 1,
                VersionId = expected.VersionId
            });
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);

        var action = () => store.VerifyAsync(expected, CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Multipart_boundary_uses_bounded_parts_and_completes_before_readback()
    {
        var options = Options();
        options.MultipartPartSizeBytes = 5 * 1024 * 1024;
        options.MultipartThresholdBytes = options.MultipartPartSizeBytes;
        var content = new byte[options.MultipartPartSizeBytes + 1];
        RandomNumberGenerator.Fill(content);
        var hash = SHA256.HashData(content);
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListVersionsAsync(Arg.Any<ListVersionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListVersionsResponse { Versions = [] });
        s3.InitiateMultipartUploadAsync(Arg.Any<InitiateMultipartUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InitiateMultipartUploadResponse { UploadId = "upload-1" });
        s3.ListPartsAsync(Arg.Any<ListPartsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListPartsResponse { Parts = [], IsTruncated = false });
        var part = 0;
        s3.UploadPartAsync(Arg.Any<UploadPartRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new UploadPartResponse
            {
                ETag = $"etag-{++part}",
                ChecksumSHA256 = call.Arg<UploadPartRequest>().ChecksumSHA256
            });
        s3.CompleteMultipartUploadAsync(Arg.Any<CompleteMultipartUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompleteMultipartUploadResponse { VersionId = "version-multipart", ChecksumSHA256 = Convert.ToBase64String(hash) });
        ConfigureReadBack(s3, options, content, "version-multipart", DateTimeOffset.UtcNow.AddDays(35));
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);

        var result = await store.UploadAsync(new AwsGeneratedObjectKey("v1/environment/development/test/multipart"),
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)), content.LongLength,
            DateTimeOffset.UtcNow.AddDays(35), new Dictionary<string, string> { ["operationId"] = "multipart" }, CancellationToken.None);

        result.VersionId.Should().Be("version-multipart");
        await s3.Received(2).UploadPartAsync(Arg.Any<UploadPartRequest>(), Arg.Any<CancellationToken>());
        await s3.Received(1).CompleteMultipartUploadAsync(
            Arg.Is<CompleteMultipartUploadRequest>(request => request.PartETags.Count == 2
                && request.PartETags.All(static item => !string.IsNullOrWhiteSpace(item.ChecksumSHA256))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Gate10Negative")]
    public async Task Wal_archive_rejects_wrong_timeline_before_any_aws_call()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var signer = Substitute.For<IAwsDocumentSignatureService>();
        var options = Options();
        var archive = new AwsPostgreSqlWalArchive(
            s3, new S3ImmutableObjectStore(s3, options, TimeProvider.System), signer, options, TimeProvider.System);
        var request = new PostgreSqlWalArchiveRequest(new DatabaseProtectionSetId("postgresql-core"),
            "00000002", "000000010000000000000001", 16, new string('A', 64), DateTimeOffset.UtcNow);

        var action = () => archive.PublishAsync(request,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(new byte[16])), CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<ArgumentException>();
        s3.ReceivedCalls().Should().BeEmpty();
    }

    static void ConfigureReadBack(
        IAmazonS3 s3, AwsCloudDatabaseBackupOptions options, byte[] content,
        string versionId, DateTimeOffset retainUntil)
    {
        s3.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse
            {
                ContentLength = content.LongLength, VersionId = versionId,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                ServerSideEncryptionKeyManagementServiceKeyId = options.PrimaryEncryptionKeyArn,
                ObjectLockMode = ObjectLockMode.Governance,
                ObjectLockRetainUntilDate = retainUntil.UtcDateTime,
                ChecksumSHA256 = Convert.ToBase64String(SHA256.HashData(content))
            });
        s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => new GetObjectResponse
            {
                ResponseStream = new MemoryStream(content, writable: false), ContentLength = content.LongLength,
                VersionId = versionId
            });
    }

    static AwsImmutableObjectVersion Expected(AwsCloudDatabaseBackupOptions options, byte[] content)
    {
        var checksum = SHA256.HashData(content);
        return new AwsImmutableObjectVersion
        {
            BucketName = options.PrimaryBucketName,
            Region = options.PrimaryRegion,
            ObjectKey = "v1/environment/development/test/exact",
            VersionId = "exact-version",
            Length = content.LongLength,
            Sha256 = Convert.ToHexString(checksum),
            S3ChecksumSha256 = Convert.ToBase64String(checksum),
            EncryptionKeyArn = options.PrimaryEncryptionKeyArn,
            EncryptionContextBase64 = Convert.ToBase64String("{}"u8),
            ObjectLockMode = "Governance",
            RetainUntilUtc = DateTimeOffset.UtcNow.AddDays(35),
            PublishedUtc = DateTimeOffset.UtcNow
        };
    }

    static AwsCloudDatabaseBackupOptions Options() => new()
    {
        Enabled = true, Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250", PrimaryVaultAccountId = "107651266250", RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1", RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-primary-development", RecoveryBucketName = "ifm-recovery-development",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-upload-development",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-recovery-development",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/11111111-1111-1111-1111-111111111111",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/22222222-2222-2222-2222-222222222222",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/33333333-3333-3333-3333-333333333333"
    };
}
