using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Recovery;

public sealed record DockerDatabaseReplacementPlan(
    string ContainerName, string DataMountPath, string ActiveVolume,
    string PreparedVolume, string RollbackVolume, string ImageId);

/// <summary>
/// Replaces data in the existing container's named volume. The controller must first fence
/// application writers, validate the prepared volume and stop both database containers.
/// This utility neither authorizes cutover nor claims application recovery is complete.
/// </summary>
public sealed class DockerNamedVolumeDataReplacement(string dockerExecutable = "docker")
{
    public async ValueTask<DockerDatabaseReplacementPlan> PrepareAsync(
        DatabaseRecoveryOperationId operationId, string containerName, string dataMountPath,
        string preparedVolume, CancellationToken cancellationToken)
    {
        RequireName(containerName);
        RequireName(preparedVolume);
        if (operationId.Value == Guid.Empty || !dataMountPath.StartsWith("/", StringComparison.Ordinal)
            || dataMountPath.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Replacement requires an operation ID and an exact absolute data mount.");
        using var container = await InspectAsync(containerName, cancellationToken).ConfigureAwait(false);
        var root = container.RootElement[0];
        var mounts = root.GetProperty("Mounts").EnumerateArray()
            .Where(m => m.GetProperty("Destination").GetString() == dataMountPath).ToArray();
        if (mounts.Length != 1 || mounts[0].GetProperty("Type").GetString() != "volume")
            throw new InvalidOperationException("Replacement requires exactly one named data volume at the configured mount.");
        var activeVolume = mounts[0].GetProperty("Name").GetString()!;
        RequireName(activeVolume);
        if (activeVolume == preparedVolume)
            throw new InvalidOperationException("Prepared and active data volumes must be distinct.");
        _ = await RunAsync(["volume", "inspect", preparedVolume], cancellationToken).ConfigureAwait(false);
        var rollback = "ifm-recovery-rollback-" + operationId.Format();
        // A pre-existing rollback volume could belong to an interrupted replacement.
        var existing = await RunAsync(["volume", "ls", "--filter", "name=^" + rollback + "$", "--format", "{{.Name}}"],
            cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(existing))
            throw new InvalidOperationException("An existing rollback volume requires controller reconciliation.");
        _ = await RunAsync(["volume", "create", "--label", "ifm.recovery-operation=" + operationId.Format(), rollback],
            cancellationToken).ConfigureAwait(false);
        return new(containerName, dataMountPath, activeVolume, preparedVolume, rollback, root.GetProperty("Image").GetString()!);
    }

    /// <summary>Captures an already stopped fresh restore into an operation-specific prepared volume.</summary>
    public async ValueTask<string> CapturePreparedVolumeAsync(DatabaseRecoveryOperationId operationId,
        string sourceContainer, string dataMountPath, CancellationToken cancellationToken)
    {
        RequireName(sourceContainer);
        using var source = await InspectAsync(sourceContainer, cancellationToken).ConfigureAwait(false);
        var root = source.RootElement[0];
        var mount = root.GetProperty("Mounts").EnumerateArray().Single(m =>
            m.GetProperty("Type").GetString() == "volume" && m.GetProperty("Destination").GetString() == dataMountPath);
        var volume = mount.GetProperty("Name").GetString()!;
        var prepared = "ifm-recovery-prepared-" + operationId.Format();
        var image = root.GetProperty("Image").GetString()!;
        var plan = new DockerDatabaseReplacementPlan(sourceContainer, dataMountPath, volume, prepared, prepared, image);
        await RequireStoppedAndUnchangedAsync(plan, cancellationToken).ConfigureAwait(false);
        var existing = await RunAsync(["volume", "ls", "--filter", "name=^" + prepared + "$", "--format", "{{.Name}}"], cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing)) throw new InvalidOperationException("Prepared volume already exists; reconcile the prior preparation.");
        _ = await RunAsync(["volume", "create", "--label", "ifm.recovery-operation=" + operationId.Format(), prepared], cancellationToken);
        await CopyAsync(image, volume, prepared, clear: false, markComplete: true, cancellationToken);
        return prepared;
    }

    public async ValueTask ReplaceAsync(DockerDatabaseReplacementPlan plan, CancellationToken cancellationToken)
    {
        await RequireStoppedAndUnchangedAsync(plan, cancellationToken).ConfigureAwait(false);
        await CopyAsync(plan.ImageId, plan.ActiveVolume, plan.RollbackVolume, clear: false, markComplete: true,
            cancellationToken).ConfigureAwait(false);
        // Recheck before writing; another process must not have restarted/recreated the container.
        await RequireStoppedAndUnchangedAsync(plan, cancellationToken).ConfigureAwait(false);
        await CopyAsync(plan.ImageId, plan.PreparedVolume, plan.ActiveVolume, clear: true, markComplete: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Without the capture marker, replacement has not yet touched the active volume.</summary>
    public async ValueTask<bool> HasCompletedRollbackCaptureAsync(DockerDatabaseReplacementPlan plan, CancellationToken token)
    {
        RequireName(plan.RollbackVolume);
        var marker = await RunAsync(["run", "--rm", "--network", "none", "--user", "0",
            "--mount", "type=volume,src=" + plan.RollbackVolume + ",dst=/source,readonly",
            "--entrypoint", "/bin/sh", plan.ImageId, "-c",
            "if test -f /source/.ifm-rollback-complete; then printf complete; fi"], token);
        return marker == "complete";
    }

    public async ValueTask RollbackAsync(DockerDatabaseReplacementPlan plan, CancellationToken cancellationToken)
    {
        await RequireStoppedAndUnchangedAsync(plan, cancellationToken).ConfigureAwait(false);
        _ = await RunAsync(["run", "--rm", "--network", "none", "--user", "0",
            "--mount", "type=volume,src=" + plan.RollbackVolume + ",dst=/source,readonly",
            "--entrypoint", "/bin/sh", plan.ImageId, "-c", "test -f /source/.ifm-rollback-complete"],
            cancellationToken).ConfigureAwait(false);
        await CopyAsync(plan.ImageId, plan.RollbackVolume, plan.ActiveVolume, clear: true, markComplete: false,
            cancellationToken).ConfigureAwait(false);
    }

    async ValueTask RequireStoppedAndUnchangedAsync(DockerDatabaseReplacementPlan plan, CancellationToken token)
    {
        RequireName(plan.ContainerName);
        RequireName(plan.ActiveVolume);
        RequireName(plan.PreparedVolume);
        RequireName(plan.RollbackVolume);
        using var container = await InspectAsync(plan.ContainerName, token).ConfigureAwait(false);
        var root = container.RootElement[0];
        var state = root.GetProperty("State");
        if (state.GetProperty("Running").GetBoolean() || state.GetProperty("Paused").GetBoolean()
            || state.GetProperty("Restarting").GetBoolean())
            throw new InvalidOperationException("The database container must be completely stopped before replacing its data.");
        if (root.GetProperty("Image").GetString() != plan.ImageId
            || !root.GetProperty("Mounts").EnumerateArray().Any(m =>
                m.GetProperty("Type").GetString() == "volume"
                && m.GetProperty("Name").GetString() == plan.ActiveVolume
                && m.GetProperty("Destination").GetString() == plan.DataMountPath))
            throw new InvalidOperationException("The configured database container or its data mount changed during recovery.");
    }

    async ValueTask CopyAsync(string imageId, string source, string destination, bool clear, bool markComplete,
        CancellationToken token)
    {
        RequireName(source);
        RequireName(destination);
        if (source == destination) throw new InvalidOperationException("A volume cannot replace itself.");
        if (!Regex.IsMatch(imageId, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Recovery helpers require the inspected immutable database image ID.");
        // Static shell code only. All deletion paths are within /destination, the exact named
        // volume verified above. No Windows paths, command strings or credentials are interpolated.
        var script = clear
            ? "set -eu; test -d /source; test -d /destination; find /destination -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +; cp -a /source/. /destination/; rm -f /destination/.ifm-rollback-complete; sync"
            : "set -eu; test -d /source; test -d /destination; test -z \"$(ls -A /destination)\"; cp -a /source/. /destination/; sync; touch /destination/.ifm-rollback-complete; sync";
        if (!clear && !markComplete) throw new ArgumentException("Rollback capture must record completion.");
        _ = await RunAsync(["run", "--rm", "--network", "none", "--user", "0",
            "--mount", "type=volume,src=" + source + ",dst=/source,readonly",
            "--mount", "type=volume,src=" + destination + ",dst=/destination",
            "--entrypoint", "/bin/sh", imageId, "-c", script], token).ConfigureAwait(false);
    }

    async ValueTask<JsonDocument> InspectAsync(string container, CancellationToken token)
        => JsonDocument.Parse(await RunAsync(["inspect", "--type", "container", container], token).ConfigureAwait(false));

    async ValueTask<string> RunAsync(string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(dockerExecutable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker process could not start.");
        var output = process.StandardOutput.ReadToEndAsync(token);
        var error = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            var result = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
            // Do not publish inspect output or Docker environment values in exception text.
            if (process.ExitCode != 0) throw new InvalidOperationException("Docker recovery operation failed with exit code " + process.ExitCode + ".");
            return result;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    static void RequireName(string name)
    {
        if (!Regex.IsMatch(name ?? "", "^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Recovery requires an exact Docker container or named-volume identifier.");
    }
}
