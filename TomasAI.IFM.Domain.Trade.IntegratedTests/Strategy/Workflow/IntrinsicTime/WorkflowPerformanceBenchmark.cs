using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact, Trait("Category", "WorkflowPerformanceBenchmark")]
    public async Task Workflow_benchmark_measures_repeated_real_service_authorizations()
    {
        if (Environment.GetEnvironmentVariable("IFM_WORKFLOW_BENCHMARK") != "1")
        {
            output.WriteLine("Workflow benchmark is opt-in: set IFM_WORKFLOW_BENCHMARK=1. No benchmark was run.");
            return;
        }
        var settings = WorkflowBenchmarkSettings.Read();
        using var writer = new WorkflowBenchmarkWriter(settings);
        await RunWorkflow(settings.Scenarios[0].Horizon, settings.Scenarios[0].Variant,
            captureTrace: settings.CaptureTrace, benchmark: settings, benchmarkWriter: writer);
    }

    sealed record WorkflowBenchmarkScenario(TimeFrameType Horizon, string Variant)
    {
        public override string ToString() => $"{Horizon}/{Variant}";
    }

    sealed record WorkflowBenchmarkIteration(WorkflowBenchmarkScenario Scenario, string Phase, int Iteration);

    sealed record WorkflowBenchmarkSettings(int Warmups, int Samples, WorkflowBenchmarkScenario[] Scenarios,
        string Label, string OutputPath, bool CaptureTrace)
    {
        public static readonly WorkflowBenchmarkScenario[] SupportedScenarios =
        [new(TimeFrameType.Daily, "LongFuture"), new(TimeFrameType.Weekly, "ShortFuture"),
            new(TimeFrameType.Monthly, "BullCallDebit"), new(TimeFrameType.Daily, "BearPutDebit"),
            new(TimeFrameType.Weekly, "ShortBalancedIronCondor")];

        public static WorkflowBenchmarkSettings Read()
        {
            const string prefix = "IFM_WORKFLOW_BENCHMARK_";
            static int Number(string name, int fallback, int minimum)
            {
                var value = Environment.GetEnvironmentVariable(prefix + name);
                if (value is null) return fallback;
                if (!int.TryParse(value, out var count) || count < minimum)
                    throw new InvalidOperationException($"{prefix}{name} must be an integer >= {minimum}.");
                return count;
            }
            var requested = (Environment.GetEnvironmentVariable(prefix + "SCENARIOS") ?? "Daily/LongFuture")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var scenarios = requested.Select(name => SupportedScenarios.SingleOrDefault(x =>
                string.Equals(x.ToString(), name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unsupported benchmark scenario '{name}'.")).ToArray();
            if (scenarios.Length == 0 || scenarios.Distinct().Count() != scenarios.Length)
                throw new InvalidOperationException("Select at least one scenario, without duplicates.");
            return new(Number("WARMUPS", 3, 0), Number("SAMPLES", 30, 1), scenarios,
                Environment.GetEnvironmentVariable(prefix + "MODE") ?? "baseline",
                Path.GetFullPath(Environment.GetEnvironmentVariable(prefix + "OUTPUT")
                    ?? Path.Combine("TestResults", "workflow-performance", $"workflow-{DateTime.UtcNow:yyyyMMddTHHmmssfff}.jsonl")),
                Environment.GetEnvironmentVariable(prefix + "TRACE") == "1");
        }

        public IEnumerable<WorkflowBenchmarkIteration> Iterations()
        {
            // Host startup and fixture/schema creation are deliberately outside all workflow timers.
            yield return new(Scenarios[0], "cold", 0);
            for (var i = 1; i <= Warmups; i++)
                foreach (var scenario in Scenarios) yield return new(scenario, "warmup", i);
            for (var i = 1; i <= Samples; i++)
                foreach (var scenario in Scenarios) yield return new(scenario, CaptureTrace ? "trace" : "warm", i);
        }

        public static void ValidateEnvironment(IWebHostEnvironment environment, IConfiguration config, string broker)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("The workflow benchmark requires the Development environment.");
            if (!Uri.TryCreate(broker, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Port == 4222)
                throw new InvalidOperationException("The workflow benchmark requires an isolated loopback NATS broker on a non-default port.");
            foreach (var item in config.GetSection("ConnectionStrings").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(item.Value)) continue;
                ValidateConnection(item.Key, item.Value);
            }
            if (config["IFM_TEST_MARKET_DATA_CONNECTION"] is { Length: > 0 } market)
                ValidateConnection("IFM_TEST_MARKET_DATA_CONNECTION", market);
            var events = new DbConnectionStringBuilder { ConnectionString = config.GetConnectionString("EventSourceActorDbConnection") };
            if (!string.Equals(Convert.ToString(events["Database"]), "event-source-test-db", StringComparison.Ordinal)
                || (events.ContainsKey("Port") && Convert.ToString(events["Port"]) != "5432"))
                throw new InvalidOperationException("Workflow fixture funding requires localhost:5432/event-source-test-db.");
        }

        static void ValidateConnection(string name, string connection)
        {
            var values = new DbConnectionStringBuilder { ConnectionString = connection };
            var host = values.ContainsKey("Host") ? Convert.ToString(values["Host"])
                : values.ContainsKey("Contact Points") ? Convert.ToString(values["Contact Points"]) : null;
            if (host is null || host.Split(',').Any(x => !Uri.TryCreate($"http://{x.Trim()}", UriKind.Absolute, out var uri) || !uri.IsLoopback))
                throw new InvalidOperationException($"Benchmark connection '{name}' must use loopback hosts.");
            var databaseName = values.ContainsKey("Database") ? Convert.ToString(values["Database"])
                : values.ContainsKey("Default Keyspace") ? Convert.ToString(values["Default Keyspace"]) : null;
            if (databaseName?.Contains("test", StringComparison.OrdinalIgnoreCase) != true)
                throw new InvalidOperationException($"Benchmark connection '{name}' must identify a test database/keyspace.");
        }
    }

    sealed class WorkflowBenchmarkWriter : IDisposable
    {
        readonly StreamWriter writer;
        public string RunId { get; } = Guid.NewGuid().ToString("N");

        public WorkflowBenchmarkWriter(WorkflowBenchmarkSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settings.OutputPath)!);
            // CreateNew prevents accidental mixing of incompatible binaries/batches in one report.
            writer = new(new FileStream(settings.OutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            var assemblies = new[] { typeof(TradeSelectionRuntimeTests).Assembly,
                typeof(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.WorkflowTrace).Assembly,
                typeof(TomasAI.IFM.Domain.Portfolio.Operations.PortfolioTelemetry).Assembly,
                typeof(TomasAI.IFM.Application.Storage.IDbContextFactory).Assembly }
                .Distinct().Select(assembly => new { Name = assembly.GetName().Name, Path = assembly.Location,
                    Version = assembly.GetName().Version?.ToString(),
                    BuildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
                    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) }).ToArray();
            Write(new { RecordType = "metadata", RunId, settings.Label,
                Commit = Environment.GetEnvironmentVariable("IFM_WORKFLOW_BENCHMARK_COMMIT") ?? GitCommit(),
                BuildConfiguration = typeof(TradeSelectionRuntimeTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
                Machine = Environment.MachineName, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount, ProcessId = Environment.ProcessId,
                ServerGC = System.Runtime.GCSettings.IsServerGC, GCLatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(),
                StartedAtUtc = DateTime.UtcNow, settings.Warmups, settings.Samples,
                Scenarios = settings.Scenarios.Select(x => x.ToString()), settings.CaptureTrace,
                Assemblies = assemblies, QueryVisiblePollingMilliseconds = WorkflowMeasurement.VisibilityPollMilliseconds,
                ColdDefinition = "First workflow in one host after startup, schema creation, numerical fixture setup, and funding; not process startup latency.",
                Conditions = "Sequential workflows; one production Portfolio/Trade actor host; real local test stores and isolated NATS; unique equivalent fixture/funding before each timer; no global database reset; full projection forwarding; no latency qualification threshold." });
        }

        public void Write(object value) => writer.WriteLine(JsonSerializer.Serialize(value));
        public void Dispose() => writer.Dispose();
        static string? GitCommit()
        {
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
                    { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
                var result = git?.StandardOutput.ReadToEnd().Trim();
                if (git is not null && git.WaitForExit(5000) && git.ExitCode == 0) return result;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            return null;
        }
    }

    sealed record WorkflowStageMeasurement(int StageCount, string Endpoint, double Milliseconds, long WorkflowRevision);

    sealed class WorkflowMeasurement(int stageCount)
    {
        public const int VisibilityPollMilliseconds = 10;
        static readonly string[] Endpoints = ["Regime Discovery", "Market Assessment", "Trade Selection", "Order Composer", "Risk Manager"];
        readonly object sync = new();
        readonly WorkflowStageMeasurement?[] stages = new WorkflowStageMeasurement?[5];
        readonly TaskCompletionSource<IntrinsicTimeStrategyWorkflowView> committed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly Stopwatch timer = new();
        WorkflowProcessMetrics? startedMetrics;
        public string SampleId { get; } = Guid.NewGuid().ToString("N");
        public DateTime StartedAtUtc { get; private set; }
        public double? AuthorizedMilliseconds { get; private set; }
        public double? EndpointMilliseconds { get; private set; }
        public double? QueryVisibleMilliseconds { get; private set; }
        public WorkflowProcessMetrics? ProcessMetrics { get; private set; }
        public bool ObservedActive { get; private set; }
        public double ElapsedMilliseconds => timer.Elapsed.TotalMilliseconds;
        public IReadOnlyList<WorkflowStageMeasurement> Stages { get { lock (sync) return stages.OfType<WorkflowStageMeasurement>().ToArray(); } }

        public void Start()
        {
            startedMetrics = WorkflowProcessMetrics.Capture();
            StartedAtUtc = DateTime.UtcNow;
            timer.Start();
        }

        public bool OnCommitted(IntrinsicTimeStrategyWorkflowView view)
        {
            lock (sync)
            {
                var elapsed = timer.Elapsed.TotalMilliseconds;
                var pipeline = PipelineStages(view);
                for (var index = 0; index < pipeline.Length; index++)
                    if (stages[index] is null && pipeline[index].ProcessingStatus == StrategyActorProcessingStatus.Completed)
                        stages[index] = new(index + 1, Endpoints[index], elapsed, view.WorkflowRevision);
                if (stageCount < 5 && pipeline[stageCount - 1].ProcessingStatus == StrategyActorProcessingStatus.Completed)
                {
                    EndpointMilliseconds ??= elapsed;
                    committed.TrySetResult(view);
                    return true; // Only staggered endpoint tests suppress the following stage.
                }
                if (view.FinancialHandoff?.Phase == RiskFinancialHandoffPhase.Authorized)
                {
                    AuthorizedMilliseconds ??= elapsed;
                    EndpointMilliseconds ??= elapsed;
                }
                if (view.TerminalAtUtc is not null)
                    committed.TrySetResult(view); // Failed terminal outcomes are verified and reported immediately too.
                return false;
            }
        }

        public async Task<IntrinsicTimeStrategyWorkflowView> WaitAsync(string entity, CancellationToken cancellationToken)
        {
            var cache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
            // Observe from workflow start, including startup projection. Removal alone is ambiguous before first insertion.
            while (!committed.Task.IsCompleted)
            {
                ObservedActive |= cache.TryGet(entity, out _);
                await Task.WhenAny(committed.Task, Task.Delay(VisibilityPollMilliseconds, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            var final = await committed.Task;
            if (stageCount == 5)
            {
                while (true)
                {
                    var active = cache.TryGet(entity, out _);
                    ObservedActive |= active;
                    if (ObservedActive && !active) break;
                    await Task.Delay(VisibilityPollMilliseconds, cancellationToken);
                }
                QueryVisibleMilliseconds = timer.Elapsed.TotalMilliseconds;
            }
            timer.Stop();
            ProcessMetrics = WorkflowProcessMetrics.Capture().Since(startedMetrics!);
            return final;
        }
    }

    sealed record WorkflowProcessMetrics(long AllocatedBytes, double CpuMilliseconds, int Gen0Collections, int Gen1Collections,
        int Gen2Collections, long WorkingSetBytes, long PrivateMemoryBytes, long ManagedHeapBytes)
    {
        public static WorkflowProcessMetrics Capture()
        {
            using var process = Process.GetCurrentProcess();
            return new(GC.GetTotalAllocatedBytes(false), process.TotalProcessorTime.TotalMilliseconds,
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                process.WorkingSet64, process.PrivateMemorySize64, GC.GetTotalMemory(false));
        }
        public WorkflowProcessMetrics Since(WorkflowProcessMetrics start) => this with
        {
            AllocatedBytes = AllocatedBytes - start.AllocatedBytes, CpuMilliseconds = CpuMilliseconds - start.CpuMilliseconds,
            Gen0Collections = Gen0Collections - start.Gen0Collections, Gen1Collections = Gen1Collections - start.Gen1Collections,
            Gen2Collections = Gen2Collections - start.Gen2Collections
        };
    }
}
