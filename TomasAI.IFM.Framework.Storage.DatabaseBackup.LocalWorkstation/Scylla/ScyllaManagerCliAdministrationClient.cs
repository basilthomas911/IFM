using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

internal sealed partial class ScyllaManagerCliAdministrationClient : IScyllaAdministrationClient
{
    readonly ScyllaBackupOptions _options;
    readonly IScyllaManagerProcessRunner _runner;
    string _managerVersion = string.Empty;
    readonly ConcurrentDictionary<string, string> _scyllaVersions = new(StringComparer.OrdinalIgnoreCase);

    public ScyllaManagerCliAdministrationClient(ScyllaBackupOptions options)
        : this(options, new ScyllaManagerProcessRunner(options)) { }

    internal ScyllaManagerCliAdministrationClient(
        ScyllaBackupOptions options,
        IScyllaManagerProcessRunner runner)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async ValueTask ValidateAsync(CancellationToken cancellationToken)
    {
        _options.Validate();
        var result = await RunAsync(ScyllaManagerOperation.Version, ["version"], TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);
        var match = VersionPattern().Match(result.StandardOutput + " " + result.StandardError);
        if (!match.Success)
            throw new InvalidOperationException("The Scylla Manager client returned an unrecognized version.");
        var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (major < _options.MinimumManagerMajorVersion || major > _options.MaximumManagerMajorVersion)
            throw new InvalidOperationException("The Scylla Manager client version is outside the configured compatibility range.");
        _managerVersion = match.Value.Trim();
        foreach (var protectionSet in _options.ProtectionSets.Values)
            _ = await ReadTopologyAsync(protectionSet.ManagerCluster, protectionSet.RequiredLiveNodes, cancellationToken)
                .ConfigureAwait(false);
    }

    public async ValueTask<ScyllaNativeCapture> CaptureAsync(
        Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId operationId,
        ScyllaProtectionSetOptions protectionSet,
        string nativeDirectory,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(nativeDirectory);
        var topology = await ReadTopologyAsync(
            protectionSet.ManagerCluster, protectionSet.RequiredLiveNodes, cancellationToken).ConfigureAwait(false);
        progress.Report(new DatabaseNativeProgress(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Capturing, 5));
        var taskName = "ifm-" + operationId.Format();
        var arguments = new List<string>
        {
            "backup", "--cluster", protectionSet.ManagerCluster,
            "--location", protectionSet.BackupLocation,
            "--keyspace", string.Join(',', protectionSet.Keyspaces),
            $"--retention={protectionSet.ManagerRetentionCount}", "--num-retries=0", "--name", taskName
        };
        var started = Stopwatch.GetTimestamp();
        var created = await RunAsync(
            ScyllaManagerOperation.Backup, arguments, _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var taskReference = TaskPattern().Match(created.StandardOutput).Value;
        if (string.IsNullOrEmpty(taskReference)) taskReference = "backup/" + taskName;
        await AwaitTaskAsync(protectionSet.ManagerCluster, taskReference, progress, cancellationToken).ConfigureAwait(false);

        var list = await RunAsync(ScyllaManagerOperation.BackupList,
            ["backup", "list", "--cluster", protectionSet.ManagerCluster, "--location", protectionSet.BackupLocation],
            _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var tags = SnapshotTagPattern().Matches(list.StandardOutput).Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (tags.Length == 0)
            throw new InvalidDataException("Scylla Manager did not publish a snapshot tag for the completed backup.");
        var snapshotTag = tags[^1];
        var manifest = await ReadManifestAsync(protectionSet, snapshotTag, cancellationToken).ConfigureAwait(false);
        if (manifest.NodeCount != topology.LiveNodeCount)
            throw new InvalidDataException("The completed Scylla Manager snapshot does not cover every live topology node.");
        await File.WriteAllTextAsync(Path.Combine(nativeDirectory, "manager-artifacts.txt"),
            string.Join(Environment.NewLine, manifest.Artifacts), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(nativeDirectory, "schema.sha256"), manifest.SchemaSha256, cancellationToken)
            .ConfigureAwait(false);
        progress.Report(new DatabaseNativeProgress(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Capturing, 100));
        return new ScyllaNativeCapture(
            taskReference, snapshotTag, topology, manifest.SchemaSha256, manifest.ManifestSha256,
            manifest.Artifacts, manifest.KeyspaceCount, manifest.TableCount, 0,
            _scyllaVersions[protectionSet.ManagerCluster], _managerVersion, Stopwatch.GetElapsedTime(started));
    }

    public async ValueTask<ScyllaNativeVerification> VerifyAsync(
        ScyllaProtectionSetOptions protectionSet,
        ScyllaNativeCapture capture,
        string nativeDirectory,
        CancellationToken cancellationToken)
    {
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();
        var manifest = await ReadManifestAsync(protectionSet, capture.SnapshotTag, cancellationToken).ConfigureAwait(false);
        var localArtifacts = await File.ReadAllTextAsync(
            Path.Combine(nativeDirectory, "manager-artifacts.txt"), cancellationToken).ConfigureAwait(false);
        var localSchema = await File.ReadAllTextAsync(
            Path.Combine(nativeDirectory, "schema.sha256"), cancellationToken).ConfigureAwait(false);
        var succeeded = string.Equals(manifest.ManifestSha256, capture.NativeManifestSha256, StringComparison.Ordinal)
            && string.Equals(manifest.SchemaSha256, capture.SchemaSha256, StringComparison.Ordinal)
            && string.Equals(Sha256(localArtifacts.Replace("\r\n", "\n", StringComparison.Ordinal)), capture.NativeManifestSha256, StringComparison.Ordinal)
            && string.Equals(localSchema, capture.SchemaSha256, StringComparison.Ordinal)
            && capture.Topology.SchemaAgreement
            && manifest.NodeCount == capture.Topology.LiveNodeCount;
        return new ScyllaNativeVerification(succeeded, capture.Topology, manifest.ManifestSha256, 0, Stopwatch.GetElapsedTime(started));
    }

    public async ValueTask<ScyllaNativeRestoreValidation> RestoreAsync(
        Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId operationId,
        ScyllaProtectionSetOptions source,
        ScyllaFreshTargetProfileOptions target,
        ScyllaNativeCapture capture,
        string sourceNativeDirectory,
        string restoreWorkspace,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(restoreWorkspace);
        var started = Stopwatch.GetTimestamp();
        var common = new[]
        {
            "restore", "--cluster", target.ManagerCluster, "--location", source.BackupLocation,
            "--snapshot-tag", capture.SnapshotTag, "--num-retries=0"
        };
        progress.Report(new DatabaseNativeProgress(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Transferring, 5));
        var schema = await RunAsync(ScyllaManagerOperation.RestoreSchema,
            [.. common, "--restore-schema", "--name", "ifm-schema-" + operationId.Format()],
            _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var schemaTask = TaskPattern().Match(schema.StandardOutput).Value;
        if (string.IsNullOrEmpty(schemaTask)) schemaTask = "restore/ifm-schema-" + operationId.Format();
        await AwaitTaskAsync(target.ManagerCluster, schemaTask, progress, cancellationToken).ConfigureAwait(false);
        progress.Report(new DatabaseNativeProgress(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Transferring, 45));
        var tables = await RunAsync(ScyllaManagerOperation.RestoreTables,
            [.. common, "--keyspace", string.Join(',', source.Keyspaces),
                "--restore-tables", "--name", "ifm-tables-" + operationId.Format()],
            _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var tablesTask = TaskPattern().Match(tables.StandardOutput).Value;
        if (string.IsNullOrEmpty(tablesTask)) tablesTask = "restore/ifm-tables-" + operationId.Format();
        await AwaitTaskAsync(target.ManagerCluster, tablesTask, progress, cancellationToken).ConfigureAwait(false);
        var topology = await ReadTopologyAsync(target.ManagerCluster, target.RequiredLiveNodes, cancellationToken)
            .ConfigureAwait(false);
        progress.Report(new DatabaseNativeProgress(Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryPhase.Validating, 100));
        var revision = ValidationRevision(capture.SchemaSha256, capture.NativeManifestSha256);
        return new ScyllaNativeRestoreValidation(
            topology.SchemaAgreement, topology.ClusterName, topology, revision, 0, Stopwatch.GetElapsedTime(started));
    }

    async ValueTask EnsureValidatedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_managerVersion)) await ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    async ValueTask<ScyllaTopologyEvidence> ReadTopologyAsync(
        string cluster,
        int requiredLiveNodes,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(ScyllaManagerOperation.Status,
            ["status", "--cluster", cluster], _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var text = result.StandardOutput;
        _scyllaVersions[cluster] = ExtractScyllaVersion(text);
        var liveNodes = UpNodePattern().Matches(text).Count;
        if (liveNodes < requiredLiveNodes)
            throw new InvalidOperationException("The Scylla cluster does not have the required live-node coverage.");
        var tokenRanges = TokenRangePattern().Matches(text).Count;
        var schemaAgreement = !text.Contains("schema disagreement", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("CQL DOWN", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("CQL ERROR", StringComparison.OrdinalIgnoreCase);
        if (!schemaAgreement)
            throw new InvalidOperationException("The Scylla cluster is not in schema agreement.");
        return new ScyllaTopologyEvidence(cluster, liveNodes, Math.Max(liveNodes, tokenRanges), true);
    }

    static string ExtractScyllaVersion(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var headerIndex = Array.FindIndex(lines, static line =>
            line.Contains("Scylla", StringComparison.OrdinalIgnoreCase)
            && line.Contains("Host ID", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
            throw new InvalidDataException("Scylla Manager status did not contain native version evidence.");
        var headers = SplitTableRow(lines[headerIndex]);
        var versionIndex = Array.FindIndex(headers, static cell =>
            string.Equals(cell, "Scylla", StringComparison.OrdinalIgnoreCase));
        for (var index = headerIndex + 1; index < lines.Length; index++)
        {
            var cells = SplitTableRow(lines[index]);
            if (versionIndex >= 0 && cells.Length > versionIndex && SemanticVersionPattern().IsMatch(cells[versionIndex]))
                return cells[versionIndex];
        }
        throw new InvalidDataException("Scylla Manager status did not contain a recognized Scylla version.");
    }

    static string[] SplitTableRow(string value)
        => value.Trim().Trim('│', '|').Split(['│', '|'], StringSplitOptions.TrimEntries);

    async ValueTask AwaitTaskAsync(
        string cluster,
        string taskReference,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _options.OperationTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await RunAsync(ScyllaManagerOperation.Tasks,
                ["tasks", "--cluster", cluster], _options.OperationTimeout, cancellationToken)
                .ConfigureAwait(false);
            var taskRow = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => SplitTableRow(line))
                .FirstOrDefault(cells => cells.Length > 0
                    && string.Equals(cells[0], taskReference, StringComparison.OrdinalIgnoreCase));
            if (taskRow?.Contains("DONE", StringComparer.OrdinalIgnoreCase) == true)
                return;
            if (taskRow?.Any(static cell => cell.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                    || cell.Equals("ABORTED", StringComparison.OrdinalIgnoreCase)
                    || cell.Equals("STOPPED", StringComparison.OrdinalIgnoreCase)) == true)
                throw new InvalidOperationException("The Scylla Manager task did not complete successfully.");
            await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The Scylla Manager task exceeded its configured timeout.");
    }

    async ValueTask<(string[] Artifacts, string SchemaSha256, string ManifestSha256, int KeyspaceCount, int TableCount, int NodeCount)>
        ReadManifestAsync(
            ScyllaProtectionSetOptions protectionSet,
            string snapshotTag,
            CancellationToken cancellationToken)
    {
        var result = await RunAsync(ScyllaManagerOperation.BackupFiles,
            ["backup", "files", "--cluster", protectionSet.ManagerCluster,
                "--location", protectionSet.BackupLocation, "--snapshot-tag", snapshotTag,
                "--delimiter=|", "--with-version"],
            _options.OperationTimeout, cancellationToken).ConfigureAwait(false);
        var artifacts = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.Contains('|') && !line.Contains("REMOTE", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static line => line, StringComparer.Ordinal).ToArray();
        if (artifacts.Length == 0)
            throw new InvalidDataException("Scylla Manager returned an empty native backup manifest.");
        var schemaLines = artifacts.Where(static line => line.Contains("schema", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (schemaLines.Length == 0) schemaLines = artifacts;
        var tables = artifacts.Select(ParseUnit).Where(static value => value is not null).Select(static value => value!.Value)
            .Distinct().ToArray();
        var nodes = artifacts.Select(ParseNodeId).Where(static value => value is not null)
            .Distinct(StringComparer.Ordinal).Count();
        if (nodes == 0)
            throw new InvalidDataException("Scylla Manager returned no node identity in the native backup manifest.");
        return (artifacts, Sha256(string.Join('\n', schemaLines)), Sha256(string.Join('\n', artifacts)),
            tables.Select(static value => value.Keyspace).Distinct(StringComparer.Ordinal).Count(), tables.Length, nodes);
    }

    ValueTask<ScyllaManagerProcessResult> RunAsync(
        ScyllaManagerOperation operation,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => _runner.RunAsync(new ScyllaManagerInvocation(operation, arguments, timeout), cancellationToken);

    static (string Keyspace, string Table)? ParseUnit(string line)
    {
        var values = line.Split('|', StringSplitOptions.TrimEntries);
        if (values.Length < 3 || string.IsNullOrWhiteSpace(values[^2]) || string.IsNullOrWhiteSpace(values[^1])) return null;
        return (values[^2], values[^1]);
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

    static long ValidationRevision(string schemaSha256, string manifestSha256)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(schemaSha256 + manifestSha256));
        return BitConverter.ToInt64(digest, 0) & long.MaxValue;
    }

    [GeneratedRegex(@"\b(\d+)\.\d+(?:\.\d+)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\b(?:backup|restore)/[A-Za-z0-9-]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();

    [GeneratedRegex(@"\bsm_\d{14}UTC\b", RegexOptions.CultureInvariant)]
    private static partial Regex SnapshotTagPattern();

    [GeneratedRegex(@"(?:^|[│|+\s])UN(?:[│|+\s]|$)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex UpNodePattern();

    [GeneratedRegex(@"\b[0-9a-f]{8}-[0-9a-f-]{27}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRangePattern();

    [GeneratedRegex(@"^\d+\.\d+(?:\.\d+)?(?:[-+][A-Za-z0-9._-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();
}
