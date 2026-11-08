using Microsoft.Extensions.Hosting;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.MarketData.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class BackendDiagnosticReadsTests
{
    [Fact]
    public async Task Simultaneous_health_consumers_share_one_refresh_without_sharing_waiter_cancellation()
    {
        var market = Substitute.For<IMarketDataApi>();
        var pending = new TaskCompletionSource<MarketDataBackendStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var reads = new BackendDiagnosticReads(market, Substitute.For<IHostApplicationLifetime>());
        using var cancellation = new CancellationTokenSource();
        var first = reads.StatusAsync(cancellation.Token);
        var waiters = Enumerable.Range(0, 32).Select(_ => reads.StatusAsync(default)).ToArray();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var generation = Guid.NewGuid();
        pending.SetResult(new(true, new(2026, 11, 7), [], generation));
        Assert.All(await Task.WhenAll(waiters), status => Assert.Equal(generation, status.HostInstanceId));
        await market.Received(1).GetBackendStatusAsync(Arg.Any<CancellationToken>());
        var replacement = Guid.NewGuid();
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(false, new(2026, 11, 7), [], replacement));
        var next = await reads.StatusAsync(default);
        Assert.False(next.Ready); Assert.Equal(replacement, next.HostInstanceId);
        await market.Received(2).GetBackendStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_refresh_is_not_cached_and_independent_recovery_calls_are_not_intercepted()
    {
        var market = Substitute.For<IMarketDataApi>();
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<MarketDataBackendStatus>(new IOException("fixture")));
        var reads = new BackendDiagnosticReads(market, Substitute.For<IHostApplicationLifetime>());
        await Assert.ThrowsAsync<IOException>(() => reads.StatusAsync(default));
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, new(2026, 11, 7), []));
        Assert.True((await reads.StatusAsync(default)).Ready);
        Assert.True((await market.GetBackendStatusAsync(default)).Ready);
        await market.Received(3).GetBackendStatusAsync(Arg.Any<CancellationToken>());
    }
}
