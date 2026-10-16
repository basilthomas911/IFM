using System.Diagnostics;
using System.Text.Json;

namespace TomasAI.IFM.Application.Storage.Scylla.Benchmarks;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="BenchmarkDotNet generates a separate assembly that invokes this public benchmark entry point.")]

/// <summary>Three-minute paced writes with two explicit memtable flushes and individual request latency samples.</summary>
public static class ScyllaSustainedWrites
{
    static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static async Task RunAsync()
    {
        var profile=Environment.GetEnvironmentVariable("IFM_SCYLLA_PROFILE")!;
        var root=Environment.GetEnvironmentVariable("IFM_SCYLLA_EVIDENCE")!;
        var fixture=new ScyllaVersionBenchmarks{Profile=profile};await (fixture.Setup()).ConfigureAwait(false);
        using var metrics=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:59145/"),Timeout=TimeSpan.FromSeconds(15)};
        await (File.WriteAllTextAsync(Path.Combine(root,"metrics-before.prom"),await (metrics.GetStringAsync(new Uri("metrics", UriKind.Relative))).ConfigureAwait(false))).ConfigureAwait(false);
        var timer=Stopwatch.StartNew();
        var flushes=new List<double>();
        var flushing=Task.Run(async()=>{for(var i=0;i<2;i++){await (Task.Delay(TimeSpan.FromSeconds(60))).ConfigureAwait(false);var start=Stopwatch.GetTimestamp();await (fixture.FlushAsync()).ConfigureAwait(false);flushes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);}});
        var samples=await (Task.WhenAll(Enumerable.Range(0,8).Select(async worker=>
        {
            var values=new List<double>();
            while(timer.Elapsed.TotalSeconds<180)
            {
                var start=Stopwatch.GetTimestamp();await (fixture.BatchAsync(32,11000+worker)).ConfigureAwait(false);var elapsed=Stopwatch.GetElapsedTime(start);values.Add(elapsed.TotalMilliseconds);
                var pacing=TimeSpan.FromMilliseconds(51.2)-elapsed;if(pacing>TimeSpan.Zero)await (Task.Delay(pacing)).ConfigureAwait(false);
            }
            return values;
        }))).ConfigureAwait(false);
        timer.Stop();await (flushing).ConfigureAwait(false);
        await (File.WriteAllTextAsync(Path.Combine(root,"metrics-after.prom"),await (metrics.GetStringAsync(new Uri("metrics", UriKind.Relative))).ConfigureAwait(false))).ConfigureAwait(false);
        var values=samples.SelectMany(x=>x).Order().ToArray();
        double Percentile(double p)=>values[(int)Math.Ceiling(p*values.Length)-1];
        var report=new{Profile=profile,DurationSeconds=timer.Elapsed.TotalSeconds,BatchSize=32,ConcurrentWriters=8,TargetEventsPerSecond=5000,Events=values.Length*32L,EventsPerSecond=values.Length*32/timer.Elapsed.TotalSeconds,MeanBatchMilliseconds=values.Average(),P50BatchMilliseconds=Percentile(.5),P95BatchMilliseconds=Percentile(.95),P99BatchMilliseconds=Percentile(.99),SuccessfulFlushes=flushes.Count,FlushMilliseconds=flushes};
        await (File.WriteAllTextAsync(Path.Combine(root,profile+"-sustained.json"),JsonSerializer.Serialize(report,IndentedJson))).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(report));await (fixture.Cleanup()).ConfigureAwait(false);
    }
}
