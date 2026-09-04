using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.Application.Actor.Event;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationStartupCommandDispatcherTests
{
    [Fact]
    public async Task Healthy_bootstrap_dispatches_exactly_once_after_application_started()
    {
        var lifetime = new TestLifetime();
        var statusStore = new ApplicationStartupStatusStore();
        var commandApi = new RecordingCommandApi
        {
            OnAccepted = (commandId, valueDate) => statusStore.Set(new()
            {
                State = ApplicationLifecycleState.Starting,
                ValueDate = valueDate,
                CommandId = commandId,
                StartedAtUtc = DateTime.UtcNow,
                Summary = "Application startup activities are executing."
            })
        };
        var dispatcher = Create(
            lifetime, new ConstantReadiness(true), commandApi, enabled: true, statusStore: statusStore);

        await dispatcher.StartAsync(CancellationToken.None);
        Assert.Equal(0, commandApi.Count);

        lifetime.SignalStarted();
        await commandApi.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        lifetime.SignalStarted();
        await Task.Delay(50);

        Assert.Equal(1, commandApi.Count);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Disabled_dispatcher_never_posts_a_command()
    {
        var lifetime = new TestLifetime();
        var commandApi = new RecordingCommandApi();
        var dispatcher = Create(lifetime, new ConstantReadiness(true), commandApi, enabled: false);

        await dispatcher.StartAsync(CancellationToken.None);
        lifetime.SignalStarted();
        await Task.Delay(50);

        Assert.Equal(0, commandApi.Count);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Dispatch_exception_is_reported_without_terminating_the_hosted_service()
    {
        var lifetime = new TestLifetime();
        var console = new TestConsole();
        var dispatcher = Create(
            lifetime,
            new ConstantReadiness(true),
            new ThrowingCommandApi(),
            enabled: true,
            statusConsole: console);

        await dispatcher.StartAsync(CancellationToken.None);
        lifetime.SignalStarted();

        Assert.Equal(10013, await console.ErrorCode.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Shutdown_before_application_started_completes_without_cancellation()
    {
        var dispatcher = Create(
            new TestLifetime(),
            new ConstantReadiness(true),
            new RecordingCommandApi(),
            enabled: true);

        await dispatcher.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await dispatcher.StopAsync(CancellationToken.None);
        await dispatcher.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(dispatcher.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Accepted_but_unobserved_handoff_retries_to_the_configured_limit()
    {
        var lifetime = new TestLifetime();
        var commandApi = new RecordingCommandApi();
        var handoffStore = new ApplicationStartupHandoffStatusStore();
        var dispatcher = Create(
            lifetime,
            new ConstantReadiness(true),
            commandApi,
            enabled: true,
            handoffStore: handoffStore,
            handoffMaximumAttempts: 2,
            handoffObservationTimeout: TimeSpan.FromMilliseconds(100));

        await dispatcher.StartAsync(CancellationToken.None);
        lifetime.SignalStarted();
        await dispatcher.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, commandApi.Count);
        Assert.Equal(ApplicationStartupHandoffState.TimedOut, handoffStore.Current.State);
        Assert.Equal(2, handoffStore.Current.AttemptCount);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Rejected_command_is_reported_and_is_not_retried()
    {
        var lifetime = new TestLifetime();
        var handoffStore = new ApplicationStartupHandoffStatusStore();
        var commandApi = new RejectedCommandApi();
        var dispatcher = Create(
            lifetime,
            new ConstantReadiness(true),
            commandApi,
            enabled: true,
            handoffStore: handoffStore,
            handoffMaximumAttempts: 3);

        await dispatcher.StartAsync(CancellationToken.None);
        lifetime.SignalStarted();
        await dispatcher.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, commandApi.Count);
        Assert.Equal(ApplicationStartupHandoffState.CommandRejected, handoffStore.Current.State);
        Assert.Equal(1, handoffStore.Current.AttemptCount);
        Assert.Contains("rejected", handoffStore.Current.Summary, StringComparison.OrdinalIgnoreCase);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stale_lifecycle_status_cannot_satisfy_a_new_command_handoff()
    {
        var lifetime = new TestLifetime();
        var valueDate = new DateOnly(2026, 9, 2);
        var startupStore = new ApplicationStartupStatusStore();
        startupStore.Set(new()
        {
            State = ApplicationLifecycleState.Running,
            ValueDate = valueDate,
            CommandId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4),
            Summary = "A previous startup completed."
        });
        var handoffStore = new ApplicationStartupHandoffStatusStore();
        var commandApi = new RecordingCommandApi();
        var dispatcher = Create(
            lifetime,
            new ConstantReadiness(true),
            commandApi,
            enabled: true,
            statusStore: startupStore,
            handoffStore: handoffStore,
            handoffObservationTimeout: TimeSpan.FromMilliseconds(100));

        await dispatcher.StartAsync(CancellationToken.None);
        lifetime.SignalStarted();
        await dispatcher.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, commandApi.Count);
        Assert.Equal(ApplicationStartupHandoffState.TimedOut, handoffStore.Current.State);
        Assert.NotEqual(startupStore.Current.CommandId, handoffStore.Current.CommandId);
        await dispatcher.StopAsync(CancellationToken.None);
    }

    static ApplicationStartupCommandDispatcher Create(
        IHostApplicationLifetime lifetime,
        IApplicationBootstrapReadiness readiness,
        IApplicationCommandApi commandApi,
        bool enabled,
        IStatusConsoleWriter? statusConsole = null,
        IApplicationStartupStatusStore? statusStore = null,
        IApplicationStartupHandoffStatusStore? handoffStore = null,
        int handoffMaximumAttempts = 1,
        TimeSpan? handoffObservationTimeout = null) => new(
            lifetime,
            readiness,
            new TestAuthority(),
            commandApi,
            statusStore ?? new ApplicationStartupStatusStore(),
            handoffStore ?? new ApplicationStartupHandoffStatusStore(),
            statusConsole ?? new TestConsole(),
            new ApplicationStartupOptions
            {
                AutoStartAfterBootstrap = enabled,
                BootstrapTimeout = TimeSpan.FromSeconds(2),
                ParticipantTimeout = TimeSpan.FromSeconds(2),
                HandoffObservationTimeout = handoffObservationTimeout ?? TimeSpan.FromMilliseconds(500),
                HandoffRetryDelay = TimeSpan.Zero,
                HandoffMaximumAttempts = handoffMaximumAttempts
            },
            TimeProvider.System,
            NullLogger<ApplicationStartupCommandDispatcher>.Instance);

    sealed class ConstantReadiness(bool healthy) : IApplicationBootstrapReadiness
    {
        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) => Task.FromResult(healthy);
    }

    sealed class RecordingCommandApi : IApplicationCommandApi
    {
        int count;
        public int Count => Volatile.Read(ref count);
        public TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<Guid, DateOnly>? OnAccepted { get; init; }

        public Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate)
        {
            Interlocked.Increment(ref count);
            var commandId = Guid.NewGuid();
            OnAccepted?.Invoke(commandId, valueDate);
            Accepted.TrySetResult();
            return Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId));
        }

        public Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate) => throw new NotSupportedException();
    }

    sealed class ThrowingCommandApi : IApplicationCommandApi
    {
        public Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate) =>
            throw new InvalidOperationException("NATS command transport unavailable.");

        public Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate) =>
            throw new NotSupportedException();
    }

    sealed class RejectedCommandApi : IApplicationCommandApi
    {
        int count;
        public int Count => Volatile.Read(ref count);

        public Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate)
        {
            Interlocked.Increment(ref count);
            return Task.FromResult(new ServiceResult<Guid>(409, "startup already rejected"));
        }

        public Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate) =>
            throw new NotSupportedException();
    }

    sealed class TestAuthority : IFuturesMarketSessionAuthority
    {
        public MarketSessionReadModel Current { get; } = new()
        {
            OperationalValueDate = new(2026, 9, 2),
            ActiveValueDate = new(2026, 9, 2),
            MarketTime = new(2026, 9, 2, 9, 0, 0),
            SessionStartUtc = new(2026, 9, 1, 22, 0, 0, DateTimeKind.Utc),
            SessionEndUtc = new(2026, 9, 2, 21, 0, 0, DateTimeKind.Utc),
            NextTransitionUtc = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc),
            Revision = 1,
            AsOfUtc = DateTime.UtcNow,
            State = FuturesMarketState.LiveTrading
        };
    }

    sealed class TestConsole : IStatusConsoleWriter
    {
        public TaskCompletionSource<int> ErrorCode { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteConsoleAsync(LogSourceType logSourceType, string statusMsg) => Task.CompletedTask;
        public Task WriteConsoleAsync(LogSourceType logSourceType, int errorCode, string errorMsg, string dataType = "", string data = "")
        {
            ErrorCode.TrySetResult(errorCode);
            return Task.CompletedTask;
        }
    }

    sealed class TestLifetime : IHostApplicationLifetime
    {
        readonly CancellationTokenSource started = new();
        readonly CancellationTokenSource stopping = new();
        readonly CancellationTokenSource stopped = new();
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        public void StopApplication() => stopping.Cancel();
        public void SignalStarted()
        {
            if (!started.IsCancellationRequested)
                started.Cancel();
        }
    }
}
