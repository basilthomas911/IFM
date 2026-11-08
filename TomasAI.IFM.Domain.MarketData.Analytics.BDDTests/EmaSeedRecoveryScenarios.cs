using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests;

public sealed class EmaSeedRecoveryScenarios
{
    [Fact]
    public void Given_a_saved_prefix_when_restarted_with_an_overlapping_seed_then_only_the_unfinished_suffix_is_emitted()
    {
        var bars = Enumerable.Range(1, 48).Select(Observation).ToArray();
        var id = new FuturesTradeSessionBarEntityId(bars[0].MarketSeriesIdentity, TimeFrameType.OneMinute);
        var command = new SeedFuturesEmaSignalsCommand { CommandId = Guid.NewGuid(), EntityId = id, Observations = bars[..24],
            Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, id.Format()) };
        var original = new FuturesEmaSignalCommandState();
        Assert.True(command.Execute(original).Success);
        var snapshot = MessagePackBinarySerializer.Shared.Deserialize<FuturesEmaSignalGeneratedEvent>(
            MessagePackBinarySerializer.Shared.Serialize((FuturesEmaSignalGeneratedEvent)original.Events.Last())!)!;

        var restarted = new FuturesEmaSignalCommandState();
        restarted.ReplayEvents(new IEvent[] { snapshot });
        restarted.RestoreCommittedStreamVersion(24);
        Assert.Equal(24, restarted.FuturesEmaCheckpoint!.Count);
        var retry = command with { CommandId = Guid.NewGuid(), Observations = bars };
        Assert.True(retry.Execute(restarted).Success);
        Assert.Equal(48, restarted.FuturesEmaCheckpoint.Count);
        Assert.Equal(Enumerable.Range(25, 24).Select(value => (long)value), restarted.Events.Cast<FuturesEmaSignalGeneratedEvent>()
            .Select(fact => fact.Observation.LastSourceSequence));
        restarted.AcceptChanges();
        Assert.True(retry.Execute(restarted).Success); Assert.Empty(restarted.Events);
    }

    static FuturesTradeSessionBarReadModel Observation(int sequence)
    {
        var series = MarketSeriesIdentity.ForContract("ES20261218");
        var end = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero).AddMinutes(sequence);
        return new() { MarketSeriesIdentity = series, ContractId = "ES20261218", ValueDate = new(2026, 11, 6), TimeFrame = TimeFrameType.OneMinute,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, end, sequence),
            IntervalStartUtc = end.AddMinutes(-1), IntervalEndUtc = end, Open = 5000, High = 5001, Low = 4999, Close = 5000 + sequence / 100m,
            Volume = 1, TradeCount = 1, PriceVolumeSum = 5000 + sequence / 100m, FirstSourceSequence = sequence, LastSourceSequence = sequence,
            FirstMarketEventUtc = end.AddSeconds(-59), LastMarketEventUtc = end.AddMilliseconds(-1), CalculatedAtUtc = end,
            IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "bdd-seed-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate };
    }
}
