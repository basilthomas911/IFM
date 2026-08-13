using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class ScyllaNativeDockerIntegrationTests : IAsyncLifetime
{
    const string Image = "scylladb/scylla:6.2.2";
    readonly string _id = Guid.NewGuid().ToString("N")[..12];
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate7-native", Guid.NewGuid().ToString("N"));
    string SourceContainer => $"ifm-gate7-source-{_id}";
    string TargetContainer => $"ifm-gate7-target-{_id}";
    long _originalAioMaximum;

    public async Task InitializeAsync()
    {
        _originalAioMaximum = long.Parse(await DockerAsync([
            "run", "--rm", "--privileged", "--entrypoint", "sysctl", Image, "-n", "fs.aio-max-nr"]));
        if (_originalAioMaximum < 196_608)
            await SetAioMaximumAsync(196_608);
        await StartScyllaAsync(SourceContainer);
        await WaitForScyllaAsync(SourceContainer);
        await CqlAsync(SourceContainer,
            "CREATE KEYSPACE gate7_keyspace WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1}; " +
            "CREATE TABLE gate7_keyspace.probe(id int PRIMARY KEY, payload text); " +
            "INSERT INTO gate7_keyspace.probe(id, payload) VALUES (1, 'native-scylla-restore-ok');");
    }

    [Fact]
    [Trait("Category", "Gate7NativeIntegration")]
    [Trait("Category", "Gate10NativeIntegration")]
    public async Task Native_snapshot_SSTables_restore_to_fresh_disposable_node_after_host_restart()
    {
        var options = Options();
        var native = new DockerScyllaAdministrationClient(SourceContainer, TargetContainer);
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var firstHost = new ScyllaBackupCapability(options, native);
        await firstHost.ValidateAsync(CancellationToken.None);
        var boundary = await firstHost.CreateBackupAsync(
            new ScyllaBackupRequest(operation, new DatabaseProtectionSetId("read-model-scylla")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        var restartedHost = new ScyllaBackupCapability(options, native);
        await restartedHost.ValidateAsync(CancellationToken.None);
        var recovered = await restartedHost.CreateBackupAsync(
            new ScyllaBackupRequest(operation, new DatabaseProtectionSetId("read-model-scylla")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verified = await restartedHost.VerifyAsync(
            new ScyllaVerificationRequest(operation, recovered.SafeBoundaryReference), CancellationToken.None);
        var restored = await restartedHost.RestoreToFreshTargetAsync(
            new ScyllaRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(operation.Format()),
                new DatabaseFreshTargetDescriptor("docker-validation", "gate7-native")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        recovered.SafeBoundaryReference.Should().Be(boundary.SafeBoundaryReference);
        boundary.Snapshot!.SnapshotTag.Should().StartWith("ifm_gate7_");
        boundary.Snapshot.ScyllaVersion.Should().NotBeNullOrWhiteSpace();
        boundary.Snapshot.ArtifactCount.Should().BeGreaterThan(0);
        boundary.Topology!.LiveNodeCount.Should().Be(1);
        boundary.Topology.TokenRangeCount.Should().BeGreaterThan(0);
        verified.Succeeded.Should().BeTrue();
        restored.Succeeded.Should().BeTrue();
        restored.SourceClusterName.Should().Be("gate7-source-disposable");
        restored.RestoredClusterName.Should().Be("gate7-target-disposable");
        native.SyntheticPayload.Should().Be("native-scylla-restore-ok");
        native.CaptureCount.Should().Be(1);
    }

    ScyllaBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "manager-tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore"),
        ManagerApiUrl = "http://127.0.0.1:5080/api/v1",
        ProtectionSets = new Dictionary<string, ScyllaProtectionSetOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["read-model-scylla"] = new()
            {
                ManagerCluster = "gate7-source-disposable",
                BackupLocation = "localstorage:gate7-native",
                Keyspaces = ["gate7_keyspace"],
                RequiredLiveNodes = 1
            }
        },
        FreshTargetProfiles = new Dictionary<string, ScyllaFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["docker-validation"] = new()
            {
                ManagerCluster = "gate7-target-disposable",
                AllowedLogicalTargets = ["gate7-native"],
                RequiredLiveNodes = 1
            }
        },
        OperationTimeout = TimeSpan.FromMinutes(5),
        PollInterval = TimeSpan.FromMilliseconds(100),
        RequirePersistentBackupRoot = false
    };

    public async Task DisposeAsync()
    {
        await RemoveContainerAsync(TargetContainer);
        await RemoveContainerAsync(SourceContainer);
        if (_originalAioMaximum > 0)
        {
            try { await SetAioMaximumAsync(_originalAioMaximum); }
            catch { }
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    static Task SetAioMaximumAsync(long value) => DockerAsync([
        "run", "--rm", "--privileged", "--entrypoint", "sysctl", Image,
        "-w", $"fs.aio-max-nr={value}"]);

    static Task StartScyllaAsync(string name) => DockerAsync([
        "run", "--detach", "--name", name, "--memory", "2g", Image,
        "--smp", "1", "--overprovisioned", "1", "--developer-mode", "1",
        "--broadcast-rpc-address", "127.0.0.1"]);

    static async Task WaitForScyllaAsync(string container)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await CqlAsync(container, "SELECT now() FROM system.local;");
                return;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(500);
            }
        }
        throw new TimeoutException("The disposable Scylla node did not become ready.");
    }

    static Task<string> CqlAsync(string container, string cql)
        => DockerAsync(["exec", container, "cqlsh", "-e", cql]);

    static async Task RemoveContainerAsync(string name)
    {
        try { await DockerAsync(["rm", "--force", name], allowFailure: true); }
        catch { }
    }

    static async Task<string> DockerAsync(IReadOnlyList<string> arguments, bool allowFailure = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException($"The disposable Docker Scylla operation failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }

    sealed class DockerScyllaAdministrationClient(string sourceContainer, string targetContainer)
        : IScyllaAdministrationClient
    {
        public int CaptureCount { get; private set; }
        public string SyntheticPayload { get; private set; } = string.Empty;

        public async ValueTask ValidateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await RestAsync(sourceContainer, "GET", "/storage_service/scylla_release_version");
        }

        public async ValueTask<ScyllaNativeCapture> CaptureAsync(
            DatabaseRecoveryOperationId operationId,
            ScyllaProtectionSetOptions protectionSet,
            string nativeDirectory,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
        {
            CaptureCount++;
            var started = Stopwatch.GetTimestamp();
            var tag = "ifm_gate7_" + operationId.Format();
            await RestAsync(sourceContainer, "POST",
                $"/storage_service/snapshots?tag={tag}&kn=gate7_keyspace&cf=probe");
            var snapshotDirectory = (await DockerAsync([
                "exec", sourceContainer, "find", "/var/lib/scylla/data/gate7_keyspace",
                "-type", "d", "-path", $"*/snapshots/{tag}"])).Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Single();
            await DockerAsync(["cp", $"{sourceContainer}:{snapshotDirectory}/.", nativeDirectory]);
            var schema = await File.ReadAllTextAsync(Path.Combine(nativeDirectory, "schema.cql"), cancellationToken);
            var artifacts = Directory.EnumerateFiles(nativeDirectory).Select(Path.GetFileName)
                .Where(static value => value is not null).Select(static value => value!)
                .OrderBy(static value => value, StringComparer.Ordinal).ToArray();
            var manifest = await ScyllaEvidenceSerializer.DirectoryManifestSha256Async(nativeDirectory, cancellationToken);
            var topology = await TopologyAsync(sourceContainer, "gate7-source-disposable");
            var version = JsonSerializer.Deserialize<string>(await RestAsync(
                sourceContainer, "GET", "/storage_service/scylla_release_version"))!;
            return new ScyllaNativeCapture(
                "snapshot/" + tag, tag, topology, Sha256(schema), manifest, artifacts, 1, 1,
                Directory.EnumerateFiles(nativeDirectory).Sum(static file => new FileInfo(file).Length),
                version, "node-rest-native-test", Stopwatch.GetElapsedTime(started));
        }

        public async ValueTask<ScyllaNativeVerification> VerifyAsync(
            ScyllaProtectionSetOptions protectionSet,
            ScyllaNativeCapture capture,
            string nativeDirectory,
            CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            var digest = await ScyllaEvidenceSerializer.DirectoryManifestSha256Async(nativeDirectory, cancellationToken);
            var snapshots = await RestAsync(sourceContainer, "GET", "/storage_service/snapshots");
            return new ScyllaNativeVerification(
                string.Equals(digest, capture.NativeManifestSha256, StringComparison.Ordinal)
                    && snapshots.Contains(capture.SnapshotTag, StringComparison.Ordinal),
                await TopologyAsync(sourceContainer, "gate7-source-disposable"),
                digest,
                Directory.EnumerateFiles(nativeDirectory).Sum(static file => new FileInfo(file).Length),
                Stopwatch.GetElapsedTime(started));
        }

        public async ValueTask<ScyllaNativeRestoreValidation> RestoreAsync(
            DatabaseRecoveryOperationId operationId,
            ScyllaProtectionSetOptions source,
            ScyllaFreshTargetProfileOptions target,
            ScyllaNativeCapture capture,
            string sourceNativeDirectory,
            string restoreWorkspace,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            await RemoveContainerAsync(sourceContainer);
            await StartScyllaAsync(targetContainer);
            await WaitForScyllaAsync(targetContainer);
            await CqlAsync(targetContainer,
                "CREATE KEYSPACE gate7_keyspace WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};");
            var schema = await File.ReadAllTextAsync(Path.Combine(sourceNativeDirectory, "schema.cql"), cancellationToken);
            try { await CqlAsync(targetContainer, schema.Trim().TrimEnd(';')); }
            catch (InvalidOperationException error)
            {
                throw new InvalidDataException("The snapshot-generated Scylla schema could not be applied: " + schema, error);
            }
            var tableDirectory = (await DockerAsync([
                "exec", targetContainer, "find", "/var/lib/scylla/data/gate7_keyspace",
                "-maxdepth", "1", "-type", "d", "-name", "probe-*"])).Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Single();
            var upload = tableDirectory + "/upload";
            await DockerAsync(["exec", targetContainer, "mkdir", "-p", upload]);
            foreach (var file in Directory.EnumerateFiles(sourceNativeDirectory).Where(static file =>
                         !string.Equals(Path.GetFileName(file), "schema.cql", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(Path.GetFileName(file), "manifest.json", StringComparison.OrdinalIgnoreCase)))
                await DockerAsync(["cp", file, $"{targetContainer}:{upload}/{Path.GetFileName(file)}"]);
            await DockerAsync(["exec", targetContainer, "nodetool", "refresh", "gate7_keyspace", "probe"]);
            var query = await CqlAsync(targetContainer, "SELECT payload FROM gate7_keyspace.probe WHERE id = 1;");
            SyntheticPayload = query.Contains("native-scylla-restore-ok", StringComparison.Ordinal)
                ? "native-scylla-restore-ok"
                : string.Empty;
            if (SyntheticPayload.Length == 0)
                throw new InvalidDataException("The fresh Scylla target did not return the synthetic row. Native query output: " + query);
            var bytes = Directory.EnumerateFiles(sourceNativeDirectory, "*.db").Sum(static file => new FileInfo(file).Length);
            return new ScyllaNativeRestoreValidation(
                SyntheticPayload.Length > 0,
                "gate7-target-disposable",
                await TopologyAsync(targetContainer, "gate7-target-disposable"),
                1,
                bytes,
                Stopwatch.GetElapsedTime(started));
        }

        static async ValueTask<ScyllaTopologyEvidence> TopologyAsync(string container, string clusterName)
        {
            using var tokens = JsonDocument.Parse(await RestAsync(container, "GET", "/storage_service/tokens"));
            var schema = JsonSerializer.Deserialize<string>(await RestAsync(
                container, "GET", "/storage_service/schema_version"));
            return new ScyllaTopologyEvidence(clusterName, 1, tokens.RootElement.GetArrayLength(), !string.IsNullOrWhiteSpace(schema));
        }

        static Task<string> RestAsync(string container, string method, string path)
            => DockerAsync(["exec", container, "curl", "--silent", "--show-error", "--fail",
                "--request", method, "http://127.0.0.1:10000" + path]);

        static string Sha256(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
