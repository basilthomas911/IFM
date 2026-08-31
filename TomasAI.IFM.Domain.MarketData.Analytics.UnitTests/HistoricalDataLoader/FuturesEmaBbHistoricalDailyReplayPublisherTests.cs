using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.HistoricalDataLoader;

public sealed class FuturesEmaBbHistoricalDailyReplayPublisherTests
{
    [Fact]
    public async Task OneYearReplay_WarmsEmaAndBollingerAndReconcilesTargetValueDate()
    {
        var actorService = Substitute.For<IActorService>();
        var emaCommands = new List<GenerateFuturesEmaSignalCommand>();
        var outlookCommands = new List<ObserveMarketOutlookComponentCommand>();
        actorService.RequestAsync<GenerateFuturesEmaSignalCommand, FuturesTradeSessionBarEntityId>(
                Arg.Do<GenerateFuturesEmaSignalCommand>(emaCommands.Add))
            .Returns(new ValueTask<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid())));
        actorService.RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(
                Arg.Do<ObserveMarketOutlookComponentCommand>(outlookCommands.Add))
            .Returns(new ValueTask<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid())));
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var firstDate = new DateOnly(2025, 1, 2);
        var observations = Enumerable.Range(0, 201)
            .Select(index => Observation(series, firstDate.AddDays(index), index + 1))
            .ToArray();
        var targetValueDate = observations[^1].ValueDate.AddDays(1);
        var publisher = new FuturesEmaBbHistoricalDailyReplayPublisher(actorService);

        await publisher.PublishAsync(observations, targetValueDate, CancellationToken.None);

        emaCommands.Should().HaveCount(201);
        outlookCommands.Should().ContainSingle();
        var reconcile = outlookCommands[0];
        reconcile.EntityId.ValueDate.Should().Be(targetValueDate);
        reconcile.EntityId.ContractId.Should().Be("ESZ25");
        reconcile.FuturesEmaSignal.Should().NotBeNull();
        reconcile.FuturesEmaSignal!.IsWarm.Should().BeTrue();
        reconcile.FuturesEmaSignal.Ema50.Should().NotBeNull();
        reconcile.FuturesEmaSignal.Ema200.Should().NotBeNull();
        reconcile.FuturesBbSignal.Should().NotBeNull();
        reconcile.FuturesBbSignal!.IsWarm.Should().BeTrue();
        reconcile.FuturesBbSignal.StandardDeviation20.Should().NotBeNull();
        reconcile.FuturesBbSignal.Upper20.Should().Be(
            reconcile.FuturesBbSignal.Ema20Center + 2m * reconcile.FuturesBbSignal.StandardDeviation20);
        reconcile.FuturesBbSignal.Lower20.Should().Be(
            reconcile.FuturesBbSignal.Ema20Center - 2m * reconcile.FuturesBbSignal.StandardDeviation20);
    }

    static FuturesEodObservationReadModel Observation(
        MarketSeriesIdentity series,
        DateOnly date,
        long sequence)
    {
        var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(14, 30)), TimeSpan.Zero);
        var end = start.AddHours(6).AddMinutes(30);
        return new()
        {
            MarketSeriesIdentity = series,
            ContractId = "ESZ25",
            ValueDate = date,
            SessionStartUtc = start,
            SessionEndUtc = end,
            Open = 5000m + sequence,
            High = 5010m + sequence,
            Low = 4990m + sequence,
            Close = 5005m + sequence,
            Volume = 100,
            TradeCount = 10,
            PriceVolumeSum = (5005m + sequence) * 100,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, end, sequence),
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = start,
            LastMarketEventUtc = end.AddTicks(-1),
            SchemaVersion = 1,
            IsComplete = true,
            IsValid = true
        };
    }
}
