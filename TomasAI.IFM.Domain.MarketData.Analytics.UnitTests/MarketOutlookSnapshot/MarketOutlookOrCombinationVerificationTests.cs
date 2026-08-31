using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

/// <summary>
/// Exhaustively qualifies every non-empty availability combination for the seven independent
/// Market Outlook inputs. This is availability coverage, not an assertion that unrelated
/// indicator formulas may substitute for one another.
/// </summary>
public sealed class MarketOutlookOrCombinationVerificationTests
{
    public static IEnumerable<object[]> NonEmptyAvailabilityMasks()
        => Enumerable.Range(1, 127).Select(static mask => new object[] { mask });

    [Theory]
    [MemberData(nameof(NonEmptyAvailabilityMasks))]
    public void EveryNonEmptyInputCombination_AdvancesAvailableComponentsWithoutSiblingGates(int mask)
    {
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var state = new MarketOutlookSnapshotCommandState();
        var sequence = 0L;
        var timestamp = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Utc);
        var rsi = SampleData.AtrRsiSignals[0] with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength
        };
        var tdi = SampleData.TdiReadModelFor(TimeFrameType.FifteenSeconds) with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate
        };
        var iti = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            TimePeriod = TimeFrameType.Daily
        };

        if (Has(1)) Observe(rsi: rsi);
        if (Has(2)) Observe(tdi: tdi);
        if (Has(3)) Observe(iti: iti with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged });
        if (Has(4)) Observe(iti: iti with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged });
        if (Has(5)) Observe(iti: iti with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged });
        if (Has(6)) Observe(vix: 22.5m);
        if (Has(0))
        {
            var sourceId = Guid.NewGuid();
            var eod = SampleData.EodData with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                Symbol = "ES",
                OpenPrice = 5400m,
                ClosePrice = 5425m,
                DailyPercentChange = 0.0046
            };
            new PublishMarketOutlookSnapshotCommand(
                entityId,
                sourceId,
                ++sequence,
                timestamp.AddSeconds(sequence),
                eod)
            {
                CommandId = sourceId,
                Subject = Subject(PublishMarketOutlookSnapshotCommand.Verb)
            }.Execute(state).Success.Should().BeTrue();
        }

        var snapshot = state.WorkingState.PublishedSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.FuturesEodData.IsValid.Should().Be(Has(0));
        if (Has(0))
        {
            snapshot.FuturesEodData.OpenPrice.Should().Be(5400m);
            snapshot.FuturesEodData.ClosePrice.Should().Be(5425m);
            snapshot.FuturesEodData.DailyPercentChange.Should().Be(0.0046);
        }
        (snapshot.FuturesRsiSignal is not null).Should().Be(Has(1));
        (snapshot.FuturesTdiSignal is not null).Should().Be(Has(2));
        (snapshot.TrendDirectionChange is not null).Should().Be(Has(3));
        (snapshot.TrendExtremeChange is not null).Should().Be(Has(4));
        (snapshot.TrendReversalChange is not null).Should().Be(Has(5));
        (snapshot.VixFuturesPrice > 0).Should().Be(Has(6));
        (snapshot.FuturesTradeSignal is not null).Should().Be(Has(0),
            "EOD is the calculation base, while enrichment availability is OR-composed");
        snapshot.IsComplete.Should().Be(mask == 127);

        void Observe(
            FuturesRsiSignalReadModel? rsi = null,
            FuturesTdiSignalReadModel? tdi = null,
            FuturesItiSignalV2ReadModel? iti = null,
            decimal vix = 0)
        {
            var sourceId = Guid.NewGuid();
            new ObserveMarketOutlookComponentCommand(
                entityId,
                sourceId,
                ++sequence,
                timestamp.AddSeconds(sequence),
                "availability-verification",
                rsi,
                tdi,
                iti,
                vix)
            {
                CommandId = sourceId,
                Subject = Subject(ObserveMarketOutlookComponentCommand.Verb)
            }.Execute(state).Success.Should().BeTrue();
        }

        bool Has(int bit) => (mask & (1 << bit)) != 0;
        ActorSubject Subject(string verb) => new(
            ActorType.Command,
            ObserveMarketOutlookComponentCommand.Actor,
            verb,
            entityId.Format());
    }
}
