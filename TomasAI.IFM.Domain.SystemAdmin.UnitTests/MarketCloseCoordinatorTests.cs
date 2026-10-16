using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.MarketClose;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class MarketCloseCoordinatorTests
{
    static readonly DateOnly Date = new(2026, 10, 15);
    static readonly DateTime Boundary = new(2026, 10, 15, 21, 0, 0, DateTimeKind.Utc);
    readonly Guid run = Guid.NewGuid();
    readonly IFuturesMarketSessionAuthority authority = Substitute.For<IFuturesMarketSessionAuthority>();
    readonly IMarketCloseOperations operations = Substitute.For<IMarketCloseOperations>();
    readonly MarketCloseCoordinator close;

    public MarketCloseCoordinatorTests()
    {
        authority.Current.Returns(Session());
        operations.BeginAsync(Date, Boundary, Arg.Any<CancellationToken>()).Returns(run);
        operations.GetCompletedDateAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateOnly?>(null));
        operations.LoadPositionsAsync(Date, Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ScheduledMarketPositionPage()));
        close = new(authority, operations, new Clock(), NullLogger<MarketCloseCoordinator>.Instance);
    }

    [Fact]
    public async Task Open_session_closes_at_actual_time_and_records_EOD()
    {
        var now = Boundary.AddHours(-4);
        authority.Current.Returns(Session() with { State = FuturesMarketState.LiveTrading, ActiveValueDate = Date });
        operations.BeginAsync(Date, now, Arg.Any<CancellationToken>()).Returns(run);
        var intraday = new MarketCloseCoordinator(authority, operations, new Clock(now), NullLogger<MarketCloseCoordinator>.Instance);
        var result = await intraday.CloseMarketAsync(default);
        Assert.Equal(Date, result.ValueDate);
        await operations.Received(1).BeginAsync(Date, now, Arg.Any<CancellationToken>());
        await operations.Received(1).StopFeedsAsync(Date, Arg.Any<CancellationToken>());
        await operations.Received(1).RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retry_reuses_first_boundary_instead_of_changing_EOD_command_payload()
    {
        var first = Boundary.AddHours(-4);
        operations.GetPreviousCloseBoundaryAsync(Date, Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateTime?>(first));
        operations.BeginAsync(Date, first, Arg.Any<CancellationToken>()).Returns(run);
        await close.CloseMarketAsync(default);
        await operations.Received(1).BeginAsync(Date, first, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Captured_date_and_paginated_positions_are_finalized_before_completion()
    {
        var first = Position(); var second = Position(); var sealedPosition = Position() with { LatestSealedValueDate = Date };
        operations.LoadPositionsAsync(Date, Arg.Any<byte[]?>(), Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new ScheduledMarketPositionPage { Positions = [first, sealedPosition], PagingState = [1] }),
            Task.FromResult(new ScheduledMarketPositionPage { Positions = [first, second] }));
        operations.StopFeedsAsync(Date, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            authority.Current.Returns(Session() with { OperationalValueDate = Date.AddDays(1) });
            return Task.CompletedTask;
        });
        var result = await close.CloseMarketAsync(default);
        Assert.Equal(Date, result.ValueDate); Assert.Equal(run, result.RunId); Assert.Equal(3, result.PositionsFinalized);
        Received.InOrder(() =>
        {
            operations.BeginAsync(Date, Boundary, Arg.Any<CancellationToken>());
            operations.StopFeedsAsync(Date, Arg.Any<CancellationToken>());
            operations.RecordStageAsync(run, Date, "FeedsStopped", Arg.Any<string>(), Arg.Any<CancellationToken>());
            operations.FinalizePositionAsync(first, Date, Boundary, Arg.Any<CancellationToken>());
            operations.FinalizePositionAsync(second, Date, Boundary, Arg.Any<CancellationToken>());
            operations.RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>());
            operations.PurgeCompletedMessagesAsync(Arg.Any<CancellationToken>());
            operations.CompleteAsync(run, Arg.Any<CancellationToken>());
        });
        await operations.DidNotReceive().FinalizePositionAsync(sealedPosition, Date, Boundary, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_failure_prevents_position_finalization_and_completion_marker()
    {
        operations.StopFeedsAsync(Date, Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("stop failed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => close.CloseMarketAsync(default));
        await operations.DidNotReceive().RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().LoadPositionsAsync(Arg.Any<DateOnly>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
        await operations.Received(1).FailAsync(run, Arg.Any<Exception>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Position_failure_cannot_commit_whole_session_completion_or_purge()
    {
        var position = Position();
        operations.LoadPositionsAsync(Date, Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ScheduledMarketPositionPage { Positions = [position] }));
        operations.FinalizePositionAsync(position, Date, Boundary, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("EOD failed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => close.CloseMarketAsync(default));
        await operations.DidNotReceive().RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().PurgeCompletedMessagesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Maintenance_failure_preserves_EOD_and_retry_does_not_refinalize_positions()
    {
        var calls = 0;
        operations.RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            operations.GetCompletedDateAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateOnly?>(Date));
            return Task.CompletedTask;
        });
        operations.PurgeCompletedMessagesAsync(Arg.Any<CancellationToken>()).Returns(_ => ++calls == 1
            ? Task.FromException(new InvalidOperationException("maintenance failed")) : Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => close.CloseMarketAsync(default));
        var retry = await close.CloseMarketAsync(default);
        Assert.True(retry.AlreadyCompleted);
        await operations.Received(2).StopFeedsAsync(Date, Arg.Any<CancellationToken>());
        await operations.Received(1).RecordStageAsync(run, Date, "PositionsFinalized", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await operations.Received(2).PurgeCompletedMessagesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Already_completed_date_is_idempotent()
    {
        operations.GetCompletedDateAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateOnly?>(Date));
        authority.Current.Returns(Session() with { OperationalValueDate = Date.AddDays(1) });
        var result = await close.CloseMarketAsync(default);
        Assert.True(result.AlreadyCompleted);
        Assert.Equal(Date, result.ValueDate);
        await operations.DidNotReceive().BeginAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await operations.Received(1).StopFeedsAsync(Date, Arg.Any<CancellationToken>());
    }

    static StrategyPositionSnapshot Position() => new() { Id = new(new(1, 2, 3, 4), Guid.NewGuid()), IsOpen = true };
    static MarketSessionReadModel Session() => new()
    {
        OperationalValueDate = Date, State = FuturesMarketState.Closed, Revision = 1,
        SessionStartUtc = Boundary.AddHours(-23), SessionEndUtc = Boundary,
        NextTransitionUtc = Boundary.AddHours(1), AsOfUtc = Boundary.AddMinutes(1)
    };
    sealed class Clock(DateTime? now = null) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now ?? Boundary.AddMinutes(1)); }
}
