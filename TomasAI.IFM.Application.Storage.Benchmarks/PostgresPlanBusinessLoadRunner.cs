using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures sustained overlap of real ledger postings and atomic plan snapshots without queuing missed ticks.</summary>
internal static class PostgresPlanBusinessLoadRunner
{
    public static async Task RunAsync(string[] args)
    {
        int Read(string name, int fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 ? int.Parse(args[index + 1], CultureInfo.InvariantCulture) : fallback;
        }
        var seconds = Read("--seconds", 1800);
        var writers = Read("--writers", 4);
        var planRate = Read("--plan-rate", 5);
        var businessRate = Read("--business-rate", 1);
        if (seconds < 1 || writers is < 1 or > 32 || planRate < 0 || businessRate < 1)
            throw new ArgumentException("Positive duration, 1-32 writers, nonnegative plan rate and positive business rate required.");
        var modesIndex = Array.IndexOf(args, "--modes");
        var modes = modesIndex >= 0 ? args[modesIndex + 1].Split(',') : ["BusinessOnly", "PlansAndBusiness"];
        if (modes.Any(mode => mode is not ("BusinessOnly" or "PlansOnly" or "PlansAndBusiness" or "MixedWithReads")))
            throw new ArgumentException("Unknown sustained workload mode.");
        var readRate = Read("--read-rate", 2);
        if (readRate < 1) throw new ArgumentException("Read rate must be positive.");
        var results = new List<object>();
        foreach (var mode in modes)
        {
            var business = new List<PostgresPlanBusinessBenchmarks>();
            var plans = new List<PostgresPlanBusinessBenchmarks>();
            for (var i = 0; i < writers; i++)
            {
                if (mode != "PlansOnly")
                {
                    var ledger = new PostgresPlanBusinessBenchmarks(); await ledger.Setup(); business.Add(ledger);
                }
                if (mode != "BusinessOnly" && planRate > 0)
                {
                    var plan = new PostgresPlanBusinessBenchmarks(); await plan.Setup(); plans.Add(plan);
                }
            }
            var ledgerStats = new Measurements(); var planStats = new Measurements(); var readStats = new Measurements();
            var allocatedBefore = GC.GetTotalAllocatedBytes();
            using var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime;
            var started = Stopwatch.GetTimestamp();
            var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var tasks = business.Select(worker => RunWorker(worker.BusinessLedgerPosting, businessRate, ledgerStats, seconds)).ToList();
            tasks.AddRange(plans.Select(worker => RunWorker(worker.PlanWrite, planRate, planStats, seconds)));
            if (mode == "MixedWithReads")
                tasks.AddRange(plans.Select(worker => RunWorker(worker.ReadPlanLatestAndHistory, readRate, readStats, seconds)));
            var completion = Task.WhenAll(tasks);
            Console.WriteLine($"{mode}: started {business.Count} business writers and {plans.Count} plan writers for {seconds}s");
            while (!completion.IsCompleted)
            {
                await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(30)));
                Console.WriteLine($"{mode}: elapsed={Stopwatch.GetElapsedTime(started).TotalSeconds:F0}s business={ledgerStats.Completed} plans={planStats.Completed} reads={readStats.Completed} errors={ledgerStats.Errors + planStats.Errors + readStats.Errors}");
            }
            await completion;
            results.Add(new { Mode = mode, Seconds = Stopwatch.GetElapsedTime(started).TotalSeconds, Writers = writers,
                PlanRatePerWriter = plans.Count > 0 ? planRate : 0, BusinessRatePerWriter = business.Count > 0 ? businessRate : 0,
                ReadRatePerWriter = mode == "MixedWithReads" ? readRate : 0,
                Business = ledgerStats.Summary(), Plans = planStats.Summary(), Reads = readStats.Summary(),
                AllocatedBytes = GC.GetTotalAllocatedBytes() - allocatedBefore,
                ProcessCpuSeconds = (process.TotalProcessorTime - cpuBefore).TotalSeconds,
                ProcessWorkingSetBytes = process.WorkingSet64,
                QueuePolicy = "No backlog; late schedule slots are dropped and counted",
                GcCollections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray() });
            var resultPath = Path.Combine(".artifacts", "postgres-plan-business-load-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            Directory.CreateDirectory(".artifacts");
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                Profile = Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_PROFILE") ?? "AtomicDurable",
                Database = Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_CONNECTION") is null ? "ifm_storage_refactor_test" : "ifm_durability_benchmark", Workload = "Initial rates; not measured production peak",
                LatencyHistogramRelativeResolution = 0.01, Runtime = Environment.Version.ToString(),
                Results = results
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Results: " + resultPath);
        }
    }

    static async Task RunWorker(Func<Task> operation, int rate, Measurements measurements, int seconds)
    {
        var started = Stopwatch.GetTimestamp();
        var period = TimeSpan.FromSeconds(1d / rate);
        var next = TimeSpan.Zero;
        while (Stopwatch.GetElapsedTime(started).TotalSeconds < seconds)
        {
            var wait = next - Stopwatch.GetElapsedTime(started);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            if (Stopwatch.GetElapsedTime(started).TotalSeconds >= seconds) break;
            var operationStarted = Stopwatch.GetTimestamp();
            try
            {
                await operation();
                measurements.Record(Stopwatch.GetElapsedTime(operationStarted).TotalMilliseconds);
            }
            catch (Exception error)
            {
                Interlocked.Increment(ref measurements.Errors);
                Console.WriteLine($"{operation.Method.Name} failed: {error}");
            }
            next += period;
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (next < elapsed)
            {
                var missed = (long)((elapsed - next).Ticks / period.Ticks) + 1;
                Interlocked.Add(ref measurements.MissedScheduleSlots, missed);
                next += missed * period;
            }
        }
    }

    sealed class Measurements
    {
        readonly long[] histogram = new long[8192];
        public long Completed, Errors, MissedScheduleSlots;
        public void Record(double milliseconds)
        {
            var bucket = Math.Clamp((int)Math.Ceiling(Math.Log(Math.Max(milliseconds, 0.001) / 0.001, 1.01)), 0, histogram.Length - 1);
            Interlocked.Increment(ref histogram[bucket]); Interlocked.Increment(ref Completed);
        }
        double Percentile(double percentile)
        {
            var target = (long)Math.Ceiling(Completed * percentile); if (target == 0) return 0;
            long seen = 0;
            for (var i = 0; i < histogram.Length; i++) if ((seen += histogram[i]) >= target) return 0.001 * Math.Pow(1.01, i);
            return 0;
        }
        public object Summary() => new { Completed, Errors, MissedScheduleSlots, P50Milliseconds = Percentile(0.50), P95Milliseconds = Percentile(0.95), P99Milliseconds = Percentile(0.99) };
    }
}
