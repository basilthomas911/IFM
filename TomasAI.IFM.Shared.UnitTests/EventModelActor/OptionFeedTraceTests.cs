using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Diagnostics;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class OptionFeedTraceTests
{
    [Fact]
    public async Task Nested_async_stages_share_trace_and_log_operation_with_durations()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OptionFeedTrace.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => spans.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new Capture();
        using (var root = new OptionFeedTrace("subscribe", logger, "trade|contract"))
        {
            await Task.Yield();
            using (var child = new OptionFeedTrace("lease", logger, "lease-id")) child.Complete();
            root.Complete();
        }
        var mine = spans.Where(span => span.GetTagItem("option_feed.correlation_id") is "trade|contract" or "lease-id").ToArray();
        Assert.Equal(2, mine.Length);
        Assert.Equal(mine[0].TraceId, mine[1].TraceId);
        Assert.Equal(mine[1].SpanId, mine[0].ParentSpanId);
        Assert.All(mine, span => Assert.Equal(ActivityStatusCode.Ok, span.Status));
        Assert.Single(logger.Events.Select(row => row.Values["OperationId"]).Distinct());
        Assert.All(logger.Events.Where(row => row.Values.ContainsKey("ElapsedMilliseconds")), row =>
            Assert.True((double)row.Values["ElapsedMilliseconds"]! >= 0));
    }

    [Theory]
    [InlineData(false, "Failed")]
    [InlineData(true, "Canceled")]
    public void Failure_and_cancellation_are_visible_and_dispose_is_idempotent(bool cancel, string outcome)
    {
        var logger = new Capture();
        var step = new OptionFeedTrace("load", logger, "command", realtime: true);
        step.Fail(cancel ? new OperationCanceledException() : new InvalidOperationException("storage unavailable"));
        step.Dispose();
        step.Dispose();
        Assert.Equal(2, logger.Events.Count);
        Assert.Equal(LogLevel.Debug, logger.Events[0].Level);
        Assert.Equal(LogLevel.Warning, logger.Events[1].Level);
        Assert.Equal(outcome, logger.Events[1].Values["Outcome"]);
    }

    sealed class Capture : ILogger
    {
        public List<(LogLevel Level, Dictionary<string, object?> Values)> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Events.Add((logLevel, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
