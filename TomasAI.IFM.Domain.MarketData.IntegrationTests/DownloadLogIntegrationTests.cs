using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DownloadLogIntegrationTests(MarketDataFixture fixture) : IClassFixture<MarketDataFixture>
{
    static MarketDataDownloadOutcome Outcome(MarketDataDownloadDataset dataset, DateOnly date, int seconds = 0) => new()
    {
        Dataset = dataset, Scope = "US", ValueDate = date, ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
        RequestedAtUtc = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc).AddSeconds(seconds),
        StartedAtUtc = new DateTime(2026, 9, 5, 12, 0, 1, DateTimeKind.Utc).AddSeconds(seconds),
        FinishedAtUtc = new DateTime(2026, 9, 5, 12, 0, 2, DateTimeKind.Utc).AddSeconds(seconds),
        Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 0, PersistedRecordCount = 0, ElapsedMilliseconds = 1000
    };
    Task Insert(MarketDataDownloadOutcome outcome) => fixture.MarketDataDb.InsertMarketDataDownloadLogAsync(
        outcome, MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId), outcome.ComputeHash());

    [Theory] [InlineData(MarketDataDownloadDataset.EconomicCalendar)] [InlineData(MarketDataDownloadDataset.TreasuryCurve)]
    public async Task Scylla_round_trip_replay_and_latest_failure_preserve_earlier_success(MarketDataDownloadDataset dataset)
    {
        var date = DateOnly.FromDayNumber(Random.Shared.Next(new DateOnly(7000, 1, 1).DayNumber, new DateOnly(8990, 1, 1).DayNumber));
        var success = Outcome(dataset, date); var partition = new MarketDataDownloadPartition(dataset, "FMP", "US", date);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition)).CompletionConfirmed);
        await Insert(success); await Insert(success);
        var failed = Outcome(dataset, date, 5) with { Status = MarketDataDownloadStatus.Failed, DownloadedRecordCount = 7, PersistedRecordCount = null, ErrorCode = "StorageFailed", ErrorMessage = "Write was not confirmed." };
        await Insert(failed);
        var exact = await fixture.MarketDataDb.GetMarketDataDownloadLogAsync(partition, new(success.RequestedAtUtc, success.ImportCommandId));
        Assert.Equal(success, exact.Attempt!.Outcome);
        var history = await fixture.MarketDataDb.GetMarketDataDownloadHistoryAsync(partition, 1);
        Assert.Equal(failed, Assert.Single(history.Attempts).Outcome); Assert.NotNull(history.Continuation);
        var next = await fixture.MarketDataDb.GetMarketDataDownloadHistoryAsync(partition, 1, history.Continuation);
        Assert.Equal(success, Assert.Single(next.Attempts).Outcome); Assert.Null(next.Continuation);
        var status = await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition);
        Assert.True(status.CompletionConfirmed); Assert.Equal(failed, status.LatestAttempt!.Outcome); Assert.Equal(success, status.SuccessfulAttempt!.Outcome);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, failed.ImportCommandId)).CompletionConfirmed);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, Guid.NewGuid())).CompletionConfirmed);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition with { ValueDate = date.AddDays(1) })).CompletionConfirmed);
    }

    [Fact] public async Task Calendar_scope_isolation_and_bounded_search_do_not_invent_completion()
    {
        var date = DateOnly.FromDayNumber(Random.Shared.Next(new DateOnly(7000, 1, 1).DayNumber, new DateOnly(8990, 1, 1).DayNumber));
        var partition = new MarketDataDownloadPartition(MarketDataDownloadDataset.EconomicCalendar, "FMP", "US", date);
        var original = Outcome(partition.Dataset, date); await Insert(original);
        for (var i = 1; i <= 101; i++)
            await Insert(Outcome(partition.Dataset, date, i * 3) with { Status = MarketDataDownloadStatus.Failed, ErrorCode = "Failed", ErrorMessage = "Provider unavailable." });
        var first = await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition);
        Assert.False(first.CompletionConfirmed); Assert.False(first.SearchExhaustive); Assert.NotNull(first.Continuation);
        var rest = await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, cursor: first.Continuation);
        Assert.True(rest.CompletionConfirmed); Assert.Equal(original.ImportCommandId, rest.SuccessfulAttempt!.Outcome.ImportCommandId);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition with { Scope = "CA" })).CompletionConfirmed);
    }

    [Fact] public async Task Paging_preserves_attempts_with_identical_millisecond_request_times()
    {
        var date = DateOnly.FromDayNumber(Random.Shared.Next(new DateOnly(7000, 1, 1).DayNumber, new DateOnly(8990, 1, 1).DayNumber));
        var outcomes = Enumerable.Range(0, 4).Select(_ => Outcome(MarketDataDownloadDataset.EconomicCalendar, date)).ToArray();
        foreach (var outcome in outcomes) await Insert(outcome);
        var partition = new MarketDataDownloadPartition(outcomes[0].Dataset, "FMP", "US", date);
        var ids = new HashSet<Guid>(); MarketDataDownloadCursor? cursor = null;
        do
        {
            var page = await fixture.MarketDataDb.GetMarketDataDownloadHistoryAsync(partition, 1, cursor);
            Assert.True(ids.Add(Assert.Single(page.Attempts).Outcome.ImportCommandId));
            cursor = page.Continuation;
        } while (cursor is not null);
        Assert.True(ids.SetEquals(outcomes.Select(o => o.ImportCommandId)));
    }
}
