using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionRecoveryLiveVerificationTests
{
    [Fact]
    public async Task Injection_refreshes_a_raced_generation_without_restarting_the_countdown()
    {
        var market = Substitute.For<IMarketDataApi>();
        var before = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100,
            true, true, "Healthy", null, new DateOnly(2026, 10, 23));
        var after = before with { GenerationId = Guid.NewGuid(), ProcessId = 200 };
        var attempts = 0;
        market.InjectDevelopmentOptionWorkerFaultAsync(Arg.Any<MarketDataDevelopmentFaultRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (++attempts == 1) throw new InvalidOperationException("Fault target generation changed.");
                Assert.Equal(after.GenerationId, call.Arg<MarketDataDevelopmentFaultRequest>().GenerationId);
                return true;
            });
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, before.ValueDate, [after]));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        Assert.True(await OptionRecoveryLiveVerificationService.InjectCurrentTargetAsync(market, Guid.NewGuid(), before, timeout.Token));
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Injection_never_hides_an_unrelated_error_or_waits_past_its_original_budget(bool stale)
    {
        var market = Substitute.For<IMarketDataApi>();
        var target = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100,
            true, true, "Healthy", null, new DateOnly(2026, 10, 23));
        market.InjectDevelopmentOptionWorkerFaultAsync(Arg.Any<MarketDataDevelopmentFaultRequest>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException(stale ? "Fault target generation changed." : "Development verification disabled"));
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, target.ValueDate, [target]));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var work = OptionRecoveryLiveVerificationService.InjectCurrentTargetAsync(market, Guid.NewGuid(), target, timeout.Token);
        if (stale) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(2)));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => work);
    }

    [Fact]
    public async Task A_locked_report_cannot_abort_the_timer_and_reporting_recovers_after_the_reader_closes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ifm-option-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var operation = Guid.NewGuid(); var target = Path.Combine(directory, operation.ToString("D") + ".json");
        try
        {
            Assert.True(await OptionRecoveryLiveVerificationService.TryWriteReportAsync(directory, operation, "ArmedCountdown", new { }, CancellationToken.None));
            using (var reader = File.Open(target, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (OperatingSystem.IsWindows())
                    Assert.False(await OptionRecoveryLiveVerificationService.TryWriteReportAsync(directory, operation, "ArmedCountdown", new { }, CancellationToken.None));
            }
            Assert.True(await OptionRecoveryLiveVerificationService.TryWriteReportAsync(directory, operation, "Recovering", new { }, CancellationToken.None));
        }
        finally
        {
            File.Delete(target); File.Delete(target + ".tmp"); Directory.Delete(directory);
        }
    }
    [Fact]
    public async Task A_local_verification_file_failure_cannot_fault_the_API_host_or_inject_a_worker_error()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-option-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ".git"), "test root");
        File.WriteAllText(Path.Combine(root, ".artifacts"), "deliberately prevents directory creation");
        try
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Development); environment.ContentRootPath.Returns(root);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["MarketDataRecovery:DevelopmentVerification:Enabled"] = "true" }).Build();
            var market = Substitute.For<IMarketDataApi>(); var runtime = Substitute.For<IOptionPipelineRecoveryRuntime>();
            var coordinator = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance);
            using var service = new OptionRecoveryLiveVerificationService(environment, configuration, market, coordinator,
                new TradeRealtimeCacheRegistry(), NullLogger<OptionRecoveryLiveVerificationService>.Instance);
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
            Assert.Empty(market.ReceivedCalls()); Assert.Empty(runtime.ReceivedCalls());
        }
        finally
        {
            File.Delete(Path.Combine(root, ".git")); File.Delete(Path.Combine(root, ".artifacts")); Directory.Delete(root);
        }
    }
    [Theory]
    [InlineData("Happy", 300, true)]
    [InlineData("Deadline", 300, true)]
    [InlineData("Happy", 299, false)]
    [InlineData("Other", 300, false)]
    public void Arming_requires_a_valid_mode_and_full_five_minute_delay(string mode, int seconds, bool valid)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new OptionRecoveryVerificationRequest(Guid.NewGuid(), mode, now.AddMinutes(20), seconds);
        if (valid) OptionRecoveryLiveVerificationService.Validate(request, now);
        else Assert.Throws<InvalidDataException>(() => OptionRecoveryLiveVerificationService.Validate(request, now));
        Assert.Throws<InvalidDataException>(() => OptionRecoveryLiveVerificationService.Validate(request with { ExpiresAtUtc = now }, now));
        Assert.Throws<InvalidDataException>(() => OptionRecoveryLiveVerificationService.Validate(request with { OperationId = Guid.Empty }, now));
    }

    [Fact]
    public async Task Fixed_countdown_fires_at_300_seconds_without_any_health_observations()
    {
        var clock = new CountdownClock();
        var at299 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new OptionRecoveryVerificationRequest(Guid.NewGuid(), "Happy", clock.GetUtcNow().AddMinutes(20));
        var countdown = OptionRecoveryLiveVerificationService.WaitForInjectionAsync(request, clock, (elapsed, remaining) =>
        {
            if (elapsed.TotalSeconds >= 299) at299.TrySetResult();
            return Task.CompletedTask;
        }, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(299));
        await at299.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(countdown.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        await countdown.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Explicit_test_can_start_after_a_previous_failed_recovery()
    {
        var runtime = new Runtime { Failure = new InvalidOperationException("Already broken before this test") };
        var coordinator = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance,
            recoveryBudget: TimeSpan.FromMilliseconds(100));
        var inspection = new OptionPipelineInspection(DateOnly.FromDateTime(DateTime.UtcNow), "stale", ["position"]);
        coordinator.BeginDevelopmentVerification(inspection, Guid.NewGuid(), false, CancellationToken.None);
        await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal("Failed", coordinator.State);
        runtime.Failure = null;
        coordinator.BeginDevelopmentVerification(inspection, Guid.NewGuid(), false, CancellationToken.None);
        await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal("Healthy", coordinator.State);
        Assert.Null(coordinator.Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Development_injection_uses_real_recovery_and_deadline_not_a_manufactured_error(bool holdCompletion)
    {
        var clock = new LivePipelineIntegrationTests.ManualTime();
        var runtime = new Runtime();
        var coordinator = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance, clock);
        var operation = Guid.NewGuid();
        coordinator.BeginDevelopmentVerification(new(DateOnly.FromDateTime(DateTime.UtcNow), "Deliberate Development fault", ["test-position"]), operation, holdCompletion, CancellationToken.None);
        await runtime.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        if (holdCompletion)
        {
            clock.Advance(TimeSpan.FromSeconds(119));
            Assert.Equal("Recovering", coordinator.State); Assert.Null(coordinator.Failure);
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(holdCompletion ? "Failed" : "Healthy", coordinator.State);
        if (holdCompletion) Assert.Equal(operation, coordinator.Failure!.CommandId);
        else Assert.Null(coordinator.Failure);
    }

    sealed class Runtime : IOptionPipelineRecoveryRuntime
    {
        public Exception? Failure { get; set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public OptionPipelineInspection Inspect() => new(DateOnly.FromDateTime(DateTime.UtcNow), null, ["test-position"]);
        public Task RecoverAndVerifyAsync(Guid operationId, OptionPipelineInspection inspection, DateTimeOffset deadlineUtc, CancellationToken cancellationToken)
        { Called.TrySetResult(); return Failure is { } failure ? Task.FromException(failure) : Task.CompletedTask; }
    }

    sealed class CountdownClock : TimeProvider
    {
        readonly LivePipelineIntegrationTests.ManualTime inner = new();
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override long GetTimestamp() => GetUtcNow().UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => inner.CreateTimer(callback, state, dueTime, period);
        public void Advance(TimeSpan duration) => inner.Advance(duration);
    }
}
