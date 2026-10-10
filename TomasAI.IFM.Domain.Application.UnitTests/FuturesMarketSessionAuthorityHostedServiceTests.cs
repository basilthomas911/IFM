using TomasAI.IFM.Application.Api.Server.Core.MarketData.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.MarketData.Query;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class FuturesMarketSessionAuthorityHostedServiceTests
{
    [Fact]
    public async Task Host_shutdown_completes_the_reconciliation_worker_without_cancellation()
    {
        var service = new FuturesMarketSessionAuthorityHostedService(
            new FuturesMarketSessionAuthority(TimeProvider.System),
            TimeProvider.System,
            NullLogger<FuturesMarketSessionAuthorityHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(service.ExecuteTask);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(
            service.ExecuteTask.IsCompletedSuccessfully,
            $"Unexpected hosted-service completion state: {service.ExecuteTask.Status}.");
    }

    [Fact]
    public async Task Unexpected_reconciliation_failure_is_contained_without_faulting_the_host()
    {
        var timeProvider = new ThrowOnFourthReadTimeProvider();
        var service = new FuturesMarketSessionAuthorityHostedService(
            new FuturesMarketSessionAuthority(timeProvider),
            timeProvider,
            NullLogger<FuturesMarketSessionAuthorityHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_restores_completed_EOD_and_storage_failure_does_not_change_held_date(bool failRead)
    {
        var clock = new FixedClock();
        var authority = new FuturesMarketSessionAuthority(clock);
        if (failRead) authority.ApplyCompletedEndOfDay(new DateOnly(2026,10,6));
        var reads = new CompletionReads(failRead);
        var service = new FuturesMarketSessionAuthorityHostedService(authority, clock,
            NullLogger<FuturesMarketSessionAuthorityHostedService>.Instance, reads,
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" });
        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(new DateOnly(2026,10,7), authority.Current.OperationalValueDate);
            Assert.True(authority.Current.IsEndOfDayPending);
            Assert.Null(authority.Current.ActiveValueDate);
            Assert.Equal(1, reads.ReadCount);
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,10,8,16,0,0,TimeSpan.Zero);
    }
    private sealed class CompletionReads(bool failRead) : IScheduledTaskReadStore
    {
        public int ReadCount { get; private set; }
        public ValueTask<DateOnly?> GetCompletedEndOfDayAsync(string environment, CancellationToken cancellationToken = default)
        {
            Assert.Equal("Development", environment);
            ReadCount++;
            if (failRead) throw new InvalidOperationException("Injected completion read failure.");
            return ValueTask.FromResult<DateOnly?>(new DateOnly(2026,10,6));
        }
        public ValueTask<ScheduledTaskDefinition?> GetDefinitionAsync(string environment, string hostId, ScheduledTaskId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScheduledTaskDefinition[]> GetDefinitionsAsync(string environment, string hostId, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScheduledTaskCatalog?> GetCatalogAsync(string environment, string hostId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScheduledTaskRun?> GetRunAsync(string environment, string hostId, ScheduledTaskId scheduleId, ScheduledTaskId runId, DateTimeOffset intendedFireTimeUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string hostId, ScheduledTaskId scheduleId, int pageSize, byte[]? pagingState, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScheduledTaskRun[]> GetRunsAsync(string environment, string hostId, ScheduledTaskId scheduleId, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class ThrowOnFourthReadTimeProvider : TimeProvider
    {
        int reads;

        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Increment(ref reads) == 4)
                throw new InvalidOperationException("Injected clock failure.");
            return new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);
        }
    }
}
