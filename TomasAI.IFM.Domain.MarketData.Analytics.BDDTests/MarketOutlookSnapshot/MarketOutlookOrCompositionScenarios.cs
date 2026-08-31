using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.MarketOutlookSnapshot;

/// <summary>Executable behavior specifications for independent Market Outlook components.</summary>
public sealed class MarketOutlookOrCompositionScenarios
{
    [Fact]
    public void GivenEodAndRsiButNoOtherAnalytics_WhenPublished_ThenAvailableDataIsVisibleAndMissingSiblingsDoNotBlock()
    {
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var state = new MarketOutlookSnapshotCommandState();
        var sourceId = Guid.NewGuid();
        var eod = new FuturesEodDataV2ReadModel(
            entityId.ContractId,
            entityId.ValueDate,
            "ES",
            6_400m,
            6_450m,
            6_350m,
            6_425m,
            100_000,
            0.0039,
            priceDirection: PriceDirectionType.Rising);
        var rsi = new FuturesRsiSignalReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength,
            RSI = 62.5,
            RSISlope = 0.75
        };
        var command = new PublishMarketOutlookSnapshotCommand(
            entityId,
            sourceId,
            1,
            DateTime.UtcNow,
            eod,
            futuresRsiSignal: rsi)
        {
            CommandId = sourceId,
            Subject = new ActorSubject(
                ActorType.Command,
                PublishMarketOutlookSnapshotCommand.Actor,
                PublishMarketOutlookSnapshotCommand.Verb,
                entityId.Format())
        };

        command.Execute(state).Success.Should().BeTrue();

        var snapshot = state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<MarketOutlookSnapshotPublishedEvent>()
            .Which.MarketOutlook;
        snapshot.Should().NotBeNull();
        snapshot.FuturesEodData.Should().Be(eod);
        snapshot.FuturesEodData.OpenPrice.Should().Be(6_400m);
        snapshot.FuturesEodData.ClosePrice.Should().Be(6_425m);
        snapshot.FuturesEodData.DailyPercentChange.Should().Be(0.0039);
        snapshot.FuturesRsiSignal.Should().Be(rsi);
        snapshot.FuturesTradeSignal.Should().NotBeNull();
        snapshot.FuturesTradeSignal!.RSI.Should().Be(62.5);
        snapshot.MissingInputs.Should().Contain("TDI");
        snapshot.MissingInputs.Should().Contain("ITI direction");
        snapshot.IsComplete.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GivenAnyDailyAnalyticsCombination_WhenPublished_ThenEveryAvailableFamilyIsPreserved(
        bool includeEma,
        bool includeBb)
    {
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var metadata = DailyMetadata(entityId);
        var ema = includeEma
            ? new FuturesEmaSignalReadModel
            {
                Metadata = metadata,
                Ema50 = 6200m,
                Ema200 = 6000m,
                IsWarm = true
            }
            : null;
        var bb = includeBb
            ? new FuturesBbSignalReadModel
            {
                Metadata = metadata with
                {
                    SignalKey = metadata.SignalKey with
                    {
                        SignalKind = MarketAnalyticsSignalKind.BollingerBand
                    }
                },
                Ema20Center = 6300m,
                StandardDeviation20 = 25m,
                Upper20 = 6350m,
                Lower20 = 6250m,
                IsWarm = true
            }
            : null;
        var sourceId = Guid.NewGuid();
        var command = new PublishMarketOutlookSnapshotCommand(
            entityId,
            sourceId,
            2,
            DateTime.UtcNow,
            new FuturesEodDataV2ReadModel(
                entityId.ContractId,
                entityId.ValueDate,
                "ES",
                6400m,
                6450m,
                6350m,
                6425m,
                100_000,
                0.0039),
            futuresEmaSignal: ema,
            futuresBbSignal: bb)
        {
            CommandId = sourceId,
            Subject = new(
                ActorType.Command,
                PublishMarketOutlookSnapshotCommand.Actor,
                PublishMarketOutlookSnapshotCommand.Verb,
                entityId.Format())
        };
        var state = new MarketOutlookSnapshotCommandState();

        command.Execute(state).Success.Should().BeTrue();

        var snapshot = state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<MarketOutlookSnapshotPublishedEvent>()
            .Which.MarketOutlook;
        (snapshot.FuturesEmaSignal is not null).Should().Be(includeEma);
        (snapshot.FuturesBbSignal is not null).Should().Be(includeBb);
        snapshot.MissingInputs.Contains("EMA", StringComparison.Ordinal).Should().Be(!includeEma);
        snapshot.MissingInputs.Contains("Bollinger Bands", StringComparison.Ordinal).Should().Be(!includeBb);
    }

    static MarketAnalyticsSignalMetadata DailyMetadata(MarketOutlookEntityId entityId)
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var end = new DateTimeOffset(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);
        return new()
        {
            SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "daily-v1"),
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, end, 2),
            MarketDataAsOfUtc = end,
            CalculatedAtUtc = end,
            SourceSequence = 2,
            SchemaVersion = 1,
            CalculationVersion = "daily-v1",
            IsValid = true
        };
    }
}
