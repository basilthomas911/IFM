using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.DownloadLog;

public class DownloadLogContractTests
{
    internal static MarketDataDownloadOutcome Outcome(MarketDataDownloadDataset dataset = MarketDataDownloadDataset.EconomicCalendar) => new()
    {
        Dataset = dataset, Scope = "US", ValueDate = new(2026, 9, 5), ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
        RequestedAtUtc = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc), StartedAtUtc = new(2026, 9, 5, 12, 0, 1, DateTimeKind.Utc),
        FinishedAtUtc = new(2026, 9, 5, 12, 0, 2, DateTimeKind.Utc), Status = MarketDataDownloadStatus.Completed,
        DownloadedRecordCount = 3, PersistedRecordCount = 3, ElapsedMilliseconds = 1000
    };

    [Fact] public void Logging_identity_is_stable_and_distinct_from_source_identity()
    {
        var outcome = Outcome(); var command = new InsertMarketDataDownloadLogCommand(outcome);
        command.Validate(); Assert.NotEqual(outcome.ImportCommandId, command.CommandId);
        Assert.Equal(command.CommandId, new InsertMarketDataDownloadLogCommand(outcome).CommandId);
        Assert.NotEqual(command.CommandId, new InsertMarketDataDownloadLogCommand(Outcome()).CommandId);
    }

    [Theory] [InlineData("us,CA,us", "CA,US")] [InlineData("", "ALL")]
    public void Scope_is_canonical(string input, string expected)
        => Assert.Equal(expected, MarketDataDownloadOutcome.CanonicalScope(input.Split(',', StringSplitOptions.RemoveEmptyEntries)));

    [Fact] public void Reserved_ALL_scope_cannot_be_supplied_as_a_country_filter()
        => Assert.Throws<ArgumentException>(() => MarketDataDownloadOutcome.CanonicalScope(["ALL"]));

    [Theory]
    [InlineData("version")] [InlineData("dataset")] [InlineData("status")] [InlineData("provider")]
    [InlineData("scope")] [InlineData("identity")] [InlineData("time")] [InlineData("precision")]
    [InlineData("elapsed")] [InlineData("count")] [InlineData("unknown-completed")] [InlineData("completed-error")]
    public void Invalid_outcomes_are_rejected(string field)
    {
        var o = Outcome();
        o = field switch
        {
            "version" => o with { SchemaVersion = 2 }, "dataset" => o with { Dataset = 0 }, "status" => o with { Status = 0 },
            "provider" => o with { Provider = "fmp" }, "scope" => o with { Scope = "us" }, "identity" => o with { ImportCommandId = Guid.Empty },
            "time" => o with { StartedAtUtc = o.RequestedAtUtc.AddSeconds(-1) }, "precision" => o with { RequestedAtUtc = o.RequestedAtUtc.AddTicks(1) },
            "elapsed" => o with { ElapsedMilliseconds = -1 }, "count" => o with { DownloadedRecordCount = -1 },
            "unknown-completed" => o with { PersistedRecordCount = null }, _ => o with { ErrorCode = "error" }
        };
        Assert.Throws<ArgumentException>(o.Validate);
    }

    [Fact] public void Failed_partial_write_preserves_unknown_count_and_requires_error()
    {
        var o = Outcome() with { Status = MarketDataDownloadStatus.Failed, PersistedRecordCount = null };
        Assert.Throws<ArgumentException>(o.Validate);
        (o with { ErrorCode = "StorageFailed", ErrorMessage = "Persistence was not confirmed." }).Validate();
    }

    [Fact] public void Command_and_outcome_round_trip_with_stable_hash()
    {
        var original = new InsertMarketDataDownloadLogCommand(Outcome());
        var copy = MessagePackSerializer.Deserialize<InsertMarketDataDownloadLogCommand>(MessagePackSerializer.Serialize(original));
        copy.Validate(); Assert.Equal(original, copy);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Four_terminal_contracts_accept_new_and_legacy_payloads(int type)
    {
        var o = Outcome();
        object original = type switch
        {
            0 => new EconomicCalendarsImportedCompleteEvent { DownloadOutcome = o },
            1 => new EconomicCalendarsImportedFailEvent { DownloadOutcome = o },
            2 => new YieldCurveRatesImportedCompleteEvent { DownloadOutcome = o },
            _ => new YieldCurveRatesImportedFailEvent { DownloadOutcome = o }
        };
        var bytes = MessagePackSerializer.Serialize(original.GetType(), original);
        var copy = MessagePackSerializer.Deserialize(original.GetType(), bytes);
        Assert.Equal(o, original.GetType().GetProperty("DownloadOutcome")!.GetValue(copy));
        // Strip the appended slot to model the actual pre-change array wire contract.
        var reader = new MessagePackReader(bytes); var length = reader.ReadArrayHeader();
        var buffer = new System.Buffers.ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(length - 1);
        for (var i = 0; i < length - 1; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var legacy = MessagePackSerializer.Deserialize(original.GetType(), buffer.WrittenMemory);
        Assert.Null(original.GetType().GetProperty("DownloadOutcome")!.GetValue(legacy));
    }

    [Fact] public void Equivalent_duplicate_is_noop_and_conflict_cannot_mutate_state()
    {
        var state = new DownloadLogCommandState(); var command = new InsertMarketDataDownloadLogCommand(Outcome());
        Assert.True(command.Execute(state).Success); Assert.Single(state.Events);
        Assert.True(command.Execute(state).Success); Assert.Single(state.Events);
        var conflict = new InsertMarketDataDownloadLogCommand(command.Outcome with { PersistedRecordCount = 4 });
        Assert.Throws<InvalidOperationException>(() => conflict.Execute(state));
        Assert.Equal(command.Outcome, state.Outcome);
    }
}
