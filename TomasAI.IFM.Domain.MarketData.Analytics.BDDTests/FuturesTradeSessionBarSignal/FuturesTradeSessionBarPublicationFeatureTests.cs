using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesTradeSessionBarSignal;

/// <summary>Specifies the completed-bar ingress behavior at the Realtime-to-Command boundary.</summary>
public sealed class FuturesTradeSessionBarPublicationFeatureTests
{
    [Fact]
    public async Task GivenMarketClockLeadsHost_WhenBarCloses_ThenOnePublicationIsRequested()
    {
        var bar = CompletedBar();
        var context = Substitute.For<IEventActorContext>();
        PublishFuturesTradeSessionBarCommand? submitted = null;
        context.RequestAsync<PublishFuturesTradeSessionBarCommand, FuturesTradeSessionBarEntityId>(
                Arg.Do<PublishFuturesTradeSessionBarCommand>(command => submitted = command))
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        var result = await context.PublishFuturesTradeSessionBarAsync(bar);

        result.Success.Should().BeTrue();
        submitted.Should().NotBeNull();
        submitted!.Bar.Should().Be(bar);
        submitted.CommandId.Should().NotBe(Guid.Empty);
        submitted.CommandId.Should().NotBe(bar.ObservationId.Value);
    }

    [Fact]
    public async Task GivenInvalidBar_WhenPublicationIsRequested_ThenFailureIsReturnedWithoutCommandOrThrow()
    {
        var context = Substitute.For<IEventActorContext>();
        var result = await context.PublishFuturesTradeSessionBarAsync(
            CompletedBar() with { ContractId = string.Empty });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Contract Id");
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<PublishFuturesTradeSessionBarCommand, FuturesTradeSessionBarEntityId>(default!);
    }

    static FuturesTradeSessionBarReadModel CompletedBar()
    {
        var series = MarketSeriesIdentity.ForContract("ESU6");
        var end = new DateTimeOffset(2026, 9, 15, 14, 46, 15, TimeSpan.Zero);
        return new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FifteenSeconds,
                end, 24608142),
            ContractId = "ESU6",
            ValueDate = new DateOnly(2026, 9, 15),
            TimeFrame = TimeFrameType.FifteenSeconds,
            IntervalStartUtc = end.AddSeconds(-15),
            IntervalEndUtc = end,
            Open = 6500m, High = 6501m, Low = 6499m, Close = 6501m,
            Volume = 10m, TradeCount = 2, PriceVolumeSum = 65005m,
            FirstSourceSequence = 24600931, LastSourceSequence = 24608142,
            FirstMarketEventUtc = end.AddSeconds(-14),
            LastMarketEventUtc = end.AddMilliseconds(-39),
            CalculatedAtUtc = end.AddMilliseconds(-379),
            SchemaVersion = 2, CalculationVersion = "trade-session-bar-v1",
            IsComplete = true, IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
            StreamEpochId = Guid.NewGuid()
        };
    }
}
