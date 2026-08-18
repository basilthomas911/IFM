using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class FuturesIntradaySignalRealtimeTests
{
    const string ContractId = "ES-REALTIME-SIGNALS";
    static readonly DateOnly ValueDate = new(2026, 8, 17);

    public static TheoryData<TimeFrameType> IntradayPeriods => new(
        FuturesIntradaySignalActivationProfile.TimeFrames.ToArray());

    [Theory]
    [MemberData(nameof(IntradayPeriods))]
    public void EveryConfiguredPeriod_ComputesRsiAndFeedsTdi(TimeFrameType period)
    {
        var rsiId = FuturesRsiSignalEntityId.Create(
            ContractId, ValueDate, period, FuturesIntradaySignalActivationProfile.RsiPeriodLength);
        var rsiState = new FuturesRsiSignalRealtimeState();
        FuturesRsiSignalsGeneratedEvent? window = null;

        for (var sequence = 1; sequence <= 47; sequence++)
        {
            var sample = RsiSample(rsiId, sequence, 5400m + sequence % 5 - sequence % 3);
            var evaluation = rsiState.Evaluate(sample);
            evaluation.Generated.Subject.ActorType.Should().Be(ActorType.Realtime);
            window = rsiState.Confirm(evaluation) ?? window;
        }

        window.Should().NotBeNull();
        window!.FuturesRsiSignals.Should().HaveCount(FuturesTdiConfiguration.Standard.RequiredRsiSamples);
        window.FuturesRsiSignals.Should().OnlyContain(signal =>
            signal.TimePeriod == period
            && signal.PeriodLength == FuturesTdiConfiguration.Standard.RsiPeriod
            && signal.RSI >= 0d);

        var tdiState = new FuturesTdiSignalRealtimeState();
        var tdi = tdiState.Evaluate(window, window.FuturesRsiSignals, FuturesTdiConfiguration.Standard);
        tdi.Generated.Subject.ActorType.Should().Be(ActorType.Realtime);
        tdi.Signal.TimePeriod.Should().Be(period);
        tdi.Signal.ConfigurationId.Should().Be(FuturesTdiConfiguration.StandardConfigurationId);
        tdi.Signal.SchemaVersion.Should().Be(FuturesTdiConfiguration.CurrentSchemaVersion);
    }

    [Theory]
    [MemberData(nameof(IntradayPeriods))]
    public void EveryConfiguredPeriod_ComputesAtrAdxAndConventionalMacd(TimeFrameType period)
    {
        var activation = FuturesIntradaySignalActivationProfile.Create(ContractId, ValueDate)
            .Single(item => item.TimeFrame == period);
        var timestamp = new DateTime(2026, 8, 17, 14, 30, 15, DateTimeKind.Utc);

        var atrState = new FuturesAtrSignalRealtimeState();
        var atr = atrState.Evaluate(AtrSample(activation.Atr, timestamp));
        atr.Generated.Subject.ActorType.Should().Be(ActorType.Realtime);
        atr.Signal.TimePeriod.Should().Be(period);
        atrState.Confirm(atr);

        var adxState = new FuturesAdxSignalRealtimeState();
        var adx = adxState.Evaluate(AdxSample(activation.Adx, timestamp));
        adx.Generated.Subject.ActorType.Should().Be(ActorType.Realtime);
        adx.Signal.TimePeriod.Should().Be(period);
        adxState.Confirm(adx);

        var macdState = new FuturesMacdSignalRealtimeState();
        var macd = macdState.Evaluate(MacdSample(activation.Macd, timestamp));
        macd.Generated.Subject.ActorType.Should().Be(ActorType.Realtime);
        macd.Signal.TimePeriod.Should().Be(period);
        macd.Signal.SignalEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalSignalEmaPeriod);
        macd.Signal.FastEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalFastEmaPeriod);
        macd.Signal.SlowEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalSlowEmaPeriod);
        macdState.Confirm(macd);
    }

    [Fact]
    public void AllPhaseTwoProjectors_UseTheRealtimeProjectorContract()
    {
        var db = Substitute.For<IDbContextFactory>();
        object[] projectors =
        [
            new FuturesRsiSignalRealtimeProjector(db, Substitute.For<ILogger<FuturesRsiSignalRealtimeProjector>>()),
            new FuturesAtrSignalRealtimeProjector(db, Substitute.For<ILogger<FuturesAtrSignalRealtimeProjector>>()),
            new FuturesAdxSignalRealtimeProjector(db, Substitute.For<ILogger<FuturesAdxSignalRealtimeProjector>>()),
            new FuturesMacdSignalRealtimeProjector(db, Substitute.For<ILogger<FuturesMacdSignalRealtimeProjector>>()),
            new FuturesTdiSignalRealtimeProjector(db, Substitute.For<ILogger<FuturesTdiSignalRealtimeProjector>>())
        ];

        projectors[0].Should().BeAssignableTo<IRealtimeProjector<FuturesRsiSignalRealtimeActor>>();
        projectors[1].Should().BeAssignableTo<IRealtimeProjector<FuturesAtrSignalRealtimeActor>>();
        projectors[2].Should().BeAssignableTo<IRealtimeProjector<FuturesAdxSignalRealtimeActor>>();
        projectors[3].Should().BeAssignableTo<IRealtimeProjector<FuturesMacdSignalRealtimeActor>>();
        projectors[4].Should().BeAssignableTo<IRealtimeProjector<FuturesTdiSignalRealtimeActor>>();
    }

    static FuturesRsiSignalSampledRealtimeEvent RsiSample(
        FuturesRsiSignalEntityId entityId,
        long sequence,
        decimal price)
    {
        var timestamp = new DateTime(2026, 8, 17, 13, 0, 0, DateTimeKind.Utc).AddSeconds(sequence);
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesRsiSignalSampledRealtimeEvent.Actor,
                FuturesRsiSignalSampledRealtimeEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = timestamp,
            FuturesPrice = price,
            SourceSequence = sequence,
            SourceEventTimestamp = timestamp
        };
    }

    static FuturesAtrSignalSampledRealtimeEvent AtrSample(FuturesAtrSignalEntityId entityId, DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesAtrSignalSampledRealtimeEvent.Actor,
            FuturesAtrSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "unit-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };

    static FuturesAdxSignalSampledRealtimeEvent AdxSample(FuturesAdxSignalEntityId entityId, DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesAdxSignalSampledRealtimeEvent.Actor,
            FuturesAdxSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "unit-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };

    static FuturesMacdSignalSampledRealtimeEvent MacdSample(FuturesMacdSignalEntityId entityId, DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesMacdSignalSampledRealtimeEvent.Actor,
            FuturesMacdSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "unit-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };
}
