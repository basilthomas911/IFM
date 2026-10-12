using System.Diagnostics;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Recovery;

namespace TomasAI.IFM.Framework.Storage.IntegrationTests;

public sealed class DockerDatabaseReplacementIntegrationTests
{
    [Fact]
    public async Task Stopped_postgres_volume_replacement_and_rollback_preserve_original_endpoint()
    {
        if (Environment.GetEnvironmentVariable("IFM_LIVE_DOCKER_RECOVERY_TESTS") != "1") return;
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var prefix = "ifm-recovery-test-" + operation.Format();
        var active = prefix + "-active";
        var prepared = prefix + "-prepared";
        var volumes = new[] { active, prepared, "ifm-recovery-rollback-" + operation.Format() };
        var containers = new[] { prefix + "-active", prefix + "-prepared" };
        var image = (await Docker(["inspect", "--format", "{{.Image}}", "ifm_db"])).Trim();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            for (var index = 0; index < 2; index++)
            {
                await Docker(["volume", "create", "--label", "ifm.recovery-test=" + operation.Format(), volumes[index]]);
                await Docker(["run", "-d", "--name", containers[index], "--network", "none",
                    "--label", "ifm.recovery-test=" + operation.Format(),
                    "--mount", "type=volume,src=" + volumes[index] + ",dst=/var/lib/postgresql/data",
                    "-e", "POSTGRES_HOST_AUTH_METHOD=trust", image]);
                await Ready(containers[index], deadline.Token);
                await Docker(["exec", containers[index], "psql", "-U", "postgres", "-v", "ON_ERROR_STOP=1", "-c",
                    "CREATE TABLE recovery_probe (id int primary key, value text); INSERT INTO recovery_probe VALUES (1,'"
                    + (index == 0 ? "original" : "restored") + "');"]);
                await Docker(["stop", "--timeout", "30", containers[index]]);
            }
            var replacement = new DockerNamedVolumeDataReplacement();
            var plan = await replacement.PrepareAsync(operation, containers[0], "/var/lib/postgresql/data", prepared, deadline.Token);
            await replacement.ReplaceAsync(plan, deadline.Token);
            await Docker(["start", containers[0]]);
            await Ready(containers[0], deadline.Token);
            Assert.Equal("restored", (await Docker(["exec", containers[0], "psql", "-U", "postgres", "-Atc", "SELECT value FROM recovery_probe WHERE id=1;"])).Trim());
            // The same container/name remains the endpoint throughout recovery.
            Assert.Equal(containers[0], (await Docker(["inspect", "--format", "{{.Name}}", containers[0]])).Trim().TrimStart('/'));
            await Assert.ThrowsAsync<InvalidOperationException>(() => replacement.RollbackAsync(plan, deadline.Token).AsTask());
            await Docker(["stop", "--timeout", "30", containers[0]]);
            await replacement.RollbackAsync(plan, deadline.Token);
            await Docker(["start", containers[0]]);
            await Ready(containers[0], deadline.Token);
            Assert.Equal("original", (await Docker(["exec", containers[0], "psql", "-U", "postgres", "-Atc", "SELECT value FROM recovery_probe WHERE id=1;"])).Trim());
        }
        finally
        {
            foreach (var name in containers) await Docker(["rm", "-f", name], allowFailure: true);
            // These exact random test names are the only volumes this test can remove.
            foreach (var name in volumes) await Docker(["volume", "rm", name], allowFailure: true);
        }
    }

    static async Task Ready(string container, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var result = await Docker(["exec", container, "pg_isready", "-U", "postgres"], allowFailure: true);
            if (result.Contains("accepting connections", StringComparison.Ordinal)) return;
            await Task.Delay(500, token);
        }
    }

    static async Task<string> Docker(string[] arguments, bool allowFailure = false)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await error;
        if (!allowFailure && process.ExitCode != 0) throw new InvalidOperationException("Isolated Docker recovery test failed with exit code " + process.ExitCode);
        return await output;
    }
}
