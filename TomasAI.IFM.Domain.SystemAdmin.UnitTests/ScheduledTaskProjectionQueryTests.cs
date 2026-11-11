using System.Text.Json;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskProjectionQueryTests
{
    [Fact]
    public async Task Invalid_continuation_is_rejected_before_the_database_is_called()
    {
        var store = Substitute.For<IScheduledTaskReadStore>();
        var cursor = JsonSerializer.SerializeToUtf8Bytes(new ScheduledTaskHistoryCursor("wrong-scope", DateTime.UtcNow, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.GetRunHistoryAsync("Test", "host", new(Guid.NewGuid()), 25, cursor));
        Assert.Empty(store.ReceivedCalls());
    }

    [Fact]
    public async Task The_domain_supplies_a_bounded_selection_and_forwards_cancellation()
    {
        var store = Substitute.For<IScheduledTaskReadStore>();
        using var cancellation = new CancellationTokenSource();
        store.GetRunHistoryAsync(Arg.Any<ScheduledTaskHistorySelection>(), cancellation.Token)
            .Returns(ValueTask.FromResult(new ScheduledTaskRunPage()));
        await store.GetRunHistoryAsync("Test", "host", new(Guid.NewGuid()), 1000, null, cancellation.Token);
        await store.Received(1).GetRunHistoryAsync(Arg.Is<ScheduledTaskHistorySelection>(selection =>
            selection.Environment == "Test" && selection.Host == "host" && selection.PageSize == 100
            && selection.Scope.Length == 64 && selection.After == null), cancellation.Token);
    }

    [Fact]
    public async Task A_completed_close_is_found_after_newer_incomplete_runs_using_only_projected_history()
    {
        var store = Substitute.For<IScheduledTaskReadStore>();
        var completed = new DateOnly(2026, 11, 10);
        store.GetRunHistoryAsync(Arg.Any<ScheduledTaskHistorySelection>(), default).Returns(call =>
        {
            var selection = call.ArgAt<ScheduledTaskHistorySelection>(0);
            var successful = new ScheduledTaskRun
            {
                TaskKey = ScheduledTaskKeys.FuturesMarketClose, HostId = ScheduledTaskKeys.ApiMarketCloseHost,
                Status = ScheduledTaskRunStatus.Succeeded, Stage = ScheduledTaskKeys.MarketClosedStage,
                ExitCode = 0, CompletedEndOfDayValueDate = completed
            };
            return ValueTask.FromResult(selection.After is null
                ? new ScheduledTaskRunPage
                {
                    Runs = [successful with { Status = ScheduledTaskRunStatus.Running, CompletedEndOfDayValueDate = completed.AddDays(1) }],
                    PagingState = JsonSerializer.SerializeToUtf8Bytes(new ScheduledTaskHistoryCursor(selection.Scope, DateTime.UtcNow, Guid.NewGuid()))
                }
                : new ScheduledTaskRunPage { Runs = [successful] });
        });
        Assert.Equal(completed, await store.GetCompletedMarketCloseAsync("Test"));
        await store.Received(2).GetRunHistoryAsync(Arg.Any<ScheduledTaskHistorySelection>(), default);
        Assert.DoesNotContain(store.ReceivedCalls(), call => call.GetMethodInfo().Name == nameof(IScheduledTaskReadStore.GetCompletedEndOfDayAsync));
    }

    [Fact]
    public async Task Empty_projected_history_has_no_completed_close()
    {
        var store = Substitute.For<IScheduledTaskReadStore>();
        store.GetRunHistoryAsync(Arg.Any<ScheduledTaskHistorySelection>(), default).Returns(ValueTask.FromResult(new ScheduledTaskRunPage()));
        Assert.Null(await store.GetCompletedMarketCloseAsync("Test"));
    }
}
