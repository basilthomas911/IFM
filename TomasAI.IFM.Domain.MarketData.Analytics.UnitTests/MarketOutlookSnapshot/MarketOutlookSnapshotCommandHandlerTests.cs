using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotCommandHandlerTests
{
    [Fact]
    public void Observe_DuplicateAndOlderSources_DoNotCreateAdditionalEvents()
    {
        var entityId = EntityId();
        var sourceId = Guid.NewGuid();
        var sourceTimestamp = DateTime.UtcNow;
        var state = new MarketOutlookSnapshotCommandState();
        var command = ObserveRsi(entityId, sourceId, 20, sourceTimestamp);

        command.Execute(state).Success.Should().BeTrue();
        command.Execute(state).Success.Should().BeTrue();
        ObserveRsi(entityId, Guid.NewGuid(), 19, sourceTimestamp.AddSeconds(1))
            .Execute(state).Success.Should().BeTrue();

        state.Events.Should().ContainSingle();
        state.WorkingState.Revision.Should().Be(1);
        state.WorkingState.SourceWatermarks.Should().ContainSingle(watermark =>
            watermark.ComponentType == MarketOutlookComponentType.Rsi
            && watermark.SourceEventId == sourceId
            && watermark.SourceEventSequence == 20);
    }

    [Fact]
    public void Observe_ItiWithVix_UpdatesTwoBoundedComponentWatermarksInOneTransition()
    {
        var entityId = EntityId();
        var sourceId = Guid.NewGuid();
        var state = new MarketOutlookSnapshotCommandState();
        var iti = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged
        };
        var command = new ObserveMarketOutlookComponentCommand(
            entityId,
            sourceId,
            31,
            DateTime.UtcNow,
            "iti",
            futuresItiSignal: iti,
            vixFuturesPrice: 21.5m)
        {
            CommandId = sourceId,
            Subject = Subject(ObserveMarketOutlookComponentCommand.Verb, entityId)
        };

        command.Execute(state).Success.Should().BeTrue();

        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<MarketOutlookComponentObservedEvent>();
        state.WorkingState.TrendDirectionChange.Should().Be(iti);
        state.WorkingState.VixFuturesPrice.Should().Be(21.5m);
        state.WorkingState.SourceWatermarks.Select(static value => value.ComponentType)
            .Should().BeEquivalentTo([
                MarketOutlookComponentType.ItiDirection,
                MarketOutlookComponentType.Vix
            ]);
        state.WorkingState.PublishedSnapshot.Should().NotBeNull();
        state.WorkingState.PublishedSnapshot!.TrendDirectionChange.Should().Be(iti);
        state.WorkingState.PublishedSnapshot.VixFuturesPrice.Should().Be(21.5m);
        state.WorkingState.PublishedSnapshot.FuturesEodData.IsValid.Should().BeFalse();
        state.WorkingState.PublishedSnapshot.FuturesTradeSignal.Should().BeNull();
    }

    [Fact]
    public void Observe_MixedComposite_AcceptsEveryValidSiblingAndIgnoresOnlyInvalidSibling()
    {
        var entityId = EntityId();
        var state = new MarketOutlookSnapshotCommandState();
        var sourceId = Guid.NewGuid();
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
        var invalidIti = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTimeMode = IntrinsicTimeModeType.Trending
        };
        var command = new ObserveMarketOutlookComponentCommand(
            entityId,
            sourceId,
            32,
            DateTime.UtcNow,
            "mixed-composite",
            rsi,
            tdi,
            invalidIti,
            23m)
        {
            CommandId = sourceId,
            Subject = Subject(ObserveMarketOutlookComponentCommand.Verb, entityId)
        };

        command.Execute(state).Success.Should().BeTrue();

        state.Events.Should().ContainSingle();
        state.WorkingState.FuturesRsiSignal.Should().Be(rsi);
        state.WorkingState.FuturesTdiSignal.Should().Be(tdi);
        state.WorkingState.TrendDirectionChange.Should().BeNull();
        state.WorkingState.VixFuturesPrice.Should().Be(23m);
        state.WorkingState.SourceWatermarks.Select(value => value.ComponentType).Should()
            .BeEquivalentTo([
                MarketOutlookComponentType.Rsi,
                MarketOutlookComponentType.Tdi,
                MarketOutlookComponentType.Vix
            ]);
    }

    [Fact]
    public void Publish_CreatesFullCheckpointAndDuplicateIsIdempotent()
    {
        var entityId = EntityId();
        var state = new MarketOutlookSnapshotCommandState();
        var sourceId = Guid.NewGuid();
        var eod = SampleData.EodData with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Symbol = "ES"
        };
        var command = new PublishMarketOutlookSnapshotCommand(
            entityId,
            sourceId,
            42,
            DateTime.UtcNow,
            eod)
        {
            CommandId = sourceId,
            Subject = Subject(PublishMarketOutlookSnapshotCommand.Verb, entityId)
        };

        command.Execute(state).Success.Should().BeTrue();
        command.Execute(state).Success.Should().BeTrue();

        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<MarketOutlookSnapshotPublishedEvent>();
        state.WorkingState.Revision.Should().Be(1);
        state.WorkingState.Status.Should().Be(MarketOutlookStateStatus.Published);
        state.WorkingState.PublishedSnapshot.Should().NotBeNull();
        state.WorkingState.PublishedSnapshot!.MissingInputs.Should().Contain("RSI");
        state.WorkingState.PublishedSnapshot.FuturesTradeSignal.Should().NotBeNull(
            "EOD is sufficient to compute an explicitly partial composite");
        state.WorkingState.SourceWatermarks.Should().ContainSingle(watermark =>
            watermark.ComponentType == MarketOutlookComponentType.Eod
            && watermark.SourceEventId == sourceId);
    }

    [Fact]
    public void Observe_AfterEodPublication_ReprojectsSnapshotWithNewRevision()
    {
        var entityId = EntityId();
        var state = new MarketOutlookSnapshotCommandState();
        var eodSource = Guid.NewGuid();
        var eod = SampleData.EodData with
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Symbol = "ES"
        };
        new PublishMarketOutlookSnapshotCommand(
            entityId,
            eodSource,
            1,
            DateTime.UtcNow.AddMinutes(-1),
            eod)
        {
            CommandId = eodSource,
            Subject = Subject(PublishMarketOutlookSnapshotCommand.Verb, entityId)
        }.Execute(state).Success.Should().BeTrue();

        var componentSource = Guid.NewGuid();
        ObserveRsi(entityId, componentSource, 2, DateTime.UtcNow)
            .Execute(state).Success.Should().BeTrue();

        state.Events.Should().HaveCount(2);
        state.Events[^1].Should().BeOfType<MarketOutlookComponentObservedEvent>();
        state.WorkingState.Status.Should().Be(MarketOutlookStateStatus.Published);
        state.WorkingState.PublishedSnapshot.Should().NotBeNull();
        state.WorkingState.PublishedSnapshot!.Revision.Should().Be(2);
        state.WorkingState.PublishedSnapshot.FuturesEodData.Should().Be(eod);
        state.WorkingState.PublishedSnapshot.MissingInputs.Should().NotContain("RSI");
    }

    static ObserveMarketOutlookComponentCommand ObserveRsi(
        MarketOutlookEntityId entityId,
        Guid sourceId,
        long sourceSequence,
        DateTime sourceTimestamp)
        => new(
            entityId,
            sourceId,
            sourceSequence,
            sourceTimestamp,
            "rsi",
            futuresRsiSignal: SampleData.AtrRsiSignals[0] with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.FifteenSeconds,
                PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength
            })
        {
            CommandId = sourceId,
            Subject = Subject(ObserveMarketOutlookComponentCommand.Verb, entityId)
        };

    static MarketOutlookEntityId EntityId()
        => new("ESU26", new DateOnly(2026, 8, 21));

    static ActorSubject Subject(string verb, MarketOutlookEntityId entityId)
        => new(
            ActorType.Command,
            ObserveMarketOutlookComponentCommand.Actor,
            verb,
            entityId.Format());
}
