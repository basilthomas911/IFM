using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Verifies the strict orchestration contract separately from real-host qualification.</summary>
public sealed class ApiDatabentoRecoveryActionVerificationTests
{
    static readonly string[] Sequence =
    [
        nameof(IApiDatabentoRecoveryActions.CaptureRecoveryInputsAsync),
        nameof(IApiDatabentoRecoveryActions.FenceFailedGenerationAsync),
        nameof(IApiDatabentoRecoveryActions.AbandonPreviousCandidateAsync),
        nameof(IApiDatabentoRecoveryActions.StopDatabentoWorkersAsync),
        nameof(IApiDatabentoRecoveryActions.StartDatabentoWorkersAsync),
        nameof(IApiDatabentoRecoveryActions.QualifyDatabentoAsync),
        nameof(IApiDatabentoRecoveryActions.PrepareCandidateAsync),
        nameof(IApiDatabentoRecoveryActions.QualifyInfrastructureAsync),
        nameof(IApiDatabentoRecoveryActions.ReconcileActorsAsync),
        nameof(IApiDatabentoRecoveryActions.StartPublisherAsync),
        nameof(IApiDatabentoRecoveryActions.ProveDownstreamWritesAsync),
        nameof(IApiDatabentoRecoveryActions.AdmitGenerationAsync),
    ];

    public static IEnumerable<object[]> FailureCases() =>
        Sequence.SelectMany(action => new[] { new object[] { action, false }, new object[] { action, true } });

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Each_action_failure_or_timeout_stops_sequence_logs_exact_action_and_shuts_down(
        string failedAction, bool hang)
    {
        var actions = new RecordingActions { FailedAction = failedAction, Hang = hang };
        var log = new RecordingLogger();
        var pipeline = Create(actions, log);
        var request = Request();

        var result = await pipeline.HardResetRecoveryAsync(request).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, result.Outcome);
        Assert.Equal(failedAction, result.HardResult!.FailedStage);
        var prefix = Sequence.Take(Array.IndexOf(Sequence, failedAction) + 1).ToArray();
        Assert.Equal(prefix.Concat([nameof(actions.NotifySystemConsoleAsync), nameof(actions.ShutdownApiAsync)]),
            actions.Calls);
        Assert.Same(result, await pipeline.HardResetRecoveryAsync(Request()));
        Assert.Equal(prefix.Concat([nameof(actions.NotifySystemConsoleAsync), nameof(actions.ShutdownApiAsync)]),
            log.Entries.Where(entry => entry.Id == 17400).Select(entry => entry.Action));
        Assert.Equal(prefix.SkipLast(1).Concat([nameof(actions.NotifySystemConsoleAsync), nameof(actions.ShutdownApiAsync)]),
            log.Entries.Where(entry => entry.Id == 17401).Select(entry => entry.Action));
        var failure = Assert.Single(log.Entries, entry => entry.Id == 17402);
        Assert.Equal(failedAction, failure.Action);
        Assert.Equal(request.CorrelationId, failure.CorrelationId);
        Assert.NotNull(failure.Exception);
        Assert.Contains(hang ? "CanceledException" : "Injected " + failedAction, result.Detail);
        Assert.All(log.Entries.Where(entry => entry.Id == 17401), entry =>
            Assert.InRange(entry.ElapsedMs!.Value, 0, 5000));
        Assert.Same(result.HardResult, actions.ShutdownFailure);
        Assert.Equal(1, actions.Calls.Count(item => item == nameof(actions.ShutdownApiAsync)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Console_exception_or_noncooperative_write_cannot_prevent_shutdown(bool hang)
    {
        var actions = new RecordingActions
        {
            FailedAction = Sequence[0], ConsoleFailure = true, ConsoleHang = hang
        };
        var log = new RecordingLogger();
        var result = await Create(actions, log).HardResetRecoveryAsync(Request())
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, result.Outcome);
        Assert.Equal([Sequence[0], nameof(actions.NotifySystemConsoleAsync), nameof(actions.ShutdownApiAsync)], actions.Calls);
        Assert.Equal(nameof(actions.NotifySystemConsoleAsync),
            Assert.Single(log.Entries, entry => entry.Id == 17403).Action);
        Assert.NotNull(actions.ShutdownFailure);
    }

    [Fact]
    public async Task Success_logs_every_action_in_order_and_releases_gate()
    {
        var actions = new RecordingActions();
        var log = new RecordingLogger();
        var pipeline = Create(actions, log);
        var result = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, result.Outcome);
        Assert.Equal(Sequence, actions.Calls);
        Assert.Equal(Sequence.SelectMany(_ => new[] { 17400, 17401 }), log.Entries.Select(entry => entry.Id));
        Assert.Null(actions.ShutdownFailure);
        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy,
            (await pipeline.HardResetRecoveryAsync(Request())).Outcome);
        Assert.Equal(Sequence.Concat(Sequence), actions.Calls);
    }

    [Fact]
    public async Task Concurrent_requests_are_rejected_immediately_and_caller_cancellation_does_not_abandon_owner()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        var actions = new RecordingActions { Pause = async () => { entered.SetResult(); await release.Task; } };
        var pipeline = Create(actions, new RecordingLogger(), TimeSpan.FromSeconds(10));
        var request = Request();
        var owner = pipeline.HardResetRecoveryAsync(request, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            caller.Cancel();
            var contenders = await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(_ => pipeline.HardResetRecoveryAsync(Request()))).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.All(contenders, result =>
            {
                Assert.Equal(DatabentoRecoveryRequestOutcome.AlreadyInProgress, result.Outcome);
                Assert.Contains(request.CorrelationId.ToString("D"), result.Detail);
            });
            Assert.Equal([Sequence[0]], actions.Calls);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, (await owner).Outcome);
        Assert.Equal(Sequence, actions.Calls);
    }

    [Fact]
    public async Task Logging_sink_exception_does_not_skip_shutdown()
    {
        var actions = new RecordingActions { FailedAction = Sequence[0] };
        var result = await Create(actions, new RecordingLogger { Throw = true })
            .HardResetRecoveryAsync(Request());
        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, result.Outcome);
        Assert.NotNull(actions.ShutdownFailure);
    }

    static ApiDatabentoRecoveryPipeline Create(RecordingActions actions, RecordingLogger log, TimeSpan? timeout = null) =>
        new(actions, CancellationToken.None, new()
        {
            OverallTimeout = timeout ?? TimeSpan.FromMilliseconds(300),
            SupervisorTimeout = TimeSpan.FromMilliseconds(100),
            DownstreamTimeout = TimeSpan.FromMilliseconds(100),
            SystemConsoleTimeout = TimeSpan.FromMilliseconds(50)
        }, log);

    static DatabentoHardRecoveryRequest Request() =>
        new(Guid.NewGuid(), new DateOnly(2026, 10, 1), Guid.NewGuid(), "Verification", "Isolated injected fault");

    sealed class RecordingActions : IApiDatabentoRecoveryActions
    {
        public List<string> Calls { get; } = [];
        public string? FailedAction { get; init; }
        public bool Hang { get; init; }
        public bool ConsoleFailure { get; init; }
        public bool ConsoleHang { get; init; }
        public Func<Task>? Pause { get; init; }
        public DatabentoHardRecoveryResult? ShutdownFailure { get; private set; }

        async Task Run(ApiDatabentoRecoveryContext context, [CallerMemberName] string action = "")
        {
            Calls.Add(action);
            if (action == Sequence[0] && Pause is not null) await Pause();
            if (action == FailedAction)
            {
                if (Hang) await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
                throw new IOException("Injected " + action, new InvalidOperationException("Inner fault evidence"));
            }
            if (action == nameof(QualifyDatabentoAsync))
                context.HardResult = new(context.Request.CorrelationId, Guid.NewGuid(), 1,
                    DatabentoHardRecoveryOutcome.DatabentoHealthy, "", "Test local qualification");
        }

        public Task CaptureRecoveryInputsAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task FenceFailedGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task AbandonPreviousCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task StopDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task StartDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task QualifyDatabentoAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task PrepareCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task QualifyInfrastructureAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task ReconcileActorsAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task StartPublisherAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task ProveDownstreamWritesAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);
        public Task AdmitGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token) => Run(context);

        public Task NotifySystemConsoleAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure)
        {
            Calls.Add(nameof(NotifySystemConsoleAsync));
            if (ConsoleHang) return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            if (ConsoleFailure) throw new IOException("Injected console transport failure");
            return Task.CompletedTask;
        }

        public Task ShutdownApiAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure)
        {
            Calls.Add(nameof(ShutdownApiAsync));
            ShutdownFailure = failure;
            return Task.CompletedTask;
        }
    }

    sealed record LogEntry(int Id, string Action, Guid CorrelationId, double? ElapsedMs, Exception? Exception);

    sealed class RecordingLogger : ILogger<ApiDatabentoRecoveryPipeline>
    {
        public List<LogEntry> Entries { get; } = [];
        public bool Throw { get; init; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (Throw) throw new IOException("Injected logging sink failure");
            var properties = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary();
            Entries.Add(new(eventId.Id, (string)properties["Action"]!, (Guid)properties["CorrelationId"]!,
                properties.TryGetValue("ElapsedMs", out var elapsed) ? (double)elapsed! : null, exception));
        }
    }
}
