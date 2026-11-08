using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Formats the actual compiled runtime entry/exit templates. Business logs and failures are outside sampling.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceLoggingBenchmarks
{
    [Params(false, true)] public bool InformationEnabled { get; set; }
    readonly ActorThreadId actor = new(ActorType.Query, "Performance", "ES");
    readonly ActorSubject subject = new(ActorType.Query, "Performance", "Read", "ES");
    readonly ActorInformationLoggingPolicy sampled = new ActorInformationLoggingOptions { SuccessfulQuerySampleEvery = 100 }.Compile();
    FormattingLogger logger = null!;
    [GlobalSetup] public void Setup() => logger = new(InformationEnabled);

    [Benchmark(Baseline = true), BenchmarkCategory("01-ActorSuccessLogging")]
    public void EverySuccessBefore()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        ActorMessageProcessingLog.Entry(logger, actor, "Read", subject, "ProcessMailboxAsync");
        ActorMessageProcessingLog.Exit(logger, actor, "Read", "Succeeded", started, subject, "ProcessMailboxAsync");
    }

    [Benchmark, BenchmarkCategory("01-ActorSuccessLogging")]
    public void SampledSuccessAfter()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!logger.IsEnabled(LogLevel.Information)) return;
        if (sampled.SelectRoutineInformation(actor, "Read"))
        {
            ActorMessageProcessingLog.Entry(logger, actor, "Read", subject, "ProcessMailboxAsync");
            ActorMessageProcessingLog.Exit(logger, actor, "Read", "Succeeded", started, subject, "ProcessMailboxAsync");
        }
        else sampled.RecordSuppressedSuccess(actor);
    }

    sealed class FormattingLogger(bool enabled) : ILogger
    {
        public long Bytes;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => enabled;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Bytes += System.Text.Encoding.UTF8.GetByteCount(formatter(state, exception));
    }
}
