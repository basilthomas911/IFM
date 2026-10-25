using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Journal;
using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Processing;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services.Recovery;

/// <summary>Replaces existing container data; runs outside the application it stops.</summary>
public sealed class DevelopmentDatabaseReplacementPlatform(
    DevelopmentDatabaseReplacementOptions options, DockerNamedVolumeDataReplacement replacement,
    DynamoDbDatabaseBackupExecutionJournal journal, IActorProducer producer) : IDatabaseReplacementRecoveryPlatform
{
    sealed record Prepared(string OperationId, string RestorePointId, long ValidationRevision,
        DockerDatabaseReplacementPlan Plan);
    readonly HttpClient _health = new() { Timeout = TimeSpan.FromSeconds(5) };
    string ApiExe => Path.Combine(options.RepositoryRoot,
        "TomasAI.IFM.Application.Api.Server", "bin", "Debug", "net10.0", "TomasAI.IFM.Application.Api.Server.exe");
    string OperationRoot(DatabaseCutoverRequest request) => Path.Combine(options.PersistentRoot, request.OperationId.Format());

    public async ValueTask<string> PrepareAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        options.Validate();
        var postgres = request.Engine == DatabaseEngine.PostgreSql;
        if (!postgres && request.Engine != DatabaseEngine.ScyllaDb) throw new InvalidOperationException("Unsupported replacement engine.");
        var profiles = postgres ? options.AllowedPostgreSqlProfiles : options.AllowedScyllaProfiles;
        var sets = postgres ? options.AllowedPostgreSqlProtectionSets : options.AllowedScyllaProtectionSets;
        if (!profiles.Contains(request.FreshTarget.Profile, StringComparer.Ordinal)
            || !sets.Contains(request.ProtectionSetId.Value, StringComparer.Ordinal))
            throw new InvalidOperationException("Replacement target or protection set is not configured on this controller.");
        foreach (var segment in new[] { request.FreshTarget.Profile, request.FreshTarget.LogicalTarget })
            if (!Regex.IsMatch(segment, "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,127}$")) throw new InvalidOperationException("Invalid fresh-target path.");
        var nativeRoot = postgres ? options.PostgreSqlRestoreRoot : options.ScyllaRestoreRoot;
        var targetRoot = Path.Combine(nativeRoot, request.FreshTarget.Profile, request.FreshTarget.LogicalTarget);
        if (Directory.EnumerateDirectories(targetRoot, "*.inprogress").Any())
            throw new InvalidOperationException("An unfinished restore must be reconciled before cutover.");
        var evidenceName = postgres ? "ifm-postgresql-restore-evidence.json" : "ifm-scylla-restore-evidence.json";
        var path = Path.Combine(targetRoot, request.OperationId.Format(), evidenceName);
        using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
        var identity = Property(evidence.RootElement, "OperationId");
        var operationValue = identity.ValueKind == JsonValueKind.Object ? Property(identity, "Value").GetString() : identity.GetString();
        if (Guid.Parse(operationValue!) != request.OperationId.Value
            || Property(evidence.RootElement, "RestorePointId").GetString() != request.RestorePointId.Value
            || Property(evidence.RootElement, "ValidationRevision").GetInt64() != request.ValidationRevision)
            throw new InvalidOperationException("Native restore evidence does not match the approved operation, point and revision.");
        // A fixed fresh target must still belong to this operation, not a later restore.
        var evidenceTime = Property(evidence.RootElement, "ValidatedUtc").GetDateTimeOffset();
        foreach (var candidate in Directory.EnumerateFiles(Path.Combine(nativeRoot, request.FreshTarget.Profile), evidenceName, SearchOption.AllDirectories))
        {
            using var other = JsonDocument.Parse(await File.ReadAllTextAsync(candidate, token));
            if (Property(other.RootElement, "ValidatedUtc").GetDateTimeOffset() > evidenceTime)
                throw new InvalidOperationException("This fixed fresh target has a newer validation; prepare a new restore.");
        }
        if (!File.Exists(ApiExe)) throw new InvalidOperationException("The Development API executable is missing.");
        Directory.CreateDirectory(OperationRoot(request));
        var active = postgres ? options.PostgreSqlContainer : options.ScyllaContainer;
        var mount = postgres ? "/tmp/pgdata" : "/var/lib/scylla";
        string prepared;
        if (postgres)
        {
            prepared = await StagePostgreSqlAsync(request, token);
            var image = (await Docker(["inspect", "--format", "{{.Image}}", active], token)).Trim();
            var control = await Docker(["run", "--rm", "--network", "none", "--mount",
                "type=volume,src=" + prepared + ",dst=/data,readonly", "--entrypoint", "pg_controldata", image, "/data"], token);
            var restoredIdentity = Property(evidence.RootElement, "RestoredSystemIdentifier").GetString();
            var capturedIdentity = Regex.Match(control, @"Database system identifier:\s*(\d+)").Groups[1].Value;
            if (capturedIdentity != restoredIdentity || restoredIdentity != Property(evidence.RootElement, "SourceSystemIdentifier").GetString())
                throw new InvalidOperationException("Prepared PostgreSQL system identity differs from its validated native restore.");
        }
        else
        {
            if (active == options.ScyllaFreshContainer) throw new InvalidOperationException("Fresh and active Scylla containers must differ.");
            if (Property(Property(evidence.RootElement, "Topology"), "LiveNodeCount").GetInt32() != 1)
                throw new InvalidOperationException("This Development volume replacement supports a single-node Scylla restore only.");
            var sourceImage = (await Docker(["inspect", "--format", "{{.Image}}", options.ScyllaFreshContainer], token)).Trim();
            var activeImage = (await Docker(["inspect", "--format", "{{.Image}}", active], token)).Trim();
            if (sourceImage != activeImage) throw new InvalidOperationException("Fresh and active Scylla versions differ; do not copy system data between them.");
            var sourceDescription = await Docker(["exec", options.ScyllaFreshContainer, "nodetool", "describecluster"], token);
            var activeDescription = await Docker(["exec", active, "nodetool", "describecluster"], token);
            var sourceCluster = ReadScyllaClusterName(sourceDescription);
            var activeCluster = ReadScyllaClusterName(activeDescription);
            if (!StringComparer.Ordinal.Equals(sourceCluster, activeCluster))
                throw new InvalidOperationException("Prepared Scylla cluster identity differs from the active container configuration.");
            await Docker(["stop", "--timeout", "120", options.ScyllaFreshContainer], token);
            prepared = await replacement.CapturePreparedVolumeAsync(request.OperationId, options.ScyllaFreshContainer, mount, token);
        }
        var plan = await replacement.PrepareAsync(request.OperationId, active, mount, prepared, token);
        var receipt = Path.Combine(OperationRoot(request), "prepared-replacement.json");
        await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new Prepared(request.OperationId.Format(),
            request.RestorePointId.Value, request.ValidationRevision, plan)), token);
        return receipt;
    }

    /// <summary>Reads the native cluster name; system.local's compatibility field can be empty in Scylla.</summary>
    internal static string ReadScyllaClusterName(string description)
    {
        var name = Regex.Match(description, @"^[ \t]*Name:[ \t]*([^\r\n]+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Scylla did not report a native cluster name.");
        return name;
    }
    async ValueTask<string> StagePostgreSqlAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        var prepared = "ifm-recovery-prepared-" + request.OperationId.Format();
        var exists = await Docker(["volume", "ls", "--filter", "name=^" + prepared + "$", "--format", "{{.Name}}"], token);
        if (!string.IsNullOrWhiteSpace(exists)) throw new InvalidOperationException("Prepared PostgreSQL volume already exists; reconcile preparation.");
        var image = (await Docker(["inspect", "--format", "{{.Image}}", options.PostgreSqlContainer], token)).Trim();
        var staging = Path.Combine(OperationRoot(request), "native-data");
        Directory.CreateDirectory(staging);
        var native = "/var/lib/ifm/database-restore/postgresql/" + request.FreshTarget.Profile + "/"
            + request.FreshTarget.LogicalTarget + "/" + request.OperationId.Format() + "/data/.";
        await Docker(["cp", options.NativeRestoreContainer + ":" + native, staging], token);
        if ((await File.ReadAllTextAsync(Path.Combine(staging, "PG_VERSION"), token)).Trim() != "17")
            throw new InvalidOperationException("Prepared PostgreSQL data has an unsupported major version.");
        await Docker(["volume", "create", "--label", "ifm.recovery-operation=" + request.OperationId.Format(), prepared], token);
        var helper = "ifm-recovery-import-" + request.OperationId.Format();
        try
        {
            await Docker(["run", "-d", "--name", helper, "--network", "none", "--user", "0",
                "--mount", "type=volume,src=" + prepared + ",dst=/destination", "--entrypoint", "/bin/sh", image,
                "-c", "sleep infinity"], token);
            await Docker(["cp", staging + Path.DirectorySeparatorChar + ".", helper + ":/destination"], token);
            await Docker(["exec", helper, "sync"], token);
        }
        finally { await Docker(["rm", "-f", helper], CancellationToken.None, allowFailure: true); }
        return prepared;
    }

    public async ValueTask StopApplicationAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        // Exact known repository executables only; the independent backup/cutover host is excluded.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "IFMServerManager", "TomasAI.IFM.Application.Api.Server", "TomasAI.IFM.UI.Net",
          "TomasAI.IFM.Application.ServerManager.SchedulerHost", "TomasAI.IFM.Application.MarketData.Host",
          "TomasAI.IFM.Application.MarketData.Host.Worker", "TomasAI.IFM.Application.MarketData.Host.Worker.Futures-GLBX.MDP3",
          "TomasAI.IFM.Application.MarketData.Host.Worker.Futures-XCBF.PITCH",
          "TomasAI.IFM.Application.MarketData.Host.Worker.Options-GLBX.MDP3" };
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                // Do not open handles to unrelated protected Windows processes.
                // Checking HasExited before filtering names can fail on System/CSRSS.
                var processName = process.ProcessName;
                if (!names.Contains(processName) && !processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) continue;
                if (process.HasExited) continue;
                string? executable;
                try
                {
                    if (process.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                        executable = process.Modules.Cast<ProcessModule>()
                            .FirstOrDefault(module => names.Contains(Path.GetFileNameWithoutExtension(module.ModuleName)))?.FileName;
                    else if (names.Contains(process.ProcessName)) executable = process.MainModule?.FileName;
                    else continue;
                }
                catch (InvalidOperationException) when (process.HasExited) { continue; }
                if (executable is null) continue;
                if (!Path.GetFullPath(executable).StartsWith(Path.GetFullPath(options.RepositoryRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("An application process is outside the configured repository; stop it explicitly.");
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(token);
                }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        }
    }

    public async ValueTask ReplaceDataAsync(DatabaseCutoverRequest request, string rollbackReference, CancellationToken token)
    {
        var prepared = await ReadPrepared(request, rollbackReference, token);
        await Docker(["stop", "--timeout", "120", prepared.Plan.ContainerName], token);
        await replacement.ReplaceAsync(prepared.Plan, token);
    }

    public async ValueTask ValidateDatabaseAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        var prepared = await ReadPrepared(request, Path.Combine(OperationRoot(request), "prepared-replacement.json"), token);
        await Docker(["start", prepared.Plan.ContainerName], token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        while (true)
        {
            var scyllaIp = request.Engine == DatabaseEngine.ScyllaDb
                ? (await Docker(["inspect", "--format", "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}", prepared.Plan.ContainerName], deadline.Token)).Trim()
                : "";
            var command = request.Engine == DatabaseEngine.PostgreSql
                ? new[] { "exec", prepared.Plan.ContainerName, "pg_isready" }
                : new[] { "exec", prepared.Plan.ContainerName, "cqlsh", scyllaIp, "-e", "SELECT release_version FROM system.local;" };
            try { await Docker(command, deadline.Token); return; }
            catch (InvalidOperationException) { await Task.Delay(1000, deadline.Token); }
        }
    }

    public async ValueTask StartApplicationInRecoveryAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        // Redis survives a database restore; its numeric event IDs and derived read
        // values may describe data newer than the restored database. Writers are stopped.
        var cachePrefixes = Enum.GetNames<TomasAI.IFM.Shared.Caching.DataCacheName>()
            .Where(name => name != "Undefined").ToArray();
        const string invalidate = "local allowed={} for _,v in ipairs(ARGV) do allowed[v]=true end local cursor='0' local removed=0 repeat local r=redis.call('SCAN',cursor,'COUNT',1000) cursor=r[1] for _,k in ipairs(r[2]) do local p=string.match(k,'^([^:]+):') if allowed[p] then removed=removed+redis.call('UNLINK',k) end end until cursor=='0' return removed";
        var deleted = await Docker(new[] { "exec", options.RedisContainer, "redis-cli", "--raw", "EVAL", invalidate, "0" }
            .Concat(cachePrefixes).ToArray(), token);
        if (!long.TryParse(deleted.Trim(), out var removed) || removed < 0)
            throw new InvalidOperationException("Restored database cache invalidation did not return a valid result.");
        await File.WriteAllTextAsync(Path.Combine(OperationRoot(request), "derived-cache-invalidation.json"),
            JsonSerializer.Serialize(new { RemovedKeys = removed, Prefixes = cachePrefixes, CompletedUtc = DateTimeOffset.UtcNow }), token);
        // Composition root and real providers run; no HTTP listeners, actors, schedules or writers start.
        var start = new ProcessStartInfo(ApiExe) { WorkingDirectory = Path.GetDirectoryName(ApiExe)!,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--validate-restored-databases-only");
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["IFM_REPOSITORY_ROOT"] = options.RepositoryRoot;
        using var validationDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        validationDeadline.CancelAfter(TimeSpan.FromMinutes(5));
        var result = await Run(start, validationDeadline.Token);
        await File.WriteAllTextAsync(Path.Combine(OperationRoot(request), "application-validation.log"), result, token);
    }

    public ValueTask ValidateApplicationAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(Path.Combine(OperationRoot(request), "application-validation.log")))
            throw new InvalidOperationException("Application-provider validation has not completed.");
        return ValueTask.CompletedTask;
    }

    public async ValueTask ResumeAdmissionAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = options.RepositoryRoot };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(options.RepositoryRoot, "scripts", "Development", "Start-IFMDevelopment.ps1"), "-NoBuild" })
            start.ArgumentList.Add(argument);
        using var launcher = Process.Start(start) ?? throw new InvalidOperationException("Development launcher could not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        while (true)
        {
            try { using var response = await _health.GetAsync(options.ApiHealthUrl, deadline.Token); if (response.IsSuccessStatusCode) break; }
            catch (HttpRequestException) { }
            await Task.Delay(1000, deadline.Token);
        }
        // Restoring PostgreSQL removes operation events newer than its backup. Rehydrate only
        // this validated cutover from the independent journal before its completion is published.
        var latest = await journal.ReadLatestServiceEventAsync(request.OperationId, token)
            ?? throw new InvalidOperationException("Validated restore service state is missing from the external journal.");
        var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("cutover-recovery:" + request.OperationId.Format()
            + ":" + request.ValidationRevision + ":" + request.FencingToken)).AsSpan(0, 16));
        var command = new ReconcileDatabaseBackupServiceStateCommand
        {
            CommandId = eventId, EntityId = request.OperationId,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupInternalCommand.Actor, "ReconcileServiceState", request.OperationId.Format()),
            Source = latest.Source with { SourceEventId = eventId, OperationKind = DatabaseRecoveryOperationKind.Cutover,
                Phase = DatabaseRecoveryPhase.CuttingOver }, ValidationRevision = request.ValidationRevision,
            RestorePointId = request.RestorePointId, FreshTarget = request.FreshTarget,
            CutoverState = DatabaseCutoverState.InProgress, VerificationLevel = DatabaseVerificationLevel.ApplicationValidation,
            SafeDiagnosticReference = "external-journal-validated-cutover-recovery"
        };
        command.Validate();
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, DatabaseBackupInternalCommand.Actor), token);
        var result = await producer.RequestAsync<ReconcileDatabaseBackupServiceStateCommand, DatabaseRecoveryOperationId, GuidResult>(command.Subject, command, command.EntityId, token);
        if (!result.Success) throw new InvalidOperationException("Restored cutover state reconciliation was rejected: " + result.ErrorMessage);
    }

    public async ValueTask RollbackAsync(DatabaseCutoverRequest request, string rollbackReference, CancellationToken token)
    {
        await StopApplicationAsync(request, token);
        var prepared = await ReadPrepared(request, rollbackReference, token);
        await Docker(["stop", "--timeout", "120", prepared.Plan.ContainerName], token);
        if (await replacement.HasCompletedRollbackCaptureAsync(prepared.Plan, token))
            await replacement.RollbackAsync(prepared.Plan, token);
        await ValidateDatabaseAsync(request, token);
    }

    public async ValueTask ValidateRollbackAsync(DatabaseCutoverRequest request, CancellationToken token)
    {
        await StartApplicationInRecoveryAsync(request, token);
        await ValidateApplicationAsync(request, token);
        // Keep application admission stopped after rollback; original data is available for inspection.
    }

    async ValueTask<Prepared> ReadPrepared(DatabaseCutoverRequest request, string path, CancellationToken token)
    {
        var expected = Path.Combine(OperationRoot(request), "prepared-replacement.json");
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), Path.GetFullPath(expected)))
            throw new InvalidOperationException("Rollback reference escaped the operation journal.");
        var result = JsonSerializer.Deserialize<Prepared>(await File.ReadAllTextAsync(path, token))!;
        if (result.OperationId != request.OperationId.Format() || result.RestorePointId != request.RestorePointId.Value
            || result.ValidationRevision != request.ValidationRevision) throw new InvalidOperationException("Prepared data identity changed.");
        return result;
    }

    static JsonElement Property(JsonElement element, string name)
        => element.EnumerateObject().Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    static ValueTask<string> Docker(string[] arguments, CancellationToken token, bool allowFailure = false)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Run(start, token, allowFailure);
    }
    static async ValueTask<string> Run(ProcessStartInfo start, CancellationToken token, bool allowFailure = false)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Recovery dependency could not start.");
        var output = process.StandardOutput.ReadToEndAsync(token);
        var error = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token);
            var text = await output; _ = await error;
            if (process.ExitCode != 0 && !allowFailure) throw new InvalidOperationException("Recovery dependency failed with exit code " + process.ExitCode);
            return text;
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
    }
}
