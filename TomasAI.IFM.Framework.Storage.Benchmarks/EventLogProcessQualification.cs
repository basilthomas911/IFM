using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Shared.Exceptions;
using static TomasAI.IFM.Framework.Storage.Benchmarks.EventLogMarkerQualification;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Owned subprocess/container faults. Never resolves destructive targets from caller-supplied names.</summary>
internal static class EventLogProcessQualification
{
    const long Gate = 19760920;
    static string _containerId = "";
    internal sealed record Input(string Provider, string Stream, long StreamId, int EventNameId,
        long Expected, Guid CommandId, bool Batch);

    internal static async Task ChildAsync()
    {
        var input = JsonSerializer.Deserialize<Input>(await Console.In.ReadLineAsync()
            ?? throw new InvalidOperationException("Missing child configuration."))!;
        var builder = new NpgsqlConnectionStringBuilder(input.Provider);
        Check(builder.Port != 5432 && string.IsNullOrEmpty(builder.Username) && string.IsNullOrEmpty(builder.Password),
            "Child requires isolated provider configuration without embedded credentials.");
        var layout = EventLogSqlLayout.ForBenchmark(input.Provider, input.Batch);
        await using var writer = new BinaryCopyEventLogAppender(input.Provider, true,
            new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy }, layout);
        Console.WriteLine("READY");
        Check(await Console.In.ReadLineAsync() == "GO", "Child requires explicit start.");
        string result;
        try
        {
            await writer.AppendAsync(Request(input.Stream, input.StreamId, input.EventNameId,
                input.Expected, input.CommandId));
            result = "committed";
        }
        catch (CommandAuditDuplicateException) { result = "duplicate"; }
        catch (ConcurrencyException) { result = "conflict"; }
        catch (NpgsqlException ex) { result = "database-failure"; Console.Error.WriteLine(ex.GetType().Name); }
        Console.WriteLine("RESULT:" + result);
        Check(await Console.In.ReadLineAsync() == "EXIT", "Child requires explicit terminal acknowledgment.");
    }

    internal static async Task ValidateContainer(string raw)
    {
        var connection = new NpgsqlConnectionStringBuilder(raw);
        Check(connection.Host == "127.0.0.1" && connection.Port != 5432 && connection.Database == "postgres",
            "Process/restart qualification requires the dedicated 127.0.0.1 admin endpoint on an explicit non-5432 port with database postgres.");
        var hostPort = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var expectedPublication = $"127.0.0.1:{hostPort}->5432/tcp";
        var ids = (await Docker("ps", "--no-trunc", "--format", "{{.ID}}\t{{.Ports}}"))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[1].Split(',', StringSplitOptions.TrimEntries)
                .Contains(expectedPublication, StringComparer.Ordinal))
            .Select(parts => parts[0])
            .ToArray();
        Check(ids.Length == 1,
            $"Expected exactly one running container publishing {expectedPublication}; found {ids.Length}.");
        using var parsed = JsonDocument.Parse(await Docker("inspect", ids[0]));
        var item = parsed.RootElement[0];
        Check(item.GetProperty("State").GetProperty("Running").GetBoolean() &&
            item.GetProperty("Config").GetProperty("Image").GetString() == "postgres:17.2" &&
            item.GetProperty("Config").GetProperty("Labels").GetProperty("ifm.purpose").GetString() == "eventlog-v2-benchmark",
            "Running container label/image does not match the owned fixture.");
        Check(!item.GetProperty("HostConfig").GetProperty("AutoRemove").GetBoolean(), "Container must retain its volume.");
        var binding = item.GetProperty("HostConfig").GetProperty("PortBindings").GetProperty("5432/tcp");
        Check(binding.GetArrayLength() == 1 && binding[0].GetProperty("HostIp").GetString() == "127.0.0.1" &&
            binding[0].GetProperty("HostPort").GetString() == hostPort,
            "Container 5432/tcp must have one 127.0.0.1 binding whose HostPort matches the connection port.");
        var mount = item.GetProperty("Mounts").EnumerateArray().Single(m =>
            m.GetProperty("Destination").GetString() == "/var/lib/postgresql/data");
        Check(mount.GetProperty("Type").GetString() == "volume" && mount.GetProperty("RW").GetBoolean(),
            "Requires a persistent Docker volume.");
        var id = item.GetProperty("Id").GetString()!;
        var source = mount.GetProperty("Source").GetString();
        var allIds = (await Docker("ps", "-aq")).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        using var containers = JsonDocument.Parse(await Docker(new[] { "inspect" }.Concat(allIds).ToArray()));
        foreach (var other in containers.RootElement.EnumerateArray())
            if (other.GetProperty("Id").GetString() != id)
                Check(!other.GetProperty("Mounts").EnumerateArray().Any(m => m.GetProperty("Source").GetString() == source),
                    "Benchmark volume is shared with another container; refusing server faults.");
        Check(_containerId.Length == 0 || _containerId == id, "Container identity changed during qualification.");
        _containerId = id;
    }

    internal static async Task RunCase(NpgsqlConnection db, string direct, string provider, bool batch,
        int eventNameId, string scenario, int repetition, string output)
    {
        var stream = $"ProcessQualification.{scenario}.{repetition}";
        var streamId = Convert.ToInt64(await Sql(db,
            "INSERT INTO event_stream_id(eventstream) VALUES($1) RETURNING eventstreamid", stream));
        var input = new Input(provider, stream, streamId, eventNameId, 0, Guid.NewGuid(), batch);
        var request = Request(stream, streamId, eventNameId, 0, input.CommandId);
        if (scenario is "process-version-race" or "process-duplicate")
        {
            var secondInput = scenario == "process-duplicate" ? input : input with { CommandId = Guid.NewGuid() };
            await using var first = await Child.Start(input);
            await using var second = await Child.Start(secondInput);
            Check(first.Id != second.Id && first.Id != Environment.ProcessId && second.Id != Environment.ProcessId,
                "Contenders must be separate OS processes.");
            await using var blocker = new NpgsqlConnection(direct);
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await Sql(blocker, "SELECT eventstreamid FROM event_stream_id WHERE eventstreamid=$1 FOR UPDATE", streamId);
            await first.Go(); await second.Go();
            try
            {
                await Until(async () => Convert.ToInt64(await Sql(db, """
                    SELECT count(*) FROM pg_stat_activity WHERE datname=current_database()
                    AND application_name IN ($1,$2) AND wait_event_type='Lock'
                    """, first.Tag, second.Tag)) == 2);
            }
            finally { await transaction.RollbackAsync(); }
            var results = await Task.WhenAll(first.Result(), second.Result());
            Check(results.Count(r => r == "committed") == 1 &&
                results.Count(r => r == (scenario == "process-duplicate" ? "duplicate" : "conflict")) == 1,
                "Unexpected cross-process contention result: " + string.Join(",", results));
            await Verify(db, request, 8, 1);
            await Expect(results[0] == "committed" ? input : secondInput, "duplicate");
            await Verify(db, request, 8, 1);
            return;
        }
        if (scenario == "graceful-restart")
        {
            await Expect(input, "committed");
            await Verify(db, request, 8, 1);
            await Restart(db, direct, false, output, batch, repetition);
            await Verify(db, request, 8, 1);
            await Expect(input, "duplicate");
            await Expect(input with { Expected = 8, CommandId = Guid.NewGuid() }, "committed");
            await Verify(db, request, 16, 2);
            return;
        }
        if (scenario == "server-crash")
        {
            // Commit a prefix before interrupting the next transaction; recovery must preserve only that prefix.
            await Expect(input, "committed");
            input = input with { Expected = 8, CommandId = Guid.NewGuid() };
            request = Request(stream, streamId, eventNameId, 8, input.CommandId);
            await ValidateContainer(Environment.GetEnvironmentVariable(EventLogV2Benchmark.AdminVariable)!);
            await OwnedDatabaseOnly(db);
        }
        await Sql(db, $"""
            CREATE OR REPLACE FUNCTION process_qualification_gate() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock({Gate}); RETURN NEW; END $$;
            CREATE TRIGGER process_qualification_gate BEFORE INSERT ON event_projector_state
            FOR EACH ROW EXECUTE FUNCTION process_qualification_gate()
            """);
        await using var child = await Child.Start(input);
        await using var gate = new NpgsqlConnection(direct);
        await gate.OpenAsync();
        await Sql(gate, $"SELECT pg_advisory_lock({Gate})");
        var released = false;
        try
        {
            await child.Go();
            await Until(async () => Convert.ToInt64(await Sql(db, """
                SELECT count(*) FROM pg_stat_activity WHERE datname=current_database()
                AND application_name=$1 AND wait_event_type='Lock' AND wait_event='advisory'
                """, child.Tag)) == 1);
            if (scenario == "process-death")
            {
                await child.Kill();
                Check(child.ExitCode != 0, "The writer did not exit abnormally.");
                await Sql(gate, $"SELECT pg_advisory_unlock({Gate})");
                released = true;
                await Until(async () => Convert.ToInt64(await Sql(db,
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND application_name=$1",
                    child.Tag)) == 0, 10);
                await Verify(db, request, 0, 0);
            }
            else
            {
                await Restart(db, direct, true, output, batch, repetition);
                released = true; // Server death released every session/advisory lock.
                Check(await child.Result() == "database-failure", "Interrupted child did not observe the server failure.");
                await Verify(db, request, 8, 1);
            }
        }
        finally
        {
            if (!released && gate.State == System.Data.ConnectionState.Open)
            {
                try { await Sql(gate, $"SELECT pg_advisory_unlock({Gate})"); }
                catch (NpgsqlException) { }
            }
            // On failure Child.Dispose kills the owned child; failed database remains for diagnosis.
        }
        await Sql(db, "DROP TRIGGER process_qualification_gate ON event_projector_state; DROP FUNCTION process_qualification_gate()");
        await Expect(input, "committed");
        await Expect(input, "duplicate");
        await Verify(db, request, scenario == "server-crash" ? 16 : 8, scenario == "server-crash" ? 2 : 1);
    }

    static async Task OwnedDatabaseOnly(NpgsqlConnection db)
    {
        Check(Convert.ToInt64(await Sql(db, """
            SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname<>'postgres'
            AND datname<>current_database()
            """)) == 0, "Another database is present; refusing container restart.");
    }

    static async Task Restart(NpgsqlConnection db, string direct, bool crash, string output, bool batch, int repetition)
    {
        // Crash caller has already validated before opening its timing-sensitive fault barrier.
        if (!crash)
        {
            await ValidateContainer(Environment.GetEnvironmentVariable(EventLogV2Benchmark.AdminVariable)!);
            await OwnedDatabaseOnly(db);
        }
        Check(_containerId.Length == 64, "No validated container identity.");
        var before = (DateTime)(await Sql(db, "SELECT pg_postmaster_start_time()"))!;
        var since = DateTime.UtcNow.ToString("O");
        await db.CloseAsync();
        if (crash)
        {
            await Docker("kill", "--signal=KILL", _containerId);
            using var state = JsonDocument.Parse(await Docker("inspect", _containerId));
            Check(state.RootElement[0].GetProperty("State").GetProperty("ExitCode").GetInt32() == 137,
                "Expected SIGKILL container exit.");
            await Docker("start", _containerId);
        }
        else await Docker("restart", "--time=10", _containerId);
        await Until(async () =>
        {
            try
            {
                await using var probe = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(direct)
                    { Timeout = 1, Pooling = false }.ConnectionString);
                await probe.OpenAsync();
                return Convert.ToInt32(await Sql(probe, "SELECT 1")) == 1;
            }
            catch (NpgsqlException) { return false; }
        }, 30);
        await db.OpenAsync();
        var after = (DateTime)(await Sql(db, "SELECT pg_postmaster_start_time()"))!;
        Check(after > before, "PostgreSQL postmaster did not restart.");
        Check((string)(await Sql(db,
            "SELECT current_setting('fsync')||'/'||current_setting('synchronous_commit')||'/'||current_setting('full_page_writes')"))!
            == "on/on/on", "Durability changed after restart.");
        var logs = await Docker("logs", "--since", since, _containerId);
        await File.WriteAllTextAsync(Path.Combine(output,
            $"{(batch ? "batched" : "baseline")}-{(crash ? "crash" : "restart")}-{repetition}.json"),
            JsonSerializer.Serialize(new { ContainerId = _containerId, Before = before, After = after, Crash = crash, Logs = logs },
                new JsonSerializerOptions { WriteIndented = true }));
        Check(!crash || logs.Contains("automatic recovery in progress", StringComparison.Ordinal),
            "Server logs did not confirm automatic crash recovery.");
    }

    internal static Task RestartOwnedPostgresGracefully(
        NpgsqlConnection db, string direct, string output, int repetition = 1)
        => Restart(db, direct, crash: false, output, batch: true, repetition);

    static async Task Expect(Input input, string expected)
    {
        await using var child = await Child.Start(input);
        await child.Go();
        var result = await child.Result();
        Check(result == expected, $"Fresh process expected {expected}, got {result}.");
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static async Task Until(Func<Task<bool>> predicate, int seconds = 2)
    {
        var timer = Stopwatch.StartNew();
        while (!await predicate())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(seconds)) throw new TimeoutException("Process/restart barrier timed out.");
            await Task.Delay(10);
        }
    }
    static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start docker.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        var text = await stdout; var error = await stderr;
        Check(process.ExitCode == 0, $"Docker {args[0]} failed: {error}");
        return text + (args[0] == "logs" ? error : "");
    }

    sealed class Child : IAsyncDisposable
    {
        readonly Process _process;
        readonly Task<string> _stderr;
        bool _finished;
        public string Tag { get; }
        public int Id => _process.Id;
        public int ExitCode => _process.ExitCode;
        Child(Process process, string tag) { _process = process; Tag = tag; _stderr = process.StandardError.ReadToEndAsync(); }
        public static async Task<Child> Start(Input input)
        {
            var tag = "ProcessQualification-" + Guid.NewGuid().ToString("N");
            var provider = new NpgsqlConnectionStringBuilder(input.Provider) { ApplicationName = tag, Pooling = false };
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--event-log-process-child");
            var child = new Child(Process.Start(start) ?? throw new InvalidOperationException("Cannot launch child."), tag);
            try
            {
                await child._process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(input with { Provider = provider.ConnectionString }));
                await child._process.StandardInput.FlushAsync();
                var ready = await child._process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
                if (ready != "READY")
                {
                    await child._process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    throw new InvalidOperationException($"Child failed its ready handshake: {ready}; {await child._stderr}");
                }
                return child;
            }
            catch { await child.DisposeAsync(); throw; }
        }
        public async Task Go()
        {
            await _process.StandardInput.WriteLineAsync("GO");
            await _process.StandardInput.FlushAsync();
        }
        public async Task<string> Result()
        {
            var result = await _process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Check(result?.StartsWith("RESULT:", StringComparison.Ordinal) == true, "Missing child terminal result.");
            await _process.StandardInput.WriteLineAsync("EXIT");
            await _process.StandardInput.FlushAsync();
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(_process.ExitCode == 0, "Child exit failure: " + await _stderr);
            _finished = true;
            return result![7..];
        }
        public async Task Kill()
        {
            Check(!_process.HasExited, "Child exited before the intended process-death fault.");
            _process.Kill(true);
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            _finished = true;
        }
        public async ValueTask DisposeAsync()
        {
            if (!_finished && !_process.HasExited) await Kill();
            _process.Dispose();
        }
    }
}
