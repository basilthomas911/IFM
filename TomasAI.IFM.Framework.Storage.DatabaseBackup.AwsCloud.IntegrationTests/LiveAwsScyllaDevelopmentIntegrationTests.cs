using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using FluentAssertions;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class LiveAwsScyllaDevelopmentIntegrationTests(ITestOutputHelper output) : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-scylla-aws-development", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate11ScyllaDevelopment")]
    [Trait("Category", "Gate12ScyllaDevelopment")]
    public async Task Manager_snapshot_round_trips_through_each_immutable_AWS_vault_and_fresh_target()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal)
            || !string.Equals(Environment.GetEnvironmentVariable("IFM_SCYLLA_DEVELOPMENT_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;
        var snapshotTag = Environment.GetEnvironmentVariable("IFM_SCYLLA_DEVELOPMENT_SNAPSHOT_TAG");
        if (string.IsNullOrWhiteSpace(snapshotTag))
            throw new InvalidOperationException("IFM_SCYLLA_DEVELOPMENT_SNAPSHOT_TAG is required for the live Scylla qualification.");

        Directory.CreateDirectory(_root);
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var sourceRoot = Path.Combine(_root, "source", operation.Format());
        Directory.CreateDirectory(sourceRoot);
        var references = (await DockerAsync([
                "exec", "ifm-scylla-manager", "sctool", "backup", "files",
                "--cluster", "ifm-development", "--location", "s3:ifm-development",
                "--snapshot-tag", snapshotTag, "--delimiter", "|", "--with-version"])).Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.StartsWith("s3://", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        references.Should().NotBeEmpty();

        var scyllaOptions = new ScyllaBackupOptions
        {
            PortableSnapshot = new ScyllaPortableSnapshotOptions
            {
                Enabled = true,
                ServiceUrl = "http://127.0.0.1:19000",
                AccessKeyIdEnvironmentVariable = "MINIO_ROOT_USER",
                SecretAccessKeyEnvironmentVariable = "MINIO_ROOT_PASSWORD",
                MaximumObjectCount = 1_000_000,
                MaximumTotalBytes = 512L * 1024 * 1024 * 1024
            }
        };
        using var transport = new S3ScyllaSnapshotArtifactTransport(scyllaOptions);
        var exportedBytes = await transport.ExportAsync(
            "s3:ifm-development", snapshotTag, references, sourceRoot, CancellationToken.None);
        exportedBytes.Should().BeGreaterThan(0);

        var aws = LiveOptions();
        using var primaryS3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var objects = new S3ImmutableObjectStore(primaryS3, aws, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(primaryKms, aws, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(primaryS3, objects, signer, aws);
        var wal = new AwsPostgreSqlWalArchive(primaryS3, objects, signer, aws, TimeProvider.System);
        var publisher = new S3DatabaseBackupPublicationCapability(
            new DirectoryArtifactSource(sourceRoot), objects, signer, aws,
            new DatabaseBackupHostOptions { HostId = "scylla-development-live-qualification" }, TimeProvider.System);
        var restorePoint = new DatabaseRestorePointId(operation.Format());
        var topology = new ScyllaTopologyEvidence("ifm-development", 1, 1, true);
        var schemaLines = references.Where(static line => line.Contains("/backup/schema/", StringComparison.Ordinal)).ToArray();
        var snapshot = new ScyllaSnapshotEvidence(
            snapshotTag,
            "backup/gate11-development-aws-clean",
            Sha256(string.Join('\n', schemaLines.Length == 0 ? references : schemaLines)),
            Sha256(string.Join('\n', references)),
            references.Select(ParseUnit).Where(static value => value is not null)
                .Select(static value => value!.Value.Keyspace).Distinct(StringComparer.Ordinal).Count(),
            references.Select(ParseUnit).Where(static value => value is not null).Distinct().Count(),
            references.Length,
            "6.2.2",
            "3.4.2");
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot,
            NativeIdentity = "ifm-development:ifm_manager_validation",
            BaseRestorePointId = restorePoint,
            ChainDepth = 0
        };
        var publication = await publisher.PublishAsync(new DatabaseBackupPublicationRequest(
            operation,
            new DatabaseProtectionSetId("scylla-development-validation"),
            DatabaseEngine.ScyllaDb,
            "scylla-snapshot-" + snapshot.NativeManifestSha256[..16],
            [new DatabaseLogicalDestination("aws-primary", true), new DatabaseLogicalDestination("aws-recovery", true)],
            Dependencies: [],
            BackupLineage: lineage,
            ScyllaTopology: topology,
            ScyllaSnapshot: snapshot), CancellationToken.None);
        publication.RestorePointId.Should().Be(restorePoint);

        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var sts = new AmazonSecurityTokenServiceClient(credentials, RegionEndpoint.CACentral1);
        var assumed = await sts.AssumeRoleAsync(new AssumeRoleRequest
        {
            RoleArn = aws.RecoveryReadRoleArn,
            RoleSessionName = $"scylla-development-{Environment.ProcessId}",
            ExternalId = "ifm-database-backup-development"
        }, CancellationToken.None);
        using var recoveryS3 = new AmazonS3Client(new Amazon.Runtime.SessionAWSCredentials(
            assumed.Credentials.AccessKeyId, assumed.Credentials.SecretAccessKey, assumed.Credentials.SessionToken),
            RegionEndpoint.CAWest1);
        using var recoveryVault = new AwsRecoveryVaultClient(recoveryS3);

        await StartRestoreTargetAsync();
        try
        {
            foreach (var drill in new[]
                     {
                         (Replica: "aws-primary", Bucket: "ifm-development-restore-primary"),
                         (Replica: "aws-recovery", Bucket: "ifm-development-restore-recovery")
                     })
            {
                var stagedRoot = Path.Combine(_root, drill.Replica);
                var sink = new DirectoryArtifactSink(stagedRoot);
                var restoreSource = new S3DatabaseRestoreSourceCapability(
                    primaryS3, catalog, sink, wal, aws, recoveryVault, signer, TimeProvider.System);
                DatabasePreparedRestoreSource? prepared = null;
                Exception? transient = null;
                var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    try
                    {
                        prepared = await restoreSource.PrepareAsync(new DatabaseRestoreSourceRequest(
                            new DatabaseRecoveryOperationId(Guid.NewGuid()), restorePoint, DatabaseEngine.ScyllaDb,
                            new DatabaseArtifactReplicaId(drill.Replica)), CancellationToken.None);
                        break;
                    }
                    catch (Exception exception) when (drill.Replica == "aws-recovery"
                        && exception is FileNotFoundException or InvalidDataException or AmazonS3Exception)
                    {
                        transient = exception;
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
                if (prepared is null)
                    throw new TimeoutException($"The {drill.Replica} Scylla publication did not become restore-ready.", transient);
                prepared.ScyllaRecovery.Should().Be(new ScyllaRecoveryExpectation(topology, snapshot));
                var staged = Path.Combine(stagedRoot, restorePoint.Value);
                _ = await transport.EnsureAvailableAsync(
                    "s3:ifm-development", "s3:" + drill.Bucket, snapshotTag, staged, CancellationToken.None);
                await RestoreAndValidateAsync(drill.Bucket, snapshotTag, drill.Replica);
            }
        }
        finally
        {
            await StopRestoreTargetAsync();
        }

        output.WriteLine("OperationId={0}", operation.Format());
        output.WriteLine("RestorePointId={0}", restorePoint.Value);
        output.WriteLine("SnapshotTag={0}", snapshotTag);
        output.WriteLine("PortableBytes={0}", exportedBytes);
        output.WriteLine("ArtifactReferences={0}", references.Length);
    }

    async Task RestoreAndValidateAsync(string bucket, string snapshotTag, string replica)
    {
        _ = await DockerAsync(["exec", "ifm-scylla-restore-validation", "cqlsh", "-e",
            "DROP KEYSPACE IF EXISTS ifm_manager_validation;"]);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var schemaTask = (await DockerAsync([
            "exec", "ifm-scylla-manager", "sctool", "restore", "--cluster", "ifm-restore-validation",
            "--location", "s3:" + bucket, "--snapshot-tag", snapshotTag, "--num-retries=0",
            "--restore-schema", "--name", "ifm-schema-" + suffix])).Trim();
        await AwaitManagerTaskAsync(schemaTask);
        var tableTask = (await DockerAsync([
            "exec", "ifm-scylla-manager", "sctool", "restore", "--cluster", "ifm-restore-validation",
            "--location", "s3:" + bucket, "--snapshot-tag", snapshotTag, "--num-retries=0",
            "--keyspace", "ifm_manager_validation", "--restore-tables", "--name", "ifm-tables-" + suffix])).Trim();
        await AwaitManagerTaskAsync(tableTask);
        var query = await DockerAsync(["exec", "ifm-scylla-restore-validation", "cqlsh", "-e",
            "SELECT payload FROM ifm_manager_validation.restore_probe WHERE id = 11111111-1111-1111-1111-111111111111;"]);
        query.Should().Contain("scylla-manager-aws-development-restore-ok");
        output.WriteLine("{0}SchemaTask={1}", replica, schemaTask);
        output.WriteLine("{0}TableTask={1}", replica, tableTask);
    }

    static async Task AwaitManagerTaskAsync(string taskReference)
    {
        if (!taskReference.StartsWith("restore/", StringComparison.Ordinal))
            throw new InvalidDataException("Scylla Manager returned an invalid restore task reference.");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var tasks = await DockerAsync(["exec", "ifm-scylla-manager", "sctool", "tasks",
                "--cluster", "ifm-restore-validation"]);
            var row = tasks.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .SingleOrDefault(line => line.Contains(taskReference, StringComparison.Ordinal));
            if (row?.Contains("DONE", StringComparison.Ordinal) == true) return;
            if (row?.Contains("ERROR", StringComparison.Ordinal) == true
                || row?.Contains("ABORTED", StringComparison.Ordinal) == true
                || row?.Contains("STOPPED", StringComparison.Ordinal) == true)
                throw new InvalidOperationException("The Scylla Manager restore task failed: " + row);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        throw new TimeoutException("The Scylla Manager restore task exceeded ten minutes.");
    }

    static Task StartRestoreTargetAsync() => DockerAsync([
        "compose", "-f", "Docker/ScyllaManager/docker-compose.yml", "--profile", "validation",
        "up", "--detach", "--wait", "scylla-restore-validation"]);

    static Task StopRestoreTargetAsync() => DockerAsync([
        "compose", "-f", "Docker/ScyllaManager/docker-compose.yml", "--profile", "validation",
        "stop", "--timeout", "120", "scylla-restore-validation"], allowFailure: true);

    static async Task<string> DockerAsync(IReadOnlyList<string> arguments, bool allowFailure = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = FindRepositoryRoot()
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await standardOutput;
        var error = await standardError;
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException($"Docker failed with exit code {process.ExitCode}: {error}");
        return result.Trim();
    }

    static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TomasAI.IFM.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("The IFM repository root is unavailable.");
    }

    static (string Keyspace, string Table)? ParseUnit(string line)
    {
        var values = line.Split('|', StringSplitOptions.TrimEntries);
        if (values.Length < 2 || values[^1] == "./") return null;
        var unit = values[^1].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return unit.Length == 2 ? (unit[0], unit[1]) : null;
    }

    static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    sealed class DirectoryArtifactSource(string root) : IDatabaseNativeArtifactSource
    {
        public ValueTask<IReadOnlyList<DatabaseNativeArtifactDescriptor>> DescribeAsync(
            DatabaseEngine engine, DatabaseRecoveryOperationId operationId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<DatabaseNativeArtifactDescriptor>>(
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                    .Select(path => new DatabaseNativeArtifactDescriptor(
                        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                        new FileInfo(path).Length)).ToArray());

        public ValueTask<Stream> OpenReadAsync(
            DatabaseEngine engine, DatabaseRecoveryOperationId operationId, string relativePath,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<Stream>(new FileStream(
                Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    sealed class DirectoryArtifactSink(string root) : IDatabaseNativeRestoreArtifactSink
    {
        public ValueTask PrepareFreshAsync(
            DatabaseEngine engine, DatabaseRestorePointId restorePointId, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.Combine(root, restorePointId.Value));
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
            var targetRoot = Path.Combine(root, restorePointId.Value);
            var path = Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
                destination.Length.Should().Be(expectedLength);
            }
            await using var verify = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken))
                .Should().BeEquivalentTo(expectedSha256);
        }
    }
}
