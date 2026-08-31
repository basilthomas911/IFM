using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
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
}
