using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class PostgreSqlNativeDockerIntegrationTests : IAsyncLifetime
{
    const string Image = "postgres:17.2";
    const string ConnectionVariable = "IFM_GATE6_DOCKER_POSTGRES_CONNECTION";
    const string Password = "gate6-disposable-password";
    readonly string _id = Guid.NewGuid().ToString("N")[..12];
    readonly string _root = Path.Combine(Path.GetTempPath(), "ifm-gate6-native", Guid.NewGuid().ToString("N"));
    string SourceContainer => $"ifm-gate6-source-{_id}";
    string TargetContainer => $"ifm-gate6-target-{_id}";
    int _sourcePort;
    int _targetPort;
    string _sourceConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _sourcePort = FreePort();
        _targetPort = FreePort();
        await DockerAsync(["run", "--detach", "--name", SourceContainer,
            "--publish", $"127.0.0.1:{_sourcePort}:5432",
            "--env", $"POSTGRES_PASSWORD={Password}", Image, "-c", "summarize_wal=on"]);
        _sourceConnectionString = $"Host=127.0.0.1;Port={_sourcePort};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
        Environment.SetEnvironmentVariable(ConnectionVariable, _sourceConnectionString);
        await WaitForPostgreSqlAsync(_sourceConnectionString);
        await using var connection = new NpgsqlConnection(_sourceConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE gate6_restore_probe(id integer primary key, payload text not null); INSERT INTO gate6_restore_probe VALUES (1, 'native-restore-ok'); CHECKPOINT;";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("Category", "Gate6NativeIntegration")]
    [Trait("Category", "Gate10NativeIntegration")]
    public async Task Physical_base_backup_is_verified_and_boots_as_an_isolated_fresh_target()
    {
        var options = Options();
        var runner = new DockerPostgreSqlNativeRunner(SourceContainer, TargetContainer, Password, _targetPort);
        var validator = new LiveSyntheticTargetValidator(TargetContainer);
        var capability = new PostgreSqlBackupCapability(options, runner, validator);
        var backupOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        await capability.ValidateAsync(CancellationToken.None);

        var boundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var restartedCapability = new PostgreSqlBackupCapability(options, runner, validator);
        await restartedCapability.ValidateAsync(CancellationToken.None);
        var recoveredBoundary = await restartedCapability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verification = await restartedCapability.VerifyAsync(
            new PostgreSqlVerificationRequest(backupOperation, recoveredBoundary.SafeBoundaryReference), CancellationToken.None);
        var restore = await restartedCapability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(backupOperation.Format()),
                new DatabaseFreshTargetDescriptor("docker-validation", "gate6-native")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        recoveredBoundary.SafeBoundaryReference.Should().Be(boundary.SafeBoundaryReference);
        recoveredBoundary.WalContinuity.Should().Be(boundary.WalContinuity);
        boundary.NativeMajorVersion.Should().Be(17);
        boundary.WalContinuity!.RequiredWalPresent.Should().BeTrue();
        boundary.WalContinuity.RequiredSegmentCount.Should().BeGreaterThan(0);
        verification.Succeeded.Should().BeTrue();
        restore.Succeeded.Should().BeTrue();
        restore.SourceSystemIdentifier.Should().Be(restore.RestoredSystemIdentifier);
        validator.SyntheticValue.Should().Be("native-restore-ok");
        runner.BaseBackupExecutionCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Gate6NativeIntegration")]
    [Trait("Category", "Gate10NativeIntegration")]
    public async Task Physical_incremental_backup_is_verified_combined_and_boots_with_the_latest_data()
    {
        var options = Options();
        var runner = new DockerPostgreSqlNativeRunner(SourceContainer, TargetContainer, Password, _targetPort);
        var validator = new LiveSyntheticTargetValidator();
        var capability = new PostgreSqlBackupCapability(options, runner, validator);
        var baseOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var baseRestorePoint = new DatabaseRestorePointId(baseOperation.Format());

        var baseBoundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(baseOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        _ = await capability.VerifyAsync(
            new PostgreSqlVerificationRequest(baseOperation, baseBoundary.SafeBoundaryReference), CancellationToken.None);

        await ExecuteSourceSqlAsync(
            "UPDATE gate6_restore_probe SET payload = 'native-incremental-ok' WHERE id = 1; CHECKPOINT; SELECT pg_switch_wal(); CHECKPOINT;");
        await WaitForWalSummaryAsync();

        var incrementalOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Incremental,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
            BaseRestorePointId = baseRestorePoint,
            ParentRestorePointId = baseRestorePoint,
            ChainDepth = 1
        };
        var incrementalBoundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(
                incrementalOperation, new DatabaseProtectionSetId("core-postgresql"), lineage),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        var verification = await capability.VerifyAsync(
            new PostgreSqlVerificationRequest(
                incrementalOperation, incrementalBoundary.SafeBoundaryReference, incrementalBoundary.BackupLineage),
            CancellationToken.None);
        var restore = await capability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(incrementalOperation.Format()),
                new DatabaseFreshTargetDescriptor("docker-validation", "gate6-native"),
                [baseRestorePoint]),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        incrementalBoundary.BackupLineage!.NativeKind.Should().Be(DatabaseNativeBackupKind.PostgreSqlIncremental);
        verification.Succeeded.Should().BeTrue();
        restore.Succeeded.Should().BeTrue();
        validator.SyntheticValue.Should().Be("native-incremental-ok");
        runner.BaseBackupExecutionCount.Should().Be(2);
        runner.CombineBackupExecutionCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Gate9NativeIntegration")]
    [Trait("Category", "Gate10NativeIntegration")]
    public async Task Full_plus_six_direct_parent_incrementals_combine_and_boot_with_latest_data()
    {
        var started = Stopwatch.GetTimestamp();
        var options = Options();
        var runner = new DockerPostgreSqlNativeRunner(SourceContainer, TargetContainer, Password, _targetPort);
        var validator = new LiveSyntheticTargetValidator();
        var capability = new PostgreSqlBackupCapability(options, runner, validator);
        var baseOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var baseRestorePoint = new DatabaseRestorePointId(baseOperation.Format());
        var baseBoundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(baseOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        _ = await capability.VerifyAsync(
            new PostgreSqlVerificationRequest(baseOperation, baseBoundary.SafeBoundaryReference), CancellationToken.None);

        var dependencies = new List<DatabaseRestorePointId> { baseRestorePoint };
        var parent = baseRestorePoint;
        DatabaseRecoveryOperationId finalOperation = default;
        PostgreSqlBackupBoundary? finalBoundary = null;
        for (var depth = 1; depth <= 6; depth++)
        {
            await ExecuteSourceSqlAsync(
                $"UPDATE gate6_restore_probe SET payload = 'native-depth-{depth}' WHERE id = 1; "
                + "CHECKPOINT; SELECT pg_switch_wal(); CHECKPOINT;");
            await WaitForWalSummaryAsync();
            var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
            var restorePoint = new DatabaseRestorePointId(operation.Format());
            var lineage = new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Incremental,
                ResolvedMode = DatabaseBackupMode.Incremental,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
                BaseRestorePointId = baseRestorePoint,
                ParentRestorePointId = parent,
                ChainDepth = depth
            };
            var boundary = await capability.CreateBaseBackupAsync(
                new PostgreSqlBackupRequest(
                    operation, new DatabaseProtectionSetId("core-postgresql"), lineage),
                new Progress<DatabaseNativeProgress>(), CancellationToken.None);
            _ = await capability.VerifyAsync(
                new PostgreSqlVerificationRequest(operation, boundary.SafeBoundaryReference, lineage), CancellationToken.None);
            if (depth < 6) dependencies.Add(restorePoint);
            parent = restorePoint;
            finalOperation = operation;
            finalBoundary = boundary;
        }

        var restore = await capability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(finalOperation.Format()),
                new DatabaseFreshTargetDescriptor("docker-validation", "gate6-native"),
                [.. dependencies]),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        finalBoundary.Should().NotBeNull();
        finalBoundary!.BackupLineage!.ChainDepth.Should().Be(6);
        finalBoundary.BackupLineage.ParentRestorePointId.Should().Be(dependencies[^1]);
        restore.Succeeded.Should().BeTrue();
        validator.SyntheticValue.Should().Be("native-depth-6");
        runner.BaseBackupExecutionCount.Should().Be(7);
        runner.CombineBackupExecutionCount.Should().Be(1);
        Stopwatch.GetElapsedTime(started).Should().BeLessThan(TimeSpan.FromMinutes(10));
    }

    [Fact]
    [Trait("Category", "Gate9NativeIntegration")]
    [Trait("Category", "Gate10NativeIntegration")]
    public async Task Physical_base_backup_and_archived_wal_boot_to_the_selected_utc_target()
    {
        var started = Stopwatch.GetTimestamp();
        var options = Options();
        var walArchive = Path.Combine(_root, "pitr-wal");
        Directory.CreateDirectory(walArchive);
        var runner = new DockerPostgreSqlNativeRunner(
            SourceContainer, TargetContainer, Password, _targetPort, walArchive);
        var validator = new LiveSyntheticTargetValidator(TargetContainer);
        var capability = new PostgreSqlBackupCapability(options, runner, validator);
        var backupOperation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var boundary = await capability.CreateBaseBackupAsync(
            new PostgreSqlBackupRequest(backupOperation, new DatabaseProtectionSetId("core-postgresql")),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);
        _ = await capability.VerifyAsync(
            new PostgreSqlVerificationRequest(backupOperation, boundary.SafeBoundaryReference), CancellationToken.None);

        await ExecuteSourceSqlAsync("UPDATE gate6_restore_probe SET payload = 'before-pitr-target' WHERE id = 1");
        var recoveryTargetUtc = await ReadSourceUtcAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await ExecuteSourceSqlAsync("UPDATE gate6_restore_probe SET payload = 'after-pitr-target' WHERE id = 1");
        var segment = await ReadSourceTextAsync("SELECT pg_walfile_name(pg_current_wal_insert_lsn())");
        await ExecuteSourceSqlAsync("SELECT pg_switch_wal(); CHECKPOINT;");
        await DockerAsync(["cp",
            $"{SourceContainer}:/var/lib/postgresql/data/pg_wal/{segment}",
            Path.Combine(walArchive, segment)]);

        var restore = await capability.RestoreToFreshTargetAsync(
            new PostgreSqlRestoreRequest(
                new DatabaseRecoveryOperationId(Guid.NewGuid()),
                new DatabaseRestorePointId(backupOperation.Format()),
                new DatabaseFreshTargetDescriptor(
                    "docker-validation", "gate6-native", recoveryTargetUtc,
                    new DatabaseArtifactReplicaId("aws-primary")),
                Recovery: new PostgreSqlPreparedRecovery(
                    recoveryTargetUtc, segment[..8], walArchive, [segment])),
            new Progress<DatabaseNativeProgress>(), CancellationToken.None);

        restore.Succeeded.Should().BeTrue();
        validator.SyntheticValue.Should().Be("before-pitr-target");
        Stopwatch.GetElapsedTime(started).Should().BeLessThan(TimeSpan.FromMinutes(5));
    }

    PostgreSqlBackupOptions Options() => new()
    {
        ToolDirectory = Path.Combine(_root, "container-tools"),
        BackupRoot = Path.Combine(_root, "backup"),
        RestoreRoot = Path.Combine(_root, "restore"),
        ConnectionStringEnvironmentVariable = ConnectionVariable,
        AllowedProtectionSets = ["core-postgresql"],
        FreshTargetProfiles = new Dictionary<string, PostgreSqlFreshTargetProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["docker-validation"] = new()
            {
                Host = "127.0.0.1",
                Port = _targetPort,
                Database = "postgres",
                AllowedLogicalTargets = ["gate6-native"],
                StartupTimeout = TimeSpan.FromMinutes(1)
            }
        },
        MinimumMajorVersion = 17,
        MaximumMajorVersion = 17,
        ProcessTimeout = TimeSpan.FromMinutes(5),
        RequirePersistentBackupRoot = false
    };

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionVariable, null);
        await RemoveContainerAsync(TargetContainer);
        await RemoveContainerAsync(SourceContainer);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static async Task WaitForPostgreSqlAsync(string connectionString)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException)
            {
                await Task.Delay(250);
            }
        }
        throw new TimeoutException("The disposable PostgreSQL source did not become ready.");
    }

    async Task ExecuteSourceSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_sourceConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    async Task<DateTimeOffset> ReadSourceUtcAsync()
    {
        await using var connection = new NpgsqlConnection(_sourceConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT clock_timestamp()";
        var value = (DateTime)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("PostgreSQL returned no UTC timestamp."));
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    async Task<string> ReadSourceTextAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_sourceConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("PostgreSQL returned no text value."));
    }

    async Task WaitForWalSummaryAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = new NpgsqlConnection(_sourceConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT FROM pg_available_wal_summaries())";
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(250);
        }
        throw new TimeoutException("PostgreSQL did not produce a WAL summary for incremental backup.");
    }

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
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException($"The disposable Docker PostgreSQL operation failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }

    static async Task<string> DockerDiagnosticAsync(IReadOnlyList<string> arguments)
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
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (output + Environment.NewLine + error).Trim();
    }

    sealed class LiveSyntheticTargetValidator(string? diagnosticContainer = null) : IPostgreSqlFreshTargetValidator
    {
        readonly PostgreSqlFreshTargetValidator _native = new();
        public string SyntheticValue { get; private set; } = string.Empty;

        public async ValueTask<PostgreSqlFreshTargetValidation> ValidateAsync(
            PostgreSqlFreshTargetProfileOptions profile,
            IReadOnlyDictionary<string, string?> sourceEnvironment,
            string expectedSystemIdentifier,
            CancellationToken cancellationToken)
        {
            PostgreSqlFreshTargetValidation validation;
            try
            {
                validation = await _native.ValidateAsync(
                    profile, sourceEnvironment, expectedSystemIdentifier, cancellationToken);
            }
            catch (Exception exception) when (diagnosticContainer is not null)
            {
                var logs = await DockerDiagnosticAsync(["logs", diagnosticContainer]);
                throw new InvalidOperationException(
                    $"The disposable PostgreSQL target failed validation. Container logs: {logs}", exception);
            }
            var connectionString = $"Host={profile.Host};Port={profile.Port};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload FROM gate6_restore_probe WHERE id = 1";
            SyntheticValue = (string)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("The synthetic restored row is missing."));
            return validation;
        }
    }

    sealed class DockerPostgreSqlNativeRunner(
        string sourceContainer,
        string targetContainer,
        string password,
        int targetPort,
        string? pitrWalArchivePath = null) : IPostgreSqlNativeProcessRunner
    {
        public int BaseBackupExecutionCount { get; private set; }
        public int CombineBackupExecutionCount { get; private set; }

        public async ValueTask<PostgreSqlNativeResult> RunAsync(
            PostgreSqlNativeInvocation invocation,
            CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            string output;
            if (invocation.Arguments.SequenceEqual(["--version"]))
            {
                output = await DockerAsync(["run", "--rm", Image, Tool(invocation.Tool), "--version"]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.BaseBackup)
            {
                BaseBackupExecutionCount++;
                var data = ValueAfter(invocation.Arguments, "--pgdata");
                var incremental = invocation.Arguments.SingleOrDefault(
                    value => value.StartsWith("--incremental=", StringComparison.Ordinal));
                var translated = invocation.Arguments.Select(value => value == data
                    ? "/backup"
                    : value == incremental ? "--incremental=/parent/backup_manifest" : value).ToArray();
                var arguments = new List<string>
                {
                    "run", "--rm", "--network", $"container:{sourceContainer}",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/backup",
                };
                if (incremental is not null)
                {
                    var manifest = incremental["--incremental=".Length..];
                    arguments.AddRange(["--mount",
                        $"type=bind,source={Path.GetFullPath(Path.GetDirectoryName(manifest)!)},target=/parent,readonly"]);
                }
                arguments.AddRange([
                    "--env", "PGHOST=127.0.0.1", "--env", "PGPORT=5432",
                    "--env", "PGUSER=postgres", "--env", $"PGPASSWORD={password}", "--env", "PGDATABASE=postgres",
                    Image, "pg_basebackup", .. translated]);
                output = await DockerAsync(arguments);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.CombineBackup)
            {
                CombineBackupExecutionCount++;
                var target = ValueAfter(invocation.Arguments, "--output");
                var targetParent = Path.GetDirectoryName(target)
                    ?? throw new InvalidOperationException("The combined target has no parent directory.");
                var targetIndex = invocation.Arguments.ToList().IndexOf(target);
                var sources = invocation.Arguments.Skip(targetIndex + 1).ToArray();
                var arguments = new List<string>
                {
                    "run", "--rm",
                    "--mount", $"type=bind,source={Path.GetFullPath(targetParent)},target=/output"
                };
                for (var index = 0; index < sources.Length; index++)
                    arguments.AddRange(["--mount",
                        $"type=bind,source={Path.GetFullPath(sources[index])},target=/input{index},readonly"]);
                arguments.AddRange([Image, "pg_combinebackup", "--output", "/output/data"]);
                for (var index = 0; index < sources.Length; index++) arguments.Add($"/input{index}");
                output = await DockerAsync(arguments);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.VerifyBackup)
            {
                var data = invocation.Arguments[^1];
                var translated = invocation.Arguments.Take(invocation.Arguments.Count - 1).Append("/backup").ToArray();
                output = await DockerAsync(["run", "--rm",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/backup,readonly",
                    Image, "pg_verifybackup", .. translated]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.ControlData)
            {
                var data = ValueAfter(invocation.Arguments, "--pgdata");
                output = await DockerAsync(["run", "--rm",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/backup,readonly",
                    Image, "pg_controldata", "--pgdata", "/backup"]);
            }
            else if (invocation.Tool == PostgreSqlNativeTool.Control && invocation.Arguments[0] == "start")
            {
                var data = ValueAfter(invocation.Arguments, "--pgdata");
                var arguments = new List<string> { "run", "--detach", "--name", targetContainer,
                    "--publish", $"127.0.0.1:{targetPort}:5432",
                    "--mount", $"type=bind,source={Path.GetFullPath(data)},target=/var/lib/postgresql/data",
                    "--env", $"POSTGRES_PASSWORD={password}" };
                if (pitrWalArchivePath is not null)
                {
                    await File.AppendAllTextAsync(
                        Path.Combine(data, "postgresql.auto.conf"),
                        "restore_command = 'cp -- \"/wal/%f\" \"%p\"'" + Environment.NewLine,
                        cancellationToken);
                    arguments.AddRange(["--mount",
                        $"type=bind,source={Path.GetFullPath(pitrWalArchivePath)},target=/wal,readonly"]);
                }
                arguments.Add(Image);
                output = await DockerAsync(arguments);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                var running = await DockerAsync(
                    ["inspect", "--format", "{{.State.Running}}", targetContainer], allowFailure: true);
                if (!string.Equals(running, "true", StringComparison.OrdinalIgnoreCase))
                {
                    var logs = await DockerAsync(["logs", targetContainer], allowFailure: true);
                    throw new InvalidOperationException(
                        $"The disposable PostgreSQL recovery target exited during startup: {logs}");
                }
            }
            else
            {
                await DockerAsync(["stop", targetContainer]);
                output = await DockerAsync(["rm", targetContainer]);
            }
            return new PostgreSqlNativeResult(0, output, "", Stopwatch.GetElapsedTime(started));
        }

        static string Tool(PostgreSqlNativeTool tool) => tool switch
        {
            PostgreSqlNativeTool.BaseBackup => "pg_basebackup",
            PostgreSqlNativeTool.CombineBackup => "pg_combinebackup",
            PostgreSqlNativeTool.VerifyBackup => "pg_verifybackup",
            PostgreSqlNativeTool.Control => "pg_ctl",
            PostgreSqlNativeTool.ControlData => "pg_controldata",
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };

        static string ValueAfter(IReadOnlyList<string> values, string expected)
        {
            for (var index = 0; index < values.Count - 1; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal)) return values[index + 1];
            throw new InvalidOperationException($"Expected native argument '{expected}' was not present.");
        }
    }
}
