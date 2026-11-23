using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OpenPositionEndOfDaySweepTests
{
    [Theory]
    [InlineData("2026-11-17T21:59:59Z", "2026-11-16")]
    [InlineData("2026-11-17T22:00:00Z", "2026-11-17")]
    [InlineData("2026-11-17T23:00:00Z", "2026-11-17")]
    [InlineData("2026-11-23T14:00:00Z", "2026-11-20")]
    public void Completed_session_selection_respects_close_rollover_and_weekends(string now, string expected)
    {
        var sweep = new OpenPositionEndOfDaySweep(Substitute.For<IOpenPositionEndOfDayStore>(),
            Substitute.For<IStrategyPositionCommandApi>(), new CmeFuturesMarketSessionCalendar(), new Clock(DateTimeOffset.Parse(now)), new());
        Assert.Equal(DateOnly.Parse(expected), sweep.LastCompletedValueDate());
    }

    [Fact]
    public async Task Concurrent_close_is_skipped_without_sending_an_EOD_command()
    {
        var store = Substitute.For<IOpenPositionEndOfDayStore>();
        var id = StrategyPositionId.Create(new(1, 2, 3, 4), TradeStrategyKind.IronCondor);
        var owner = new StrategyPositionSnapshot { Id = id, IsOpen = true };
        store.ReadOpenAsync(null, 64, Arg.Any<CancellationToken>()).Returns([owner]);
        store.ReadOpenAsync(id, 64, Arg.Any<CancellationToken>()).Returns([]);
        store.LoadPositionAsync(id, Arg.Any<CancellationToken>()).Returns(owner with { IsOpen = false });
        var commands = Substitute.For<IStrategyPositionCommandApi>();
        var sweep = new OpenPositionEndOfDaySweep(store, commands, new CmeFuturesMarketSessionCalendar(),
            new Clock(new(2026, 11, 17, 23, 0, 0, TimeSpan.Zero)), new());
        var audit = await sweep.RunAsync(default);
        Assert.Equal(1, audit.NotRequired); Assert.Equal(0, audit.FailedPositions);
        Assert.Empty(commands.ReceivedCalls());
    }

    [Fact]
    public async Task Cancellation_does_not_become_a_successful_empty_audit()
    {
        var store = Substitute.For<IOpenPositionEndOfDayStore>();
        store.ReadOpenAsync(null, 64, Arg.Any<CancellationToken>()).Returns(call =>
        { call.Arg<CancellationToken>().ThrowIfCancellationRequested(); return Task.FromResult(Array.Empty<StrategyPositionSnapshot>()); });
        var sweep = new OpenPositionEndOfDaySweep(store, Substitute.For<IStrategyPositionCommandApi>(),
            new CmeFuturesMarketSessionCalendar(), new Clock(DateTimeOffset.Parse("2026-11-17T23:00:00Z")), new());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sweep.RunAsync(cancel.Token));
    }

    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
