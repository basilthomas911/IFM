using System.Diagnostics;
using System.Reflection;
using TomasAI.IFM.TestSupport.ResidentActors;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Verifies authoritative snapshot restoration in a new OS process using only disposable PostgreSQL.</summary>
public static class ResidentActorProcessRestartProbe
{
    public static async Task RunAsync(bool child, ResidentPilotKind kind = default, int key = 0)
    {
        var endpoint = child ? Environment.GetEnvironmentVariable("IFM_RESIDENT_CHILD_POSTGRES")
            ?? throw new InvalidOperationException("Missing isolated child fixture endpoint.") : null;
        await using var database = new ResidentPilotDatabase(endpoint);
        await database.InitializeAsync();
        if (child)
        {
            await using var restored = await database.CreateSessionAsync(kind, true);
            var request = restored.Request(key, 3);
            if ((await restored.RunAsync(request)).Result?.Success != true || restored.Loads != 1
                || await database.EventCountAsync(request.StreamId) != 3)
                throw new InvalidOperationException("Child did not restore the two committed source facts and persist exactly one successor.");
            return;
        }
        foreach (var pilot in Enum.GetValues<ResidentPilotKind>())
        {
            key++;
            string stream;
            await using (var session = await database.CreateSessionAsync(pilot, true))
            {
                var first = session.Request(key, 1); stream = first.StreamId;
                if ((await session.RunAsync(first)).Result?.Success != true ||
                    (await session.RunAsync(session.Request(key, 2))).Result?.Success != true)
                    throw new InvalidOperationException("Parent preparation failed.");
            }
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--resident-child-restart"); start.ArgumentList.Add(pilot.ToString());
            start.ArgumentList.Add(key.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.Environment["IFM_RESIDENT_CHILD_POSTGRES"] = database.TestConnectionString;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Child launch failed.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            if (process.ExitCode != 0) throw new InvalidOperationException("Isolated child restart failed: " + await stderr + await stdout);
            if (await database.EventCountAsync(stream) != 3) throw new InvalidOperationException("Child event count mismatch.");
            await using var continued = await database.CreateSessionAsync(pilot, true);
            if ((await continued.RunAsync(continued.Request(key, 4))).Result?.Success != true
                || await database.EventCountAsync(stream) != 4) throw new InvalidOperationException("Parent continuation failed.");
            Console.WriteLine($"Process restart passed: {pilot}; committed versions 2 -> child 3 -> parent 4, no duplicates.");
        }
    }
}
