using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
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
        var commandApi = new RecordingCommandApi();
        var dispatcher = Create(lifetime, new ConstantReadiness(true), commandApi, enabled: true);

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

    static ApplicationStartupCommandDispatcher Create(
        IHostApplicationLifetime lifetime,
        IApplicationBootstrapReadiness readiness,
        IApplicationCommandApi commandApi,
        bool enabled,
        IStatusConsoleWriter? statusConsole = null) => new(
            lifetime,
            readiness,
            new TestAuthority(),
            commandApi,
            statusConsole ?? new TestConsole(),
            new ApplicationStartupOptions
            {
                AutoStartAfterBootstrap = enabled,
                BootstrapTimeout = TimeSpan.FromSeconds(2),
                ParticipantTimeout = TimeSpan.FromSeconds(2)
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

        public Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate)
        {
            Interlocked.Increment(ref count);
            Accepted.TrySetResult();
            return Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid()));
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
