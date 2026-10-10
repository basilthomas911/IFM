using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.Postgres.Benchmarks;

/// <summary>Three-minute eight-writer checkpoint stress with request latency samples; separate from BDN iteration statistics.</summary>
public static class PostgresSustainedWrites
{
    public static async Task RunAsync()
    {
        var profile=Environment.GetEnvironmentVariable("IFM_PG_TUNING_PROFILE") ?? "Baseline";
        var fixture=new PostgresTuningBenchmarks { Profile=profile };
        await fixture.Setup(); // Validates the isolated endpoint, server version and durability, then seeds the same dataset.
        var host=Environment.GetEnvironmentVariable("IFM_PG_TUNING_HOST") ?? "127.0.0.1";
        var builder=new NpgsqlConnectionStringBuilder { Host=host,Port=int.Parse(Environment.GetEnvironmentVariable("IFM_POSTGRES_BENCHMARK_PORT")!),Database="ifm_pg_benchmark",Username="benchmark",Password=Environment.GetEnvironmentVariable("IFM_POSTGRES_BENCHMARK_PASSWORD"),MaxPoolSize=16,CommandTimeout=30 };
        await using var source=NpgsqlDataSource.Create(builder.ConnectionString);
        await using var admin=await source.OpenConnectionAsync();
        using (var configure=new NpgsqlCommand("SHOW checkpoint_timeout",admin)) if ((string)(await configure.ExecuteScalarAsync())! != "30s") throw new InvalidOperationException("Checkpoint stress requires a verified 30-second interval.");

        using (var checkpoint=new NpgsqlCommand("CHECKPOINT",admin)) await checkpoint.ExecuteNonQueryAsync();
        var before=await Stats(admin);
        var duration=TimeSpan.FromSeconds(180);
        var timer=Stopwatch.StartNew();
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(async worker =>
        {
            await using var connection=await source.OpenConnectionAsync();
            await using var command=PostgresTuningBenchmarks.CreateAppend(connection,worker+1,32,Encoding.UTF8.GetBytes(new string('x',1024)));
            await command.PrepareAsync();
            var latencies=new List<double>();
            while(timer.Elapsed < duration)
            {
                command.Parameters[3].Value=Guid.NewGuid();
                var start=Stopwatch.GetTimestamp();
                var version=await command.ExecuteScalarAsync();
                if(version is not long) throw new InvalidOperationException("Append did not return a stream version.");
                var elapsed=Stopwatch.GetElapsedTime(start);
                latencies.Add(elapsed.TotalMilliseconds);
                // Bound data growth and offer 5,000 events/s across eight 32-event writers.
                var pacing=TimeSpan.FromMilliseconds(51.2)-elapsed;
                if(pacing>TimeSpan.Zero) await Task.Delay(pacing);
            }
            return latencies;
        }));
        timer.Stop();
        // Allow stats messages from other backends to arrive before reading a fresh snapshot.
        await Task.Delay(1500);
        using (var clear=new NpgsqlCommand("SELECT pg_stat_clear_snapshot()",admin)) await clear.ExecuteNonQueryAsync();
        var after=await Stats(admin);
        var values=results.SelectMany(x=>x).Order().ToArray();
        double Percentile(double p)=>values[(int)Math.Ceiling(p*values.Length)-1];
        var beforeDoc=JsonDocument.Parse(before);var afterDoc=JsonDocument.Parse(after);
        var timed=afterDoc.RootElement.GetProperty("checkpointer").GetProperty("num_timed").GetInt64()-beforeDoc.RootElement.GetProperty("checkpointer").GetProperty("num_timed").GetInt64();
        var requested=afterDoc.RootElement.GetProperty("checkpointer").GetProperty("num_requested").GetInt64()-beforeDoc.RootElement.GetProperty("checkpointer").GetProperty("num_requested").GetInt64();
        if(timed+requested < 2) throw new InvalidOperationException("Stress did not span at least two checkpoints.");
        var report=new { Profile=profile,DurationSeconds=timer.Elapsed.TotalSeconds,ConcurrentWriters=8,BatchSize=32,TargetEventsPerSecond=5000,Commits=values.Length,Events=values.Length*32L,EventsPerSecond=values.Length*32/timer.Elapsed.TotalSeconds,MeanBatchMilliseconds=values.Average(),P50BatchMilliseconds=Percentile(.5),P95BatchMilliseconds=Percentile(.95),P99BatchMilliseconds=Percentile(.99),TimedCheckpoints=timed,RequestedCheckpoints=requested,StatsBefore=JsonSerializer.Deserialize<JsonElement>(before),StatsAfter=JsonSerializer.Deserialize<JsonElement>(after) };
        var output=Environment.GetEnvironmentVariable("IFM_PG_TUNING_EVIDENCE") ?? ".";
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,profile+"-sustained.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        await fixture.Cleanup();
    }
    static async Task<string> Stats(NpgsqlConnection connection)
    {
        using var command=new NpgsqlCommand("SELECT json_build_object('wal',(SELECT row_to_json(w) FROM pg_stat_wal w),'checkpointer',(SELECT row_to_json(c) FROM pg_stat_checkpointer c),'database',(SELECT row_to_json(d) FROM pg_stat_database d WHERE datname=current_database()),'io',(SELECT json_agg(i) FROM pg_stat_io i))::text",connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}

