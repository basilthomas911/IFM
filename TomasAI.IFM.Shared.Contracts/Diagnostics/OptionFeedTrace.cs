using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.Diagnostics;

/// <summary>Correlated option lifecycle spans and timings, without logging market payloads.</summary>
public sealed class OptionFeedTrace : IDisposable
{
    public const string SourceName = "TomasAI.IFM.OptionFeed";
    static readonly ActivitySource Source = new(SourceName);
    static readonly AsyncLocal<string?> CurrentOperation = new();
    readonly string? previousOperation;
    readonly string operationId;
    readonly long started = Stopwatch.GetTimestamp();
    readonly Activity? activity;
    readonly ILogger? logger;
    readonly string stage;
    readonly string correlationId;
    readonly bool realtime;
    bool completed;
    bool disposed;
    Exception? failure;

    public OptionFeedTrace(string stage, ILogger? logger, string correlationId, bool realtime = false)
    {
        this.stage = stage;
        this.logger = logger;
        this.correlationId = correlationId;
        this.realtime = realtime;
        previousOperation = CurrentOperation.Value;
        activity = Source.StartActivity(stage);
        operationId = Activity.Current?.TraceId.ToString() ?? previousOperation ?? (realtime ? correlationId : Guid.NewGuid().ToString("N"));
        CurrentOperation.Value = operationId;
        activity?.SetTag("option_feed.correlation_id", correlationId);
        activity?.SetTag("option_feed.operation_id", operationId);
        var startLevel = realtime ? LogLevel.Debug : LogLevel.Information;
        if (logger?.IsEnabled(startLevel) == true) logger.Log(startLevel,
            "Option feed stage started; Stage={Stage}; CorrelationId={CorrelationId}; OperationId={OperationId}; TraceId={TraceId}",
            stage, correlationId, operationId, Activity.Current?.TraceId.ToString());
    }

    public void Complete() => completed = true;
    public void Fail(Exception error) => failure = error;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var outcome = completed ? "Succeeded" : failure is OperationCanceledException ? "Canceled" : failure is not null ? "Failed" : "Incomplete";
        activity?.SetTag("option_feed.outcome", outcome);
        activity?.SetTag("option_feed.elapsed_ms", elapsed);
        activity?.SetStatus(completed ? ActivityStatusCode.Ok : ActivityStatusCode.Error, failure?.Message);
        var level = !completed || elapsed >= 1000 ? LogLevel.Warning : realtime ? LogLevel.Debug : LogLevel.Information;
        if (logger?.IsEnabled(level) == true) logger.Log(level,
            failure, "Option feed stage finished; Stage={Stage}; CorrelationId={CorrelationId}; Outcome={Outcome}; ElapsedMilliseconds={ElapsedMilliseconds}; OperationId={OperationId}; TraceId={TraceId}",
            stage, correlationId, outcome, elapsed, operationId, Activity.Current?.TraceId.ToString());
        activity?.Dispose();
        CurrentOperation.Value = previousOperation;
    }
}
