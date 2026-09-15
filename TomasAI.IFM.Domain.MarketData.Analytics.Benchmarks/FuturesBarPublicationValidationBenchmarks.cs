using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks;

/// <summary>Measures validation at bar closure, separate from the per-trade accumulator path.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class FuturesBarPublicationValidationBenchmarks
{
    readonly FuturesTradeSessionBarReadModelValidationRules rules = new();
    PublishFuturesTradeSessionBarCommand command = null!;

    [GlobalSetup]
    public void Setup()
    {
        var series = MarketSeriesIdentity.ForContract("ESU6");
        var entityId = new FuturesTradeSessionBarEntityId(series, TimeFrameType.FifteenSeconds);
        var end = new DateTimeOffset(2026, 9, 15, 14, 46, 15, TimeSpan.Zero);
        command = new PublishFuturesTradeSessionBarCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Bar = new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series,
                ObservationId = FuturesTradeSessionBarId.Create(series,
                    TimeFrameType.FifteenSeconds, end, 42),
                ContractId = "ESU6", ValueDate = new(2026, 9, 15),
                TimeFrame = TimeFrameType.FifteenSeconds,
                IntervalStartUtc = end.AddSeconds(-15), IntervalEndUtc = end,
                Open = 6500m, High = 6501m, Low = 6499m, Close = 6501m,
                Volume = 10m, TradeCount = 2, PriceVolumeSum = 65005m,
                FirstSourceSequence = 41, LastSourceSequence = 42,
                FirstMarketEventUtc = end.AddSeconds(-14),
                LastMarketEventUtc = end.AddMilliseconds(-39),
                CalculatedAtUtc = end.AddMilliseconds(-379),
                SchemaVersion = 2, CalculationVersion = "trade-session-bar-v1",
                IsComplete = true, IsValid = true,
                CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
                StreamEpochId = Guid.NewGuid()
            }
        };
    }

    [Benchmark(Baseline = true)]
    public int BarModelOnly() => rules.Execute(command.Bar).Length;

    [Benchmark]
    public int PublicationIngress() =>
        new List<ValidationError>().ValidatePublishBar(command).Count;
}
