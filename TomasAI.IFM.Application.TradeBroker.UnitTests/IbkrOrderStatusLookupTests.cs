using IBApi;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.OrderExecution;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class IbkrOrderStatusLookupTests
{
    private static readonly IbkrOrderIdentity Identity = new("TEST", 17, 21, 31, "exact-order");

    [Fact]
    public async Task Waits_for_all_downloads_and_matches_the_account_and_permanent_order()
    {
        var lookup = new IbkrOrderStatusLookup();
        var pending = lookup.CollectAsync(Identity, 41, static () => { }, CancellationToken.None);
        lookup.openOrder(21, new(), new() { Account = "OTHER", PermId = 31 }, new() { Status = "Submitted" });
        lookup.openOrderEnd();
        lookup.completedOrder(new(), new() { Account = "TEST", PermId = 31 }, new() { CompletedStatus = "Filled" });
        lookup.completedOrdersEnd();
        lookup.execDetailsEnd(99);
        Assert.False(pending.IsCompleted);

        var execution = new Execution { ExecId = "exec-one", AcctNumber = "TEST", PermId = 31, Shares = 1, Price = 10 };
        lookup.execDetails(41, new() { ConId = 77 }, execution);
        lookup.execDetails(41, new() { ConId = 77 }, execution);
        lookup.execDetailsEnd(41);

        var result = await pending;
        Assert.Equal(new IbkrOrderStatus("Filled", true), Assert.Single(result.Statuses));
        Assert.Equal("exec-one", Assert.Single(result.Executions).ExecutionId);
    }

    [Fact]
    public async Task Interrupted_download_requires_a_fresh_session_before_retry()
    {
        var lookup = new IbkrOrderStatusLookup();
        using var cancelled = new CancellationTokenSource();
        var pending = lookup.CollectAsync(Identity, 41, static () => { }, cancelled.Token);
        lookup.openOrderEnd();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => lookup.CollectAsync(Identity, 42, static () => { }, CancellationToken.None));
    }
}
