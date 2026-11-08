using BenchmarkDotNet.Attributes;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Producer latency for 32 events; asynchronous delivery is verified outside timing and is not equal-delivery throughput.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceConsoleBenchmarks
{
    [Params(0, 1)] public int ConsumerDelayMilliseconds { get; set; }
    Destination destination = null!;
    BoundedConsoleLogSink buffered = null!;
    readonly LogEvent message = new(DateTimeOffset.UnixEpoch, LogEventLevel.Information, null,
        new MessageTemplateParser().Parse("Actor request completed"), []);

    [IterationSetup]
    public void Prepare()
    {
        destination = new(ConsumerDelayMilliseconds);
        buffered = new(destination, capacity: 32);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("02-ConsoleProducerLatency")]
    public void DirectBefore() { for (var index = 0; index < 32; index++) destination.Emit(message); }

    [Benchmark, BenchmarkCategory("02-ConsoleProducerLatency")]
    public void BufferedAfter() { for (var index = 0; index < 32; index++) buffered.Emit(message); }

    [IterationCleanup]
    public void DrainAndVerify()
    {
        buffered.Dispose();
        if (destination.Delivered != 32 || buffered.DroppedRoutine != 0 || buffered.Failed != 0)
            throw new InvalidOperationException("Console fixture lost an accepted event.");
    }

    sealed class Destination(int delay) : ILogEventSink
    {
        public int Delivered;
        public void Emit(LogEvent logEvent)
        {
            if (delay != 0) Thread.Sleep(delay);
            Interlocked.Increment(ref Delivered);
        }
    }
}
