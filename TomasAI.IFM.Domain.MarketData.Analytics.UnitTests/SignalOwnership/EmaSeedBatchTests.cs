using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.SignalOwnership;

public sealed class EmaSeedBatchTests
{
    [Fact]
    public void Late_history_repair_rebuilds_cold_prefix_without_regressing_newer_live_state()
    {
        var bars = Enumerable.Range(1, 48).Select(index => Bar(index, 5000 + index / 4m)).ToArray();
        var state = new FuturesEmaSignalCommandState();
        var seed = Seed(bars);
        foreach (var bar in bars.TakeLast(17))
            Assert.True(new GenerateFuturesEmaSignalCommand { CommandId = Guid.NewGuid(), EntityId = seed.EntityId,
                Subject = seed.Subject with { Verb = GenerateFuturesEmaSignalCommand.Verb }, Observation = bar }.Execute(state).Success);
        Assert.Equal(17, state.FuturesEmaCheckpoint!.Count);
        Assert.True(seed.Execute(state).Success);
        Assert.Equal(48, state.FuturesEmaCheckpoint!.Count);
        Assert.NotNull(state.FuturesEmaCheckpoint.Ema20);
        Assert.True(seed.Execute(state).Success);
        Assert.Equal(48, state.FuturesEmaCheckpoint.Count);
    }

    [Fact]
    public void Bollinger_seed_creates_full_state_and_retries_do_not_append_duplicate_events()
    {
        var bars = Enumerable.Range(1, 48).Select(index => Bar(index, 5000 + index / 4m)).ToArray();
        var ema = Seed(bars);
        var seed = new SeedFuturesBbSignalsCommand { CommandId = Guid.NewGuid(), EntityId = ema.EntityId,
            Subject = new(ActorType.Command, SeedFuturesBbSignalsCommand.Actor, SeedFuturesBbSignalsCommand.Verb, ema.EntityId.Format()), Observations = bars };
        var state = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State.FuturesBbSignalCommandState();
        Assert.True(TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.SeedFuturesBbSignals.Execute(seed, state).Success);
        Assert.True(state.FuturesBbSignal!.IsWarm);
        Assert.Equal(20, state.FuturesBbCheckpoint!.Closes.Length);
        Assert.Equal(20, state.FuturesBbCheckpoint.CompletedWidths20.Length);
        var count = state.Events.Count();
        Assert.True(TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.SeedFuturesBbSignals.Execute(seed, state).Success);
        Assert.Equal(count, state.Events.Count());
        var restored = MessagePackBinarySerializer.Shared.Deserialize<SeedFuturesBbSignalsCommand>(MessagePackBinarySerializer.Shared.Serialize(seed)!)!;
        Assert.Equal(48, restored.Observations.Length);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(48)]
    [InlineData(64)]
    public void Ordered_batch_matches_individual_commands_and_retries_append_no_duplicates(int count)
    {
        var bars = Enumerable.Range(1, count).Select(index => Bar(index, 5000 + index / 4m)).ToArray();
        var seed = Seed(bars);
        Assert.Empty(new List<ValidationError>().ValidateSeedBatch(seed));
        var serial = new FuturesEmaSignalCommandState();
        foreach (var bar in bars)
            Assert.True(new GenerateFuturesEmaSignalCommand { CommandId = Guid.NewGuid(), EntityId = seed.EntityId,
                Subject = seed.Subject with { Verb = GenerateFuturesEmaSignalCommand.Verb }, Observation = bar }.Execute(serial).Success);
        var batch = new FuturesEmaSignalCommandState();
        Assert.True(seed.Execute(batch).Success);
        Assert.Equal(serial.FuturesEmaCheckpoint, batch.FuturesEmaCheckpoint);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(serial.FuturesEmaSignal), System.Text.Json.JsonSerializer.Serialize(batch.FuturesEmaSignal));
        Assert.Equal(count, batch.Events.Count);
        Assert.True(seed.Execute(batch).Success);
        Assert.Equal(count, batch.Events.Count);
        Assert.All(batch.Events, fact => Assert.IsType<FuturesEmaSignalGeneratedEvent>(fact));
        var restored = MessagePackBinarySerializer.Shared.Deserialize<SeedFuturesEmaSignalsCommand>(MessagePackBinarySerializer.Shared.Serialize(seed)!)!;
        Assert.Equal(seed.StreamId, restored.StreamId);
        Assert.Equal(seed.Observations.Select(bar => bar.ObservationId), restored.Observations.Select(bar => bar.ObservationId));
    }

    [Fact]
    public void Overlapping_seed_continues_from_committed_prefix_and_keeps_the_same_aggregate_stream()
    {
        var bars = Enumerable.Range(1, 48).Select(index => Bar(index, 5000 + index / 4m)).ToArray();
        var state = new FuturesEmaSignalCommandState();
        Assert.True(Seed(bars[..24]).Execute(state).Success);
        state.AcceptChanges();
        var seed = Seed(bars);
        Assert.True(seed.Execute(state).Success);
        Assert.Equal(24, state.Events.Count); Assert.Equal(48, state.FuturesEmaCheckpoint!.Count);
        Assert.Equal(seed.StreamId, (seed.Subject with { Verb = GenerateFuturesEmaSignalCommand.Verb }).StreamId);
    }

    [Fact]
    public void Computation_failure_happens_before_any_state_mutation()
    {
        var state = new FuturesEmaSignalCommandState();
        var seed = Seed([Bar(1, decimal.MaxValue / 2), Bar(2, decimal.MaxValue / 2), Bar(3, decimal.MaxValue / 2)]);
        Assert.Throws<OverflowException>(() => seed.Execute(state));
        Assert.Null(state.FuturesEmaCheckpoint); Assert.Empty(state.Events);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("order")]
    [InlineData("owner")]
    [InlineData("invalid")]
    public void Whole_batch_validation_rejects_oversized_unordered_mixed_owner_and_invalid_observations(string problem)
    {
        var bars = problem switch
        {
            "count" => Enumerable.Range(1, 65).Select(index => Bar(index, 5000m)).ToArray(),
            "order" => new[] { Bar(2, 5000m), Bar(1, 5000m) },
            "owner" => new[] { Bar(1, 5000m) with { TimeFrame = TimeFrameType.OneHour } },
            _ => new[] { Bar(1, 5000m) with { IsComplete = false } }
        };
        Assert.NotEmpty(new List<ValidationError>().ValidateSeedBatch(Seed(bars)));
    }

    static SeedFuturesEmaSignalsCommand Seed(FuturesTradeSessionBarReadModel[] bars)
    {
        var id = new FuturesTradeSessionBarEntityId(MarketSeriesIdentity.ForContract("ES20261218"), TimeFrameType.OneMinute);
        return new() { CommandId = Guid.NewGuid(), EntityId = id, Observations = bars,
            Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, id.Format()) };
    }

    static FuturesTradeSessionBarReadModel Bar(int index, decimal close)
    {
        var series = MarketSeriesIdentity.ForContract("ES20261218");
        var end = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero).AddMinutes(index);
        return new() { MarketSeriesIdentity = series, ContractId = "ES20261218", ValueDate = new(2026, 11, 6), TimeFrame = TimeFrameType.OneMinute,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, end, index),
            IntervalStartUtc = end.AddMinutes(-1), IntervalEndUtc = end, Open = close, High = close + 1, Low = close - 1, Close = close,
            Volume = 1, TradeCount = 1, PriceVolumeSum = close, FirstSourceSequence = index, LastSourceSequence = index,
            FirstMarketEventUtc = end.AddSeconds(-59), LastMarketEventUtc = end.AddMilliseconds(-1), CalculatedAtUtc = end,
            IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "seed-test-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate };
    }
}
