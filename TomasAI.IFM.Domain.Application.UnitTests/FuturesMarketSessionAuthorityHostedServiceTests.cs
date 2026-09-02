using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;
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
        var timeProvider = new ThrowOnThirdReadTimeProvider();
        var service = new FuturesMarketSessionAuthorityHostedService(
            new FuturesMarketSessionAuthority(timeProvider),
            timeProvider,
            NullLogger<FuturesMarketSessionAuthorityHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    sealed class ThrowOnThirdReadTimeProvider : TimeProvider
    {
        int reads;

        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Increment(ref reads) == 3)
                throw new InvalidOperationException("Injected clock failure.");
            return new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);
        }
    }
}
