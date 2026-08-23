using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

internal sealed record PostgreSqlFreshTargetValidation(string SystemIdentifier, long ValidationRevision);
internal sealed record PostgreSqlSourceMetadata(string SystemIdentifier, int MajorVersion, bool WalSummariesEnabled);

internal interface IPostgreSqlSourceMetadataReader
{
    ValueTask<PostgreSqlSourceMetadata> ReadAsync(string connectionString, CancellationToken cancellationToken);
}

internal sealed class PostgreSqlSourceMetadataReader : IPostgreSqlSourceMetadataReader
{
    public async ValueTask<PostgreSqlSourceMetadata> ReadAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT system_identifier::text,
                   current_setting('server_version_num')::integer,
                   COALESCE(current_setting('summarize_wal', true), 'off')
            FROM pg_control_system()
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The PostgreSQL source returned no native identity.");
        return new PostgreSqlSourceMetadata(
            reader.GetString(0),
            reader.GetInt32(1) / 10000,
            string.Equals(reader.GetString(2), "on", StringComparison.OrdinalIgnoreCase));
    }
}

internal interface IPostgreSqlFreshTargetValidator
{
    ValueTask<PostgreSqlFreshTargetValidation> ValidateAsync(
        PostgreSqlFreshTargetProfileOptions profile,
        IReadOnlyDictionary<string, string?> sourceEnvironment,
        string expectedSystemIdentifier,
        CancellationToken cancellationToken);
}

internal sealed class PostgreSqlFreshTargetValidator : IPostgreSqlFreshTargetValidator
{
    public async ValueTask<PostgreSqlFreshTargetValidation> ValidateAsync(
        PostgreSqlFreshTargetProfileOptions profile,
        IReadOnlyDictionary<string, string?> sourceEnvironment,
        string expectedSystemIdentifier,
        CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = profile.Host,
            Port = profile.Port,
            Database = profile.Database,
            Username = Required(sourceEnvironment, "PGUSER"),
            Password = Required(sourceEnvironment, "PGPASSWORD"),
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 5,
            SslMode = SslMode.Disable
        };
        var deadline = DateTimeOffset.UtcNow + profile.StartupTimeout;
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = new NpgsqlConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT system_identifier::text, current_setting('server_version_num')::bigint FROM pg_control_system()";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new InvalidOperationException("The PostgreSQL fresh target returned no native identity.");
                var systemIdentifier = reader.GetString(0);
                var revision = reader.GetInt64(1);
                if (!string.Equals(systemIdentifier, expectedSystemIdentifier, StringComparison.Ordinal))
                    throw new InvalidOperationException("The PostgreSQL fresh target system identifier does not match the backup evidence.");
                return new PostgreSqlFreshTargetValidation(systemIdentifier, revision);
            }
            catch (Exception error) when (error is NpgsqlException or TimeoutException)
            {
                lastError = error;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("The PostgreSQL fresh target did not become ready for validation.", lastError);
    }

    static string Required(IReadOnlyDictionary<string, string?> values, string name)
        => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException("The PostgreSQL credential reference is incomplete.");
}

public sealed partial class PostgreSqlBackupCapability : IPostgreSqlBackupCapability, IDatabaseNativeCapabilityValidation
{
    readonly PostgreSqlBackupOptions _options;
    readonly PostgreSqlBackupPathResolver _paths;
    readonly IPostgreSqlNativeProcessRunner _runner;
    readonly IPostgreSqlFreshTargetValidator _validator;
    readonly IPostgreSqlSourceMetadataReader _sourceMetadata;
    int _nativeMajorVersion;

    public PostgreSqlBackupCapability(PostgreSqlBackupOptions options)
        : this(options, new PostgreSqlNativeProcessRunner(options), new PostgreSqlFreshTargetValidator()) { }

    internal PostgreSqlBackupCapability(
        PostgreSqlBackupOptions options,
        IPostgreSqlNativeProcessRunner runner,
        IPostgreSqlFreshTargetValidator validator,
        IPostgreSqlSourceMetadataReader? sourceMetadata = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _paths = new PostgreSqlBackupPathResolver(options);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _sourceMetadata = sourceMetadata ?? new PostgreSqlSourceMetadataReader();
    }

    public async ValueTask ValidateAsync(CancellationToken cancellationToken)
    {
        _options.Validate();
        _ = ConnectionEnvironment();
        Directory.CreateDirectory(_paths.BackupRoot);
        Directory.CreateDirectory(_paths.RestoreRoot);
        var versions = new[]
        {
            await ReadVersionAsync(PostgreSqlNativeTool.BaseBackup, cancellationToken).ConfigureAwait(false),
            await ReadVersionAsync(PostgreSqlNativeTool.VerifyBackup, cancellationToken).ConfigureAwait(false),
            await ReadVersionAsync(PostgreSqlNativeTool.Control, cancellationToken).ConfigureAwait(false),
            await ReadVersionAsync(PostgreSqlNativeTool.ControlData, cancellationToken).ConfigureAwait(false)
        };
        if (versions.Distinct().Count() != 1)
            throw new InvalidOperationException("PostgreSQL backup, verification, and control tools must have the same major version.");
        if (versions[0] < _options.MinimumMajorVersion || versions[0] > _options.MaximumMajorVersion)
            throw new InvalidOperationException("The PostgreSQL native tool version is outside the configured compatibility range.");
        Volatile.Write(ref _nativeMajorVersion, versions[0]);
    }

    public async ValueTask<PostgreSqlBackupBoundary> CreateBaseBackupAsync(
        PostgreSqlBackupRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(progress);
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.BackupFinal(request.OperationId);
        if (File.Exists(PostgreSqlBackupEvidenceSerializer.BackupEvidencePath(final)))
            return Boundary(await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(final, cancellationToken).ConfigureAwait(false));
        var lineage = await ResolveLineageAsync(request, cancellationToken).ConfigureAwait(false);

        var staging = _paths.BackupStaging(request.OperationId);
        var data = Path.Combine(staging, "data");
        if (File.Exists(Path.Combine(data, "backup_manifest")))
            return await BoundaryFromManifestAsync(data, TimeSpan.Zero, lineage, cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
            throw new InvalidOperationException("An incomplete PostgreSQL native capture requires explicit reconciliation.");

        Directory.CreateDirectory(staging);
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Capturing, 1));
        var arguments = new List<string>
        {
            "--pgdata", data, "--format=plain", "--wal-method=stream", "--checkpoint=fast",
            "--progress", "--manifest-checksums=SHA256", "--no-password"
        };
        if (lineage.NativeKind == DatabaseNativeBackupKind.PostgreSqlIncremental)
        {
            var parent = lineage.ParentRestorePointId
                ?? throw new InvalidOperationException("PostgreSQL incremental lineage has no parent.");
            var parentManifest = Path.Combine(_paths.RestorePoint(parent), "data", "backup_manifest");
            if (!File.Exists(parentManifest))
                throw new FileNotFoundException("The PostgreSQL incremental parent manifest is unavailable.", parentManifest);
            arguments.Add("--incremental=" + parentManifest);
        }
        var result = await _runner.RunAsync(new PostgreSqlNativeInvocation(
            PostgreSqlNativeTool.BaseBackup,
            arguments,
            ConnectionEnvironment(),
            _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Capturing, 100));
        return await BoundaryFromManifestAsync(data, result.Elapsed, lineage, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PostgreSqlVerificationResult> VerifyAsync(
        PostgreSqlVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.SafeBoundaryReference))
            throw new ArgumentException("PostgreSQL verification requires an operation and safe boundary reference.", nameof(request));
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.BackupFinal(request.OperationId);
        if (File.Exists(PostgreSqlBackupEvidenceSerializer.BackupEvidencePath(final)))
        {
            var existing = await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(final, cancellationToken).ConfigureAwait(false);
            EnsureBoundary(existing.SafeBoundaryReference, request.SafeBoundaryReference);
            return Verification(existing);
        }

        var staging = _paths.BackupStaging(request.OperationId);
        var evidencePath = PostgreSqlBackupEvidenceSerializer.BackupEvidencePath(staging);
        if (File.Exists(evidencePath))
        {
            var recovered = await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(staging, cancellationToken).ConfigureAwait(false);
            EnsureBoundary(recovered.SafeBoundaryReference, request.SafeBoundaryReference);
            Directory.Move(staging, final);
            return Verification(recovered);
        }

        var data = Path.Combine(staging, "data");
        var manifest = await ReadManifestAsync(data, cancellationToken).ConfigureAwait(false);
        var lineage = (request.BackupLineage ?? new DatabaseBackupLineage()).NormalizeLegacyFull(DatabaseEngine.PostgreSql);
        var expectedBoundary = SafeBoundary(manifest.ManifestSha256, lineage);
        EnsureBoundary(expectedBoundary, request.SafeBoundaryReference);
        var started = Stopwatch.GetTimestamp();
        await _runner.RunAsync(new PostgreSqlNativeInvocation(
            PostgreSqlNativeTool.VerifyBackup,
            ["--exit-on-error", data],
            EmptyEnvironment,
            _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var statistics = Statistics(DatabaseRecoveryPhase.Verifying, manifest.SourceBytes, manifest.FileCount, elapsed);
        var evidence = new PostgreSqlBackupEvidence(
            request.OperationId,
            expectedBoundary,
            manifest.ManifestSha256,
            manifest.SystemIdentifier,
            manifest.WalContinuity,
            statistics,
            Volatile.Read(ref _nativeMajorVersion),
            DateTimeOffset.UtcNow,
            lineage with { NativeIdentity = manifest.SystemIdentifier });
        await PostgreSqlBackupEvidenceSerializer.WriteBackupAsync(staging, evidence, cancellationToken).ConfigureAwait(false);
        Directory.Move(staging, final);
        return Verification(evidence);
    }

    public async ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
        PostgreSqlRestoreRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken)
    {
        ValidateRestoreRequest(request);
        ArgumentNullException.ThrowIfNull(progress);
        await EnsureValidatedAsync(cancellationToken).ConfigureAwait(false);
        var final = _paths.RestoreFinal(request);
        if (File.Exists(PostgreSqlBackupEvidenceSerializer.RestoreEvidencePath(final)))
            return RestoreResult(await PostgreSqlBackupEvidenceSerializer.ReadRestoreAsync(final, cancellationToken).ConfigureAwait(false));

        var source = _paths.RestorePoint(request.RestorePointId);
        var sourceEvidence = await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(source, cancellationToken).ConfigureAwait(false);
        var sourceData = Path.Combine(source, "data");
        var dependencyData = new List<string>();
        foreach (var dependency in request.DependencyChain ?? [])
        {
            var dependencyRoot = _paths.RestorePoint(dependency);
            _ = await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(
                dependencyRoot, cancellationToken).ConfigureAwait(false);
            dependencyData.Add(Path.Combine(dependencyRoot, "data"));
        }
        foreach (var backupData in dependencyData.Append(sourceData))
            await _runner.RunAsync(new PostgreSqlNativeInvocation(
                PostgreSqlNativeTool.VerifyBackup,
                ["--exit-on-error", backupData],
                EmptyEnvironment,
                _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);

        var staging = _paths.RestoreStaging(request);
        if (Directory.Exists(staging))
            throw new InvalidOperationException("An incomplete PostgreSQL fresh-target restore requires explicit reconciliation.");
        Directory.CreateDirectory(staging);
        var targetData = Path.Combine(staging, "data");
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Transferring, 1));
        var started = Stopwatch.GetTimestamp();
        if (dependencyData.Count == 0)
        {
            await CopyDirectoryAsync(sourceData, targetData, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var combineVersion = await ReadVersionAsync(
                PostgreSqlNativeTool.CombineBackup, cancellationToken).ConfigureAwait(false);
            if (combineVersion != Volatile.Read(ref _nativeMajorVersion) || combineVersion < 17)
                throw new InvalidOperationException(
                    "PostgreSQL incremental restore requires matching PostgreSQL 17 or later pg_combinebackup tools.");
            var arguments = new List<string> { "--output", targetData };
            arguments.AddRange(dependencyData);
            arguments.Add(sourceData);
            await _runner.RunAsync(new PostgreSqlNativeInvocation(
                PostgreSqlNativeTool.CombineBackup,
                arguments,
                EmptyEnvironment,
                _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        }
        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Transferring, 60));
        await _runner.RunAsync(new PostgreSqlNativeInvocation(
            PostgreSqlNativeTool.VerifyBackup,
            ["--exit-on-error", targetData],
            EmptyEnvironment,
            _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (request.Recovery is not null)
            await ConfigurePointInTimeRecoveryAsync(targetData, request.Recovery, cancellationToken).ConfigureAwait(false);

        var profile = _options.FreshTargetProfiles[request.FreshTarget.Profile];
        var startedServer = false;
        PostgreSqlFreshTargetValidation validation;
        try
        {
            await _runner.RunAsync(new PostgreSqlNativeInvocation(
                PostgreSqlNativeTool.Control,
                ["start", "--pgdata", targetData, "--wait",
                    "--timeout", Math.Ceiling(profile.StartupTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                    "--log", Path.Combine(staging, "postgres-validation.log"),
                    "--options", $"-p {profile.Port} -c listen_addresses={profile.Host}"],
                ConnectionEnvironment(),
                profile.StartupTimeout), cancellationToken).ConfigureAwait(false);
            startedServer = true;
            progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 80));
            validation = await _validator.ValidateAsync(
                profile, ConnectionEnvironment(), sourceEvidence.SystemIdentifier, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (startedServer)
                await _runner.RunAsync(new PostgreSqlNativeInvocation(
                    PostgreSqlNativeTool.Control,
                    ["stop", "--pgdata", targetData, "--wait", "--mode=fast"],
                    EmptyEnvironment,
                    profile.StartupTimeout), CancellationToken.None).ConfigureAwait(false);
        }

        progress.Report(new DatabaseNativeProgress(DatabaseRecoveryPhase.Validating, 100));
        var elapsed = Stopwatch.GetElapsedTime(started);
        var restoredBytes = DirectoryBytes(targetData);
        var statistics = new DatabaseRecoveryRunStatistics
        {
            Engine = DatabaseEngine.PostgreSql,
            Phase = DatabaseRecoveryPhase.Validating,
            StartedUtc = DateTimeOffset.UtcNow - elapsed,
            CompletedUtc = DateTimeOffset.UtcNow,
            Elapsed = elapsed,
            SourceBytes = sourceEvidence.Statistics.SourceBytes,
            RestoredBytes = restoredBytes,
            ArtifactCount = sourceEvidence.Statistics.ArtifactCount,
            AverageThroughputBytesPerSecond = Rate(restoredBytes, elapsed),
            RetryCount = 0,
            WarningCount = 0
        };
        var targetReference = $"postgres-fresh-{request.OperationId.Format()[..12]}";
        var evidence = new PostgreSqlRestoreEvidence(
            request.OperationId,
            request.RestorePointId.Value,
            targetReference,
            sourceEvidence.SystemIdentifier,
            validation.SystemIdentifier,
            validation.ValidationRevision,
            statistics,
            DateTimeOffset.UtcNow);
        await PostgreSqlBackupEvidenceSerializer.WriteRestoreAsync(staging, evidence, cancellationToken).ConfigureAwait(false);
        Directory.Move(staging, final);
        return RestoreResult(evidence);
    }

    static async ValueTask ConfigurePointInTimeRecoveryAsync(
        string targetData, PostgreSqlPreparedRecovery recovery, CancellationToken cancellationToken)
    {
        if (recovery.TargetUtc.Offset != TimeSpan.Zero || recovery.RequiredSegments.Length == 0
            || !Directory.Exists(recovery.WalArchivePath) || recovery.WalArchivePath.Contains('\''))
            throw new InvalidOperationException("The PostgreSQL PITR recovery preparation is incomplete or unsafe.");
        foreach (var segment in recovery.RequiredSegments)
            if (!File.Exists(Path.Combine(recovery.WalArchivePath, segment)))
                throw new FileNotFoundException("A required PostgreSQL PITR WAL segment is missing.", segment);
        var archive = recovery.WalArchivePath.Replace('\\', '/');
        var restoreCommand = OperatingSystem.IsWindows()
            ? $"copy /Y \"{archive}/%f\" \"%p\""
            : $"cp -- \"{archive}/%f\" \"%p\"";
        var target = recovery.TargetUtc.ToString(
            "yyyy-MM-dd HH:mm:ss.ffffffzzz", CultureInfo.InvariantCulture);
        var settings = string.Join(Environment.NewLine,
            $"restore_command = '{restoreCommand}'",
            $"recovery_target_time = '{target}'",
            "recovery_target_action = 'pause'",
            "recovery_target_inclusive = on") + Environment.NewLine;
        await File.AppendAllTextAsync(Path.Combine(targetData, "postgresql.auto.conf"), settings, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(targetData, "recovery.signal"), [], cancellationToken)
            .ConfigureAwait(false);
    }

    async ValueTask EnsureValidatedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _nativeMajorVersion) == 0) await ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    async ValueTask<DatabaseBackupLineage> ResolveLineageAsync(
        PostgreSqlBackupRequest request,
        CancellationToken cancellationToken)
    {
        var lineage = (request.BackupLineage ?? new DatabaseBackupLineage())
            .NormalizeLegacyFull(DatabaseEngine.PostgreSql);
        lineage.Validate(resolvedRequired: true);
        if (lineage.NativeKind != DatabaseNativeBackupKind.PostgreSqlIncremental)
            return lineage;
        var metadata = await _sourceMetadata.ReadAsync(ConnectionString(), cancellationToken).ConfigureAwait(false);
        int? combineMajorVersion = null;
        try
        {
            combineMajorVersion = await ReadVersionAsync(
                PostgreSqlNativeTool.CombineBackup, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgreSqlNativeToolUnavailableException)
        {
            // Automatic mode may safely fall back to a full backup below.
        }

        string? ineligible = null;
        if (Volatile.Read(ref _nativeMajorVersion) < 17)
            ineligible = "PostgreSQL 17 or later native tools are required for incremental backup.";
        else if (combineMajorVersion is null)
            ineligible = "PostgreSQL pg_combinebackup is required for incremental backup.";
        else if (combineMajorVersion != Volatile.Read(ref _nativeMajorVersion))
            ineligible = "PostgreSQL pg_combinebackup must match the other native backup tools.";
        else if (metadata.MajorVersion < 17)
            ineligible = "PostgreSQL 17 or later is required for incremental backup.";
        else if (!metadata.WalSummariesEnabled)
            ineligible = "PostgreSQL summarize_wal must be enabled before incremental backup.";
        PostgreSqlBackupEvidence? parentEvidence = null;
        if (ineligible is null && lineage.ParentRestorePointId is { } parent)
        {
            var parentRoot = _paths.RestorePoint(parent);
            if (!File.Exists(PostgreSqlBackupEvidenceSerializer.BackupEvidencePath(parentRoot)))
                ineligible = "The PostgreSQL incremental parent evidence is unavailable.";
            else
            {
                parentEvidence = await PostgreSqlBackupEvidenceSerializer.ReadBackupAsync(
                    parentRoot, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(parentEvidence.SystemIdentifier, metadata.SystemIdentifier, StringComparison.Ordinal))
                    ineligible = "The PostgreSQL incremental parent belongs to a different database system.";
            }
        }
        else if (ineligible is null)
        {
            ineligible = "The PostgreSQL incremental parent is missing.";
        }

        if (ineligible is null)
            return lineage with { NativeIdentity = metadata.SystemIdentifier };
        if (lineage.RequestedMode != DatabaseBackupMode.Automatic)
            throw new InvalidOperationException(ineligible);
        return new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
            BaseRestorePointId = new DatabaseRestorePointId(request.OperationId.Format()),
            ChainDepth = 0,
            NativeIdentity = metadata.SystemIdentifier
        };
    }

    async ValueTask<int> ReadVersionAsync(PostgreSqlNativeTool tool, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(new PostgreSqlNativeInvocation(
            tool, ["--version"], EmptyEnvironment, TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
        var match = VersionPattern().Match(result.StandardOutput + " " + result.StandardError);
        return match.Success
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException("A PostgreSQL native tool returned an unrecognized version.");
    }

    IReadOnlyDictionary<string, string?> ConnectionEnvironment()
    {
        var connection = new NpgsqlConnectionStringBuilder(ConnectionString());
        if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Username)
            || string.IsNullOrWhiteSpace(connection.Password))
            throw new InvalidOperationException("The configured PostgreSQL backup connection is incomplete.");
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PGHOST"] = connection.Host,
            ["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture),
            ["PGUSER"] = connection.Username,
            ["PGPASSWORD"] = connection.Password,
            ["PGDATABASE"] = string.IsNullOrWhiteSpace(connection.Database) ? "postgres" : connection.Database,
            ["PGSSLMODE"] = connection.SslMode.ToString().ToLowerInvariant()
        };
    }

    string ConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable(_options.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("The configured PostgreSQL connection-string secret reference is unavailable.");
        var connection = new NpgsqlConnectionStringBuilder(raw);
        if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Username)
            || string.IsNullOrWhiteSpace(connection.Password))
            throw new InvalidOperationException("The configured PostgreSQL backup connection is incomplete.");
        return connection.ConnectionString;
    }

    void ValidateRequest(PostgreSqlBackupRequest request)
    {
        if (request.OperationId.Value == Guid.Empty)
            throw new ArgumentException("A PostgreSQL backup operation ID is required.", nameof(request));
        if (!_options.AllowedProtectionSets.Contains(request.ProtectionSetId.Value, StringComparer.Ordinal))
            throw new InvalidOperationException("The PostgreSQL protection set is not allowlisted by this host.");
    }

    void ValidateRestoreRequest(PostgreSqlRestoreRequest request)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.RestorePointId.Value))
            throw new ArgumentException("A PostgreSQL restore operation and restore point are required.", nameof(request));
        if (!_options.FreshTargetProfiles.TryGetValue(request.FreshTarget.Profile, out var profile)
            || !profile.AllowedLogicalTargets.Contains(request.FreshTarget.LogicalTarget, StringComparer.Ordinal))
            throw new InvalidOperationException("The PostgreSQL fresh target is not allowlisted by this host.");
    }

    async ValueTask<PostgreSqlBackupBoundary> BoundaryFromManifestAsync(
        string data,
        TimeSpan elapsed,
        DatabaseBackupLineage lineage,
        CancellationToken cancellationToken)
    {
        var manifest = await ReadManifestAsync(data, cancellationToken).ConfigureAwait(false);
        if (!manifest.WalContinuity.RequiredWalPresent)
            throw new InvalidDataException("The PostgreSQL base backup does not contain its required streamed WAL.");
        lineage = lineage with { NativeIdentity = manifest.SystemIdentifier };
        return new PostgreSqlBackupBoundary(SafeBoundary(manifest.ManifestSha256, lineage))
        {
            WalContinuity = manifest.WalContinuity,
            NativeMajorVersion = Volatile.Read(ref _nativeMajorVersion),
            Statistics = Statistics(DatabaseRecoveryPhase.Capturing, manifest.SourceBytes, manifest.FileCount, elapsed),
            BackupLineage = lineage
        };
    }

    async ValueTask<PostgreSqlManifestEvidence> ReadManifestAsync(
        string data,
        CancellationToken cancellationToken)
    {
        var manifest = await PostgreSqlBackupManifestReader.ReadAsync(data, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(manifest.SystemIdentifier)) return manifest;

        var result = await _runner.RunAsync(new PostgreSqlNativeInvocation(
            PostgreSqlNativeTool.ControlData,
            ["--pgdata", data],
            EmptyEnvironment,
            _options.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        var match = SystemIdentifierPattern().Match(result.StandardOutput + " " + result.StandardError);
        if (!match.Success)
            throw new InvalidDataException("PostgreSQL control data did not contain a database system identifier.");
        return manifest with { SystemIdentifier = match.Groups[1].Value };
    }

    static PostgreSqlBackupBoundary Boundary(PostgreSqlBackupEvidence evidence)
        => new(evidence.SafeBoundaryReference)
        {
            WalContinuity = evidence.WalContinuity,
            NativeMajorVersion = evidence.NativeMajorVersion,
            Statistics = evidence.Statistics,
            BackupLineage = evidence.BackupLineage?.NormalizeLegacyFull(DatabaseEngine.PostgreSql)
        };

    static PostgreSqlVerificationResult Verification(PostgreSqlBackupEvidence evidence)
        => new(DatabaseVerificationLevel.Native, true)
        {
            SafeEvidenceReference = $"postgres-verify-{evidence.ManifestSha256[..16]}",
            Statistics = evidence.Statistics
        };

    static PostgreSqlRestoreResult RestoreResult(PostgreSqlRestoreEvidence evidence)
        => new(true, evidence.ValidationRevision)
        {
            SafeTargetReference = evidence.SafeTargetReference,
            SourceSystemIdentifier = evidence.SourceSystemIdentifier,
            RestoredSystemIdentifier = evidence.RestoredSystemIdentifier,
            Statistics = evidence.Statistics
        };

    static DatabaseRecoveryRunStatistics Statistics(
        DatabaseRecoveryPhase phase,
        long sourceBytes,
        int artifactCount,
        TimeSpan elapsed)
        => new()
        {
            Engine = DatabaseEngine.PostgreSql,
            Phase = phase,
            StartedUtc = DateTimeOffset.UtcNow - elapsed,
            CompletedUtc = DateTimeOffset.UtcNow,
            Elapsed = elapsed,
            SourceBytes = sourceBytes,
            StoredBytes = sourceBytes,
            TransferredBytes = sourceBytes,
            ArtifactCount = artifactCount,
            AverageThroughputBytesPerSecond = Rate(sourceBytes, elapsed),
            RetryCount = 0,
            WarningCount = 0
        };

    static double? Rate(long bytes, TimeSpan elapsed)
        => elapsed > TimeSpan.Zero ? bytes / elapsed.TotalSeconds : null;

    static string SafeBoundary(string manifestSha256, DatabaseBackupLineage lineage)
        => lineage.NativeKind == DatabaseNativeBackupKind.PostgreSqlIncremental
            ? $"postgres-incremental-{manifestSha256[..16]}"
            : $"postgres-base-{manifestSha256[..16]}";

    static void EnsureBoundary(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("The PostgreSQL verification boundary does not match captured evidence.");
    }

    static async ValueTask CopyDirectoryAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("PostgreSQL restore sources cannot contain reparse points.");
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("PostgreSQL restore sources cannot contain reparse points.");
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    static long DirectoryBytes(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(static file => new FileInfo(file).Length);

    static readonly IReadOnlyDictionary<string, string?> EmptyEnvironment
        = new Dictionary<string, string?>(StringComparer.Ordinal);

    [GeneratedRegex(@"(?:PostgreSQL\)?\s+)(\d+)(?:\.|\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"Database system identifier:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SystemIdentifierPattern();
}
