using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
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

public sealed class LiveAwsCurrentScyllaDevelopmentIntegrationTests(ITestOutputHelper output) : IDisposable
{
    readonly string _root = Environment.GetEnvironmentVariable("IFM_CURRENT_SCYLLA_EXISTING_ROOT") ?? Path.Combine(Environment.GetEnvironmentVariable("IFM_CURRENT_SCYLLA_EVIDENCE_ROOT") ?? Path.GetTempPath(), "ifm-current-scylla-aws-development", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate11ScyllaDevelopment")]
    [Trait("Category", "Gate12ScyllaDevelopment")]
    public async Task Multi_node_Manager_snapshot_round_trips_through_each_immutable_AWS_vault_and_fresh_target()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal)
            || !string.Equals(Environment.GetEnvironmentVariable("IFM_CURRENT_SCYLLA_DEVELOPMENT_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;
        var snapshotTag = Environment.GetEnvironmentVariable("IFM_CURRENT_SCYLLA_DEVELOPMENT_SNAPSHOT_TAG");
        if (string.IsNullOrWhiteSpace(snapshotTag))
            throw new InvalidOperationException("IFM_CURRENT_SCYLLA_DEVELOPMENT_SNAPSHOT_TAG is required for the live Scylla qualification.");

        Directory.CreateDirectory(_root);
        var qualification = Stopwatch.StartNew();
        var operation = new DatabaseRecoveryOperationId(Environment.GetEnvironmentVariable("IFM_CURRENT_SCYLLA_OPERATION_ID") is { } operationId ? Guid.Parse(operationId) : Guid.NewGuid());
        var sourceRoot = Path.Combine(_root, "source", operation.Format());
        Directory.CreateDirectory(sourceRoot);
        var references = (await DockerAsync([
                "exec", "ifm-scylla-manager", "sctool", "backup", "files",
                "--cluster", "ifm-development", "--location", "s3:ifm-development",
                "--snapshot-tag", snapshotTag, "--delimiter", "|", "--with-version"])).Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.StartsWith("s3://", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        references.Should().NotBeEmpty();
        references.Select(ParseNodeId).Where(static value => value is not null).Distinct().Should().HaveCount(1);

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
        long exportedBytes;
        var portableManifest = Path.Combine(sourceRoot, "portable-snapshot", "manifest-v1.json");
        if (File.Exists(portableManifest))
        {
            using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(portableManifest));
            saved.RootElement.GetProperty("snapshotTag").GetString().Should().Be(snapshotTag);
            exportedBytes = saved.RootElement.GetProperty("totalBytes").GetInt64();
            // UploadAsync hashes the source and fully verifies each existing exact AWS version.
        }
        else exportedBytes = await transport.ExportAsync(
            "s3:ifm-development", snapshotTag, references, sourceRoot, CancellationToken.None);
        exportedBytes.Should().BeGreaterThan(0);

        var aws = LiveOptions();
        using var primaryS3 = new AmazonS3Client(Credentials(), RegionEndpoint.CACentral1);
        using var primaryKms = new AmazonKeyManagementServiceClient(Credentials(), RegionEndpoint.CACentral1);
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
            "backup/current-development-live",
            Sha256(string.Join('\n', schemaLines.Length == 0 ? references : schemaLines)),
            Sha256(string.Join('\n', references)),
            references.Select(ParseUnit).Where(static value => value is not null)
                .Select(static value => value!.Value.Keyspace).Distinct(StringComparer.Ordinal).Count(),
            references.Select(ParseUnit).Where(static value => value is not null).Distinct().Count(),
            1,
            references.Length,
            "6.2.2",
            "3.4.2");
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = DatabaseNativeBackupKind.ScyllaManagerSnapshot,
            NativeIdentity = "ifm-development:development-read-models",
            BaseRestorePointId = restorePoint,
            ChainDepth = 0
        };
        AwsResolvedCatalogRestorePoint? existingPublication = null;
        try { existingPublication = await catalog.ResolveAwsAsync(restorePoint, new DatabaseArtifactReplicaId("aws-primary"), CancellationToken.None); }
        catch (FileNotFoundException) { }
        if (existingPublication is not null)
        {
            existingPublication.Publication.OperationId.Should().Be(operation);
            existingPublication.Publication.ProtectionSetId.Should().Be(new DatabaseProtectionSetId("read-model-scylla"));
            existingPublication.Publication.ScyllaSnapshot.Should().Be(snapshot);
            existingPublication.Publication.ScyllaTopology.Should().Be(topology);
            existingPublication.RestorePoint.VerifiedBytes.Should().Be(
                Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length));
        }
        else
        {
        var publication = await publisher.PublishAsync(new DatabaseBackupPublicationRequest(
            operation,
            new DatabaseProtectionSetId("read-model-scylla"),
            DatabaseEngine.ScyllaDb,
            "scylla-snapshot-" + snapshot.NativeManifestSha256[..16],
            [new DatabaseLogicalDestination("aws-primary", true), new DatabaseLogicalDestination("aws-recovery", true)],
            Dependencies: [],
            BackupLineage: lineage,
            ScyllaTopology: topology,
            ScyllaSnapshot: snapshot), CancellationToken.None);
        publication.RestorePointId.Should().Be(restorePoint);
        }

        using var recoveryCredentials = new AssumeRoleAWSCredentials(Credentials(), aws.RecoveryReadRoleArn,
            $"current-scylla-development-{Environment.ProcessId}",
            new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recoveryS3 = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        using var recoveryVault = new AwsRecoveryVaultClient(recoveryS3);

        await StartRestoreTargetAsync();
        var restoreDurations = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        try
        {
            foreach (var drill in new[]
                     {
                         (Replica: "aws-primary", Bucket: "ifm-gate11-restore-primary"),
                         (Replica: "aws-recovery", Bucket: "ifm-gate11-restore-recovery")
                     })
            {
                // The recovery drill runs after the primary client is disposed. Any accidental
                // primary-vault dependency therefore fails instead of weakening recovery-only proof.
                if (drill.Replica == "aws-recovery") primaryS3.Dispose();
                var stagedRoot = Path.Combine(_root, drill.Replica);
                var sink = new DirectoryArtifactSink(stagedRoot);
                var restoreSource = new S3DatabaseRestoreSourceCapability(
                    primaryS3, catalog, sink, wal, aws, recoveryVault, signer, TimeProvider.System);
                DatabasePreparedRestoreSource? prepared = null;
                Exception? transient = null;
                var deadline = DateTimeOffset.UtcNow.AddHours(2);
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
                var restore = Stopwatch.StartNew();
                _ = await transport.EnsureAvailableAsync(
                    "s3:ifm-development", "s3:" + drill.Bucket, snapshotTag, staged, CancellationToken.None);
                await RestoreAndValidateAsync(drill.Bucket, snapshotTag, drill.Replica);
                restore.Stop();
                restoreDurations.Add(drill.Replica, restore.Elapsed);
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
        output.WriteLine("SourceNodeCount=1");
        var snapshotUtc = DateTimeOffset.ParseExact(
            snapshotTag, "'sm_'yyyyMMddHHmmss'UTC'", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        output.WriteLine("MeasuredRpo={0}", DateTimeOffset.UtcNow - snapshotUtc);
        output.WriteLine("PrimaryRestoreRto={0}", restoreDurations["aws-primary"]);
        output.WriteLine("RecoveryRestoreRto={0}", restoreDurations["aws-recovery"]);
        output.WriteLine("QualificationElapsed={0}", qualification.Elapsed);
    }

    async Task RestoreAndValidateAsync(string bucket, string snapshotTag, string replica)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var schemaTask = (await DockerAsync([
            "exec", "ifm-scylla-manager", "sctool", "restore", "--cluster", "ifm-restore-validation",
            "--location", "s3:" + bucket, "--snapshot-tag", snapshotTag, "--num-retries=0",
            "--restore-schema", "--name", "ifm-current-schema-" + suffix])).Trim();
        await AwaitManagerTaskAsync(schemaTask);
        var tableTask = (await DockerAsync([
            "exec", "ifm-scylla-manager", "sctool", "restore", "--cluster", "ifm-restore-validation",
            "--location", "s3:" + bucket, "--snapshot-tag", snapshotTag, "--num-retries=0",
            "--keyspace", "event_source_*,fund_*,market_data_*,option_pricer_*,reference_*,securities_*,trade_*",
            "--restore-tables", "--name", "ifm-current-tables-" + suffix])).Trim();
        await AwaitManagerTaskAsync(tableTask);
        var schema = await DockerAsync(["exec", "ifm-scylla-restore-validation", "cqlsh", "-e",
            "DESCRIBE TABLE market_data_test_db.futures_option_tick_data;"]);
        foreach (var column in new[] { "optionprice", "bidprice", "askprice", "underlyingprice" })
            schema.Should().Contain(column + " double");
        var query = await DockerAsync(["exec", "ifm-scylla-restore-validation", "cqlsh", "-e",
            "CONSISTENCY ALL; SELECT optionprice,bidprice,askprice,underlyingprice FROM market_data_test_db.futures_option_tick_data WHERE contractid='ES20180511P2525' AND valuedate='2025-02-20' AND tickid=123456;"]);
        query.Should().Contain("100.5").And.Contain("150").And.Contain("(1 rows)");
        var tables = await DockerAsync(["exec", "ifm-scylla-restore-validation", "cqlsh", "-e",
            "SELECT keyspace_name,table_name FROM system_schema.tables;"]);
        tables.Should().Contain("market_data_test_db").And.Contain("futures_option_tick_data_legacy_20261010");
        output.WriteLine("{0}SchemaTask={1}", replica, schemaTask);
        output.WriteLine("{0}TableTask={1}", replica, tableTask);
        output.WriteLine("{0}RepairedPriceRow={1}", replica, query);
    }

    static async Task AwaitManagerTaskAsync(string taskReference)
    {
        if (!taskReference.StartsWith("restore/", StringComparison.Ordinal))
            throw new InvalidDataException("Scylla Manager returned an invalid restore task reference.");
        var deadline = DateTimeOffset.UtcNow.AddHours(2);
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
        throw new TimeoutException("The Scylla Manager restore task exceeded two hours.");
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

    static string? ParseNodeId(string line)
    {
        const string marker = "/node/";
        var start = line.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = line.IndexOf('/', start);
        return end > start ? line[start..end] : null;
    }

    static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    static AWSCredentials Credentials() => new SharedSessionCredentials(
        Environment.GetEnvironmentVariable("IFM_CURRENT_AWS_SESSION_FILE")
        ?? throw new InvalidOperationException("Current-user refreshed session file is required."));

    sealed class SharedSessionCredentials(string path) : AWSCredentials
    {
        readonly object _gate = new();
        ImmutableCredentials? _last;
        DateTimeOffset _expires;
        public override ImmutableCredentials GetCredentials()
        {
            lock (_gate)
            {
                try { return Read(); }
                catch (IOException) when (_last is not null && _expires > DateTimeOffset.UtcNow.AddMinutes(1))
                { return _last; } // Atomic credential rotation can briefly race a new file open.
            }
        }
        ImmutableCredentials Read()
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.GetProperty("Expiration").GetDateTimeOffset() <= DateTimeOffset.UtcNow.AddMinutes(1))
                throw new InvalidOperationException("The approved upload-role refresh task has not renewed its session.");
            _expires = root.GetProperty("Expiration").GetDateTimeOffset();
            return _last = new ImmutableCredentials(root.GetProperty("AccessKeyId").GetString(),
                root.GetProperty("SecretAccessKey").GetString(), root.GetProperty("SessionToken").GetString());
        }
        public override Task<ImmutableCredentials> GetCredentialsAsync() => Task.FromResult(GetCredentials());
    }

    static AwsCloudDatabaseBackupOptions LiveOptions() => new()
    {
        Enabled = true,
        MaximumSignedDocumentBytes = 16 * 1024 * 1024,
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
        // Retain native artifacts and restore staging for recovery evidence and cutover preparation.
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
            if (File.Exists(path))
            {
                await using var previous = File.OpenRead(path);
                previous.Length.Should().Be(expectedLength);
                Convert.ToHexString(await SHA256.HashDataAsync(previous, cancellationToken)).Should().BeEquivalentTo(expectedSha256);
                return;
            }
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

