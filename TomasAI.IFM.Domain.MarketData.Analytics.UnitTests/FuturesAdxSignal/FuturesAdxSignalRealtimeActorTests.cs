using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal;

public sealed class FuturesAdxSignalRealtimeActorTests
{
    const string ContractId = "ESZ26-ADX-REALTIME";
    static readonly DateOnly ValueDate = new(2026, 8, 25);

    [Fact]
    public async Task ReceiveAsync_WithMatchingClosedObservation_ForwardsGenerateCommandWithLineage()
    {
        var entityId = new FuturesAdxSignalEntityId(
            ContractId, ValueDate, TimeFrameType.FifteenMinutes, 14);
        var observation = Observation();
        var @event = Closed(observation);
        var commandResult = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var logger = Substitute.For<ILogger<FuturesAdxSignalRealtimeActor>>();
        var context = Context(logger);
        GenerateFuturesAdxSignalCommand? forwarded = null;
        context.RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(
                Arg.Do<GenerateFuturesAdxSignalCommand>(command => forwarded = command))
            .Returns(commandResult);
        var actor = new TestableFuturesAdxSignalRealtimeActor(context);

        FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
        FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(entityId);
        try
        {
            await actor.InvokeReceiveAsync(context, @event);
        }
        finally
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
        }

        forwarded.Should().NotBeNull();
        forwarded!.EntityId.Should().Be(entityId);
        forwarded.FuturesPrice.Should().Be(observation.Close);
        forwarded.Observation.Should().BeSameAs(observation);
        forwarded.FuturesAdxSignalId.Timestamp.Should()
            .Be(TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
        forwarded.Subject.Should().Match<ActorSubject>(subject => subject.Is(
            ActorType.Command,
            GenerateFuturesAdxSignalCommand.Actor,
            GenerateFuturesAdxSignalCommand.Verb));
    }

    [Fact]
    public async Task ReceiveAsync_WithUnmatchedClosedObservation_DoesNotSendCommand()
    {
        var logger = Substitute.For<ILogger<FuturesAdxSignalRealtimeActor>>();
        var context = Context(logger);
        var actor = new TestableFuturesAdxSignalRealtimeActor(context);
        FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
        FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(
            new FuturesAdxSignalEntityId("NQZ26", ValueDate, TimeFrameType.FifteenMinutes, 14));
        try
        {
            await actor.InvokeReceiveAsync(context, Closed(Observation()));
        }
        finally
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
        }

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(default!);
    }

    [Fact]
    public void GenerateCommand_AppliesObservationLineageToCommandOwnedState()
    {
        var observation = Observation();
        var signalId = new FuturesAdxSignalId(
            ContractId,
            ValueDate,
            TimeFrameType.FifteenMinutes,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
        var command = new GenerateFuturesAdxSignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAdxSignalCommand.Actor,
                GenerateFuturesAdxSignalCommand.Verb,
                signalId.ToEntityId().Format())
        };
        var state = new FuturesAdxSignalCommandState { Id = command.Subject.ThreadId };

        var result = command.Execute(state);

        result.Success.Should().BeTrue();
        state.AdxSignal.Metadata.Should().NotBeNull();
        state.AdxSignal.Metadata!.ObservationId.Should().Be(observation.ObservationId);
        state.AdxSignal.Metadata.SourceSequence.Should().Be(observation.LastSourceSequence);
        state.AdxSignal.Metadata.SignalKey.SignalKind.Should().Be(MarketAnalyticsSignalKind.Adx);
        state.AdxSignal.Timestamp.Should()
            .Be(TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
    }

    static IFuturesAdxSignalRealtimeContext Context(
        ILogger<FuturesAdxSignalRealtimeActor> logger)
    {
        var context = Substitute.For<IFuturesAdxSignalRealtimeContext>();
        context.Logger.Returns(logger);
        context.Supervisor.Returns(Substitute.For<IActorSupervisor>());
        return context;
    }

    static FuturesTradeSessionBarClosedRealtimeEvent Closed(
        FuturesTradeSessionBarReadModel observation)
    {
        var entityId = new FuturesTradeSessionBarEntityId(
            observation.MarketSeriesIdentity,
            observation.TimeFrame);
        return new()
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                FuturesTradeSessionBarClosedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = observation.CalculatedAtUtc.UtcDateTime,
            Observation = observation
        };
    }

    static FuturesTradeSessionBarReadModel Observation()
    {
        var series = MarketSeriesIdentity.ForContract(ContractId);
        var start = new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);
        var end = start.AddMinutes(15);
        return new()
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(
                series, TimeFrameType.FifteenMinutes, end, 42),
            ContractId = ContractId,
            ValueDate = ValueDate,
            TimeFrame = TimeFrameType.FifteenMinutes,
            IntervalStartUtc = start,
            IntervalEndUtc = end,
            Open = 6400m,
            High = 6410m,
            Low = 6395m,
            Close = 6405m,
            Volume = 30m,
            TradeCount = 3,
            PriceVolumeSum = 192150m,
            FirstSourceSequence = 40,
            LastSourceSequence = 42,
            FirstMarketEventUtc = start.AddSeconds(1),
            LastMarketEventUtc = end.AddSeconds(-1),
            CalculatedAtUtc = end,
            CalculationVersion = "unit-test-v1",
            IsComplete = true,
            IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
    }

    sealed class TestableFuturesAdxSignalRealtimeActor(
        IRealtimeActorContext<FuturesAdxSignalRealtimeActor> context)
        : FuturesAdxSignalRealtimeActor(context)
    {
        public ValueTask InvokeReceiveAsync(
            IEventActorContext<FuturesAdxSignalRealtimeActor> context,
            IEvent @event) => ReceiveAsync(context, @event);
    }
}
