using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using System.Buffers;
using System.Text.Json;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionDownloadLogTests
{
    [Theory]
    [InlineData("ALL")]
    [InlineData("US")]
    public async Task Confirmed_empty_download_is_clear_and_preserves_real_lineage(string scope)
    {
        var row = CalendarDownloadFixture.Row(scope: scope);
        var (adapter, db, queries) = CalendarDownloadFixture.Adapter(row);
        var result = await adapter.ReadOnceAsync(new(), CalendarDownloadFixture.At, default);
        result.Status.Should().Be(MarketEventRiskStatus.Clear);
        result.Observation.Availability.Should().Be(MarketSourceAvailability.Available);
        result.DownloadEvidence!.Attempts.Should().ContainSingle().Which.Should().Be(row);
        result.DownloadEvidence.CheckedAtUtc.Should().Be(CalendarDownloadFixture.At);
        result.DownloadEvidence.Attempts[0].Outcome.FinishedAtUtc.Should().Be(CalendarDownloadFixture.At.AddHours(-1));
        await db.Received(1).GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), "US", Arg.Any<CancellationToken>());
        queries.ReceivedCalls().Should().HaveCount(2);
    }

    [Theory]
    [InlineData("missing", "CalendarDownloadNotConfirmed")]
    [InlineData("failed", "CalendarDownloadFailed")]
    [InlineData("stale", "CalendarDownloadStale")]
    [InlineData("future", "CalendarDownloadAfterCapture")]
    [InlineData("other-date", "CalendarDownloadNotConfirmed")]
    [InlineData("other-country", "CalendarDownloadNotConfirmed")]
    [InlineData("treasury", "CalendarDownloadNotConfirmed")]
    public async Task Unconfirmed_coverage_never_becomes_clear_or_reads_calendar_rows(string scenario, string reason)
    {
        var rows = scenario switch
        {
            "missing" => Array.Empty<MarketDataDownloadLogReadModel>(),
            "failed" => [CalendarDownloadFixture.Row(status: MarketDataDownloadStatus.Failed)],
            "stale" => [CalendarDownloadFixture.Row(finished: CalendarDownloadFixture.At.AddDays(-1))],
            "future" => [CalendarDownloadFixture.Row(finished: CalendarDownloadFixture.At.AddSeconds(1))],
            "other-date" => [CalendarDownloadFixture.Row(date: DateOnly.FromDateTime(CalendarDownloadFixture.At).AddDays(-1))],
            "other-country" => [CalendarDownloadFixture.Row(scope: "CA")],
            "treasury" => [CalendarDownloadFixture.Row(scope: "US", dataset: MarketDataDownloadDataset.TreasuryCurve)],
            _ => throw new ArgumentException(scenario)
        };
        var (adapter, db, _) = CalendarDownloadFixture.Adapter(rows);
        var result = await adapter.ReadOnceAsync(new(), CalendarDownloadFixture.At, default);
        result.Status.Should().Be(MarketEventRiskStatus.Unknown);
        result.Observation.Availability.Should().Be(MarketSourceAvailability.Unavailable);
        result.DownloadEvidence!.Reason.Should().Be(reason);
        result.DownloadEvidence.ValidUntilUtc.Should().BeNull();
        db.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Newer_failed_refresh_in_either_covering_scope_cannot_reuse_old_success()
    {
        var (adapter, _, _) = CalendarDownloadFixture.Adapter(CalendarDownloadFixture.Row(),
            CalendarDownloadFixture.Row(scope: "US", status: MarketDataDownloadStatus.Failed,
                finished: CalendarDownloadFixture.At.AddMinutes(-10)));
        var result = await adapter.ReadOnceAsync(new(), CalendarDownloadFixture.At, default);
        result.DownloadEvidence!.CoverageConfirmed.Should().BeFalse();
        result.DownloadEvidence.Reason.Should().Be("CalendarDownloadFailed");
    }

    [Fact]
    public async Task Bounded_latest_read_does_not_search_past_failure_even_when_more_history_exists()
    {
        var failed = CalendarDownloadFixture.Row(status: MarketDataDownloadStatus.Failed);
        var queries = CalendarDownloadFixture.Queries();
        queries.GetHistoryAsync(Arg.Is<MarketDataDownloadPartition>(x => x.Scope == "ALL"), 1, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceResult<MarketDataDownloadHistoryResult>
            {
                Success = true,
                Value = new([failed], new(failed.Outcome.RequestedAtUtc, failed.Outcome.ImportCommandId))
            });
        var result = await MarketConditionCalendarCoverage.CaptureAsync(queries, CalendarDownloadFixture.At, 30, 20, default);
        result.CoverageConfirmed.Should().BeFalse();
        queries.ReceivedCalls().Should().HaveCount(2);
    }

    [Fact]
    public async Task Midnight_window_requires_both_actual_calendar_dates()
    {
        var at = CalendarDownloadFixture.At.Date.AddMinutes(10);
        var today = DateOnly.FromDateTime(at);
        var current = CalendarDownloadFixture.Row(date: today, finished: at.AddMinutes(-5));
        var queries = CalendarDownloadFixture.Queries(current);
        var missing = await MarketConditionCalendarCoverage.CaptureAsync(queries, at, 30, 20, default);
        missing.CoverageConfirmed.Should().BeFalse();
        missing.FromDate.Should().Be(today.AddDays(-1));
        queries.ReceivedCalls().Should().HaveCount(4);
        var previous = CalendarDownloadFixture.Row(date: today.AddDays(-1), finished: at.AddHours(-1));
        var complete = await MarketConditionCalendarCoverage.CaptureAsync(CalendarDownloadFixture.Queries(current, previous), at, 30, 20, default);
        complete.CoverageConfirmed.Should().BeTrue();
    }

    [Theory]
    [InlineData(86399, true)]
    [InlineData(86400, false)]
    [InlineData(86401, false)]
    public async Task Download_age_requires_positive_remaining_validity(int ageSeconds, bool expected)
    {
        var row = CalendarDownloadFixture.Row(finished: CalendarDownloadFixture.At.AddSeconds(-ageSeconds));
        var result = await MarketConditionCalendarCoverage.CaptureAsync(CalendarDownloadFixture.Queries(row), CalendarDownloadFixture.At, 30, 20, default);
        result.CoverageConfirmed.Should().Be(expected);
    }

    [Fact]
    public async Task Query_failure_is_technical_failure_not_missing_or_clear()
    {
        var queries = CalendarDownloadFixture.Queries();
        queries.GetHistoryAsync(Arg.Any<MarketDataDownloadPartition>(), 1, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceResult<MarketDataDownloadHistoryResult> { Success = false, ErrorMessage = "unavailable" });
        var action = () => MarketConditionCalendarCoverage.CaptureAsync(queries, CalendarDownloadFixture.At, 30, 20, default).AsTask();
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("partition")]
    [InlineData("identity")]
    public async Task Corrupt_or_wrong_partition_evidence_is_rejected(string defect)
    {
        var row = CalendarDownloadFixture.Row();
        row = defect switch
        {
            "hash" => row with { PayloadSha256 = new string('0', 64) },
            "identity" => row with { LogCommandId = Guid.NewGuid() },
            _ => CalendarDownloadFixture.Row(date: DateOnly.FromDateTime(CalendarDownloadFixture.At).AddDays(-1))
        };
        var queries = CalendarDownloadFixture.Queries();
        queries.GetHistoryAsync(Arg.Any<MarketDataDownloadPartition>(), 1, null, Arg.Any<CancellationToken>())
            .Returns(CalendarDownloadFixture.Reply(row));
        var action = () => MarketConditionCalendarCoverage.CaptureAsync(queries, CalendarDownloadFixture.At, 30, 20, default).AsTask();
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancellation_is_passed_to_query_and_stops_capture()
    {
        using var cancellation = new CancellationTokenSource();
        var queries = CalendarDownloadFixture.Queries();
        queries.GetHistoryAsync(Arg.Any<MarketDataDownloadPartition>(), 1, null, cancellation.Token).Returns(_ =>
        {
            cancellation.Cancel();
            return CalendarDownloadFixture.Reply();
        });
        var action = () => MarketConditionCalendarCoverage.CaptureAsync(queries, CalendarDownloadFixture.At, 30, 20, cancellation.Token).AsTask();
        await action.Should().ThrowAsync<OperationCanceledException>();
        queries.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public async Task Excessive_window_is_rejected_before_queries()
    {
        var queries = CalendarDownloadFixture.Queries();
        var action = () => MarketConditionCalendarCoverage.CaptureAsync(queries, CalendarDownloadFixture.At, 5000, 5000, default).AsTask();
        await action.Should().ThrowAsync<InvalidOperationException>();
        queries.ReceivedCalls().Should().BeEmpty();
    }
}

[Trait("Category", "Verification")]
public sealed class MarketConditionDownloadLogVerificationTests
{
    [Fact]
    public void Legacy_result_without_download_evidence_preserves_its_fields()
    {
        var result = new MarketConditionResult { ResultId = Guid.NewGuid(), SnapshotSha256 = "historical-snapshot" };
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(result));
        reader.ReadArrayHeader().Should().Be(36);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(35);
        for (var i = 0; i < 35; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var restored = MessagePackSerializer.Deserialize<MarketConditionResult>(buffer.WrittenMemory);
        restored.CalendarDownloadEvidence.Should().BeNull();
        restored.OutputHints.Should().BeEquivalentTo(result.OutputHints);
        restored.SnapshotSha256.Should().Be(result.SnapshotSha256);
    }

    [Fact]
    public void Legacy_four_slot_event_state_remains_readable_without_changing_json_shape()
    {
        var original = new MarketConditionEventRiskState
        {
            Status = MarketEventRiskStatus.Clear,
            Observation = new() { SourceTimestampUtc = CalendarDownloadFixture.At, ReceivedAtUtc = CalendarDownloadFixture.At }
        };
        var bytes = MessagePackSerializer.Serialize(original);
        var reader = new MessagePackReader(bytes);
        reader.ReadArrayHeader().Should().Be(5);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(4);
        for (var i = 0; i < 4; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var restored = MessagePackSerializer.Deserialize<MarketConditionEventRiskState>(buffer.WrittenMemory);
        restored.DownloadEvidence.Should().BeNull();
        JsonSerializer.Serialize(restored).Should().Be(JsonSerializer.Serialize(original)).And.NotContain("DownloadEvidence");
    }

    [Fact]
    public async Task Sealed_evidence_round_trips_is_defensive_and_affects_snapshot_hash()
    {
        var at = CalendarDownloadFixture.At;
        var (adapter, _, _) = CalendarDownloadFixture.Adapter(CalendarDownloadFixture.Row(date: DateOnly.FromDateTime(at), finished: at.AddHours(-1)));
        var state = await adapter.ReadOnceAsync(new(), at, default);
        var snapshot = (MarketConditionAssessmentCalculationTests.Snapshot(AssessmentFixture.Command()) with { CalendarEvidence = state.DownloadEvidence }).Seal();
        var restored = MessagePackSerializer.Deserialize<MarketConditionAssessmentSnapshot>(MessagePackSerializer.Serialize(snapshot));
        restored.ComputeHash().Should().Be(snapshot.PayloadSha256);
        var copy = state.DownloadEvidence!.Attempts;
        copy[0] = copy[0] with { PayloadSha256 = "mutated" };
        state.DownloadEvidence.Attempts[0].PayloadSha256.Should().NotBe("mutated");
        var changed = snapshot with { CalendarEvidence = state.DownloadEvidence with { Reason = "changed" } };
        changed.ComputeHash().Should().NotBe(snapshot.PayloadSha256);
    }
}

internal static class CalendarDownloadFixture
{
    public static readonly DateTime At = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    public static MarketDataDownloadLogReadModel Row(string scope = "ALL", DateOnly? date = null,
        MarketDataDownloadStatus status = MarketDataDownloadStatus.Completed, DateTime? finished = null,
        MarketDataDownloadDataset dataset = MarketDataDownloadDataset.EconomicCalendar)
    {
        var end = finished ?? At.AddHours(-1);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = dataset, Scope = scope, ValueDate = date ?? DateOnly.FromDateTime(At),
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
            RequestedAtUtc = end.AddSeconds(-2), StartedAtUtc = end.AddSeconds(-1), FinishedAtUtc = end,
            Status = status, DownloadedRecordCount = status == MarketDataDownloadStatus.Completed ? 0 : null,
            PersistedRecordCount = status == MarketDataDownloadStatus.Completed ? 0 : null, ElapsedMilliseconds = 1000,
            ErrorCode = status == MarketDataDownloadStatus.Failed ? "FAILED" : null,
            ErrorMessage = status == MarketDataDownloadStatus.Failed ? "Import failed." : null
        };
        return new(outcome, MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId), outcome.ComputeHash(), end);
    }

    public static ServiceResult<MarketDataDownloadHistoryResult> Reply(params MarketDataDownloadLogReadModel[] rows)
        => new() { Success = true, Value = new(rows, null) };

    public static IDownloadLogQueryApi Queries(params MarketDataDownloadLogReadModel[] rows)
    {
        var queries = Substitute.For<IDownloadLogQueryApi>();
        queries.GetHistoryAsync(Arg.Any<MarketDataDownloadPartition>(), 1, null, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var p = call.Arg<MarketDataDownloadPartition>();
            return Reply(rows.Where(x => x.Outcome.Dataset == p.Dataset && x.Outcome.Provider == p.Provider &&
                x.Outcome.Scope == p.Scope && x.Outcome.ValueDate == p.ValueDate).OrderByDescending(x => x.Outcome.RequestedAtUtc).Take(1).ToArray());
        });
        return queries;
    }

    public static (MarketConditionEventRiskAdapter Adapter, IMarketDataDbContext Db, IDownloadLogQueryApi Queries) Adapter(params MarketDataDownloadLogReadModel[] rows)
    {
        var db = Substitute.For<IMarketDataDbContext>();
        db.GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), "US", Arg.Any<CancellationToken>())
            .Returns(new List<EconomicCalendarReadModel>());
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        var queries = Queries(rows);
        return (new(factory, queries), db, queries);
    }
}
