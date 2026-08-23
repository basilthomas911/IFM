using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;
using static TomasAI.IFM.Framework.Storage.IntegratedTests.PostgreSqlBackupCapabilityIntegrationTests;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class AwsToPostgreSqlRestoreIntegrationTests : IDisposable
{
    const string ConnectionVariable = "IFM_GATE10_AWS_POSTGRES_CONNECTION";
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate10-aws-native", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Gate10AwsNativeLiveQualification")]
    public async Task Signed_full_plus_six_chain_from_each_vault_feeds_the_native_restore_capability()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;

        Environment.SetEnvironmentVariable(ConnectionVariable,
            "Host=127.0.0.1;Port=5432;Database=postgres;Username=gate10;Password=gate10-test-secret;SSL Mode=Disable");
        var captureOptions = PostgreSqlOptions(Path.Combine(_root, "capture-backup"), Path.Combine(_root, "capture-restore"));
        var captureRunner = new DeterministicPostgreSqlNativeRunner(nativeMajorVersion: 17);
        var capture = new PostgreSqlBackupCapability(
            captureOptions, captureRunner, new SyntheticPostgreSqlTargetValidator("unused"),
            new SyntheticPostgreSqlSourceMetadataReader("7543210987654321000", 17, true));
        var source = new LocalDatabaseNativeArtifactSource(captureOptions, ScyllaOptions("capture"));
        var aws = LiveOptions();
        using var primaryS3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var objects = new S3ImmutableObjectStore(primaryS3, aws, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(primaryKms, aws, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(primaryS3, objects, signer, aws);
        var wal = new AwsPostgreSqlWalArchive(primaryS3, objects, signer, aws, TimeProvider.System);
        var publisher = new S3DatabaseBackupPublicationCapability(
            source, objects, signer, aws, new DatabaseBackupHostOptions { HostId = "gate10-aws-native" },
            TimeProvider.System);
        var protectionSet = new DatabaseProtectionSetId($"postgresql-gate10-native-{Guid.NewGuid():N}");
        var baseOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var baseRestorePoint = new DatabaseRestorePointId(baseOperation.Format());
        var parent = baseRestorePoint;
        DatabaseRestorePointId finalRestorePoint = default;

        for (var depth = 0; depth <= 6; depth++)
        {
            var operation = depth == 0 ? baseOperation : new DatabaseRecoveryOperationId(Guid.NewGuid());
            var restorePoint = new DatabaseRestorePointId(operation.Format());
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
            var boundary = await capture.CreateBaseBackupAsync(
                new PostgreSqlBackupRequest(operation, new DatabaseProtectionSetId("core-postgresql"), lineage),
                new Progress<DatabaseNativeProgress>(), CancellationToken.None);
            _ = await capture.VerifyAsync(
                new PostgreSqlVerificationRequest(operation, boundary.SafeBoundaryReference, boundary.BackupLineage),
                CancellationToken.None);
            await publisher.PublishAsync(new DatabaseBackupPublicationRequest(
                operation, protectionSet, DatabaseEngine.PostgreSql, boundary.SafeBoundaryReference,
                [new DatabaseLogicalDestination("aws-primary", true)],
                Dependencies: depth == 0 ? [] : [parent],
                Statistics: boundary.Statistics,
                BackupLineage: boundary.BackupLineage), CancellationToken.None);
            parent = restorePoint;
            finalRestorePoint = restorePoint;
        }

        var sourceCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var sts = new AmazonSecurityTokenServiceClient(sourceCredentials, RegionEndpoint.CACentral1);
        var assumed = await sts.AssumeRoleAsync(new AssumeRoleRequest
        {
            RoleArn = aws.RecoveryReadRoleArn,
            RoleSessionName = $"gate10-native-{Environment.ProcessId}",
            ExternalId = "ifm-database-backup-development"
        }, CancellationToken.None);
        var recoveryCredentials = new SessionAWSCredentials(
            assumed.Credentials.AccessKeyId, assumed.Credentials.SecretAccessKey, assumed.Credentials.SessionToken);
        using var recoveryS3 = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        using var recoveryVault = new AwsRecoveryVaultClient(recoveryS3);

        foreach (var replica in new[]
                 {
                     new DatabaseArtifactReplicaId("aws-primary"),
                     new DatabaseArtifactReplicaId("aws-recovery")
                 })
        {
            var stagedOptions = PostgreSqlOptions(
                Path.Combine(_root, replica.Value, "backup"), Path.Combine(_root, replica.Value, "restore"));
            var sink = new LocalDatabaseNativeRestoreArtifactSink(stagedOptions, ScyllaOptions(replica.Value));
            var restoreSource = new S3DatabaseRestoreSourceCapability(
                primaryS3, catalog, sink, wal, aws, recoveryVault, signer, TimeProvider.System);
            DatabasePreparedRestoreSource? prepared = null;
            Exception? transient = null;
            var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    prepared = await restoreSource.PrepareAsync(new DatabaseRestoreSourceRequest(
                        new DatabaseRecoveryOperationId(Guid.NewGuid()), finalRestorePoint,
                        DatabaseEngine.PostgreSql, replica), CancellationToken.None);
                    break;
                }
                catch (Exception exception) when (replica.Value == "aws-recovery"
                    && exception is FileNotFoundException or InvalidDataException or AmazonS3Exception)
                {
                    transient = exception;
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            if (prepared is null)
                throw new TimeoutException($"The {replica.Value} chain did not become restore-ready.", transient);

            var restoreRunner = new DeterministicPostgreSqlNativeRunner(nativeMajorVersion: 17);
            var native = new PostgreSqlBackupCapability(
                stagedOptions, restoreRunner, new SyntheticPostgreSqlTargetValidator("synthetic-gate6-incremental-row"));
            var restored = await native.RestoreToFreshTargetAsync(new PostgreSqlRestoreRequest(
                    new DatabaseRecoveryOperationId(Guid.NewGuid()), prepared.NativeRestorePointId,
                    new DatabaseFreshTargetDescriptor("disposable-validation", "gate10"),
                    prepared.DependencyChain),
                new Progress<DatabaseNativeProgress>(), CancellationToken.None);

            restored.Succeeded.Should().BeTrue();
            prepared.DependencyChain.Should().HaveCount(6);
            restoreRunner.Invocations.Should().ContainSingle(value => value.Tool == PostgreSqlNativeTool.CombineBackup
                && !value.Arguments.SequenceEqual(new[] { "--version" }));
        }
    }

    PostgreSqlBackupOptions PostgreSqlOptions(string backupRoot, string restoreRoot) => new()
    {
        ToolDirectory = Path.Combine(_root, "tools"),
        BackupRoot = backupRoot,
        RestoreRoot = restoreRoot,
        ConnectionStringEnvironmentVariable = ConnectionVariable,
        AllowedProtectionSets = ["core-postgresql"],
        FreshTargetProfiles = new Dictionary<string, PostgreSqlFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["disposable-validation"] = new()
            {
                Host = "127.0.0.1", Port = 55433, Database = "postgres",
                AllowedLogicalTargets = ["gate10"], StartupTimeout = TimeSpan.FromSeconds(5)
            }
        },
        MinimumMajorVersion = 17,
        MaximumMajorVersion = 17,
        ProcessTimeout = TimeSpan.FromMinutes(1),
        RequirePersistentBackupRoot = false
    };

    ScyllaBackupOptions ScyllaOptions(string name) => new()
    {
        BackupRoot = Path.Combine(_root, name, "scylla-backup")
    };

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

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConnectionVariable, null);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
