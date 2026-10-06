using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public sealed class FuturesRsiSignalsGeneratedTests
{
    [Fact]
    public async Task ExecuteAsync_StandardIntradayWindow_SendsDeterministicTdiCommand()
    {
        var context = Substitute.For<IEventActorContext<FuturesTdiSignalEventActor>>();
        context.RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(
                Arg.Any<GenerateFuturesTdiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var logger = Substitute.For<ILogger<FuturesTdiSignalEventActor>>();
        var eventId = Guid.NewGuid();
        var @event = new FuturesRsiSignalsGeneratedEvent
        {
            Id = eventId,
            CommandId = Guid.NewGuid(),
            EntityId = new FuturesRsiSignalEntityId(
                SampleData.ContractId,
                SampleData.ValueDate,
                TimeFrameType.OneMinute,
                FuturesTdiConfiguration.Standard.RsiPeriod),
            PeriodLength = FuturesTdiConfiguration.Standard.RsiPeriod,
            FuturesRsiSignals = SampleData.TdiRsiSignals
        };

        var handled = await @event.ExecuteAsync(context, logger);

        handled.Should().BeTrue();
        await context.Received(1)
            .RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(
                Arg.Is<GenerateFuturesTdiSignalCommand>(command =>
                    command.FuturesTdiSignalId.ContractId == SampleData.ContractId
                    && command.FuturesTdiSignalId.TimePeriod == TimeFrameType.OneMinute
                    && command.FuturesTdiSignalId.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId
                    && command.FuturesRsiSignals.Length == 34
                    && command.Configuration.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId
                    && command.CommandId != eventId
                    && command.CommandId != @event.CommandId
                    && command.CommandId != Guid.Empty));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyEventId_DerivesAStableCommandIdDistinctFromRsiStart()
    {
        var context = Substitute.For<IEventActorContext<FuturesTdiSignalEventActor>>();
        context.RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(
                Arg.Any<GenerateFuturesTdiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var rsiCommandId = Guid.NewGuid();
        var @event = new FuturesRsiSignalsGeneratedEvent
        {
            CommandId = rsiCommandId,
            EntityId = new FuturesRsiSignalEntityId(
                SampleData.ContractId, SampleData.ValueDate, TimeFrameType.FiveMinutes, 13),
            PeriodLength = 13,
            FuturesRsiSignals = SampleData.TdiRsiSignals
                .Select(signal => signal with { TimePeriod = TimeFrameType.FiveMinutes }).ToArray()
        };
        var logger = Substitute.For<ILogger<FuturesTdiSignalEventActor>>();

        Assert.True(await @event.ExecuteAsync(context, logger));
        Assert.True(await @event.ExecuteAsync(context, logger));
        var sent = context.ReceivedCalls()
            .Select(call => call.GetArguments().OfType<GenerateFuturesTdiSignalCommand>().FirstOrDefault())
            .Where(command => command is not null).ToArray();
        Assert.Equal(2, sent.Length);
        Assert.NotEqual(rsiCommandId, sent[0]!.CommandId);
        Assert.NotEqual(Guid.Empty, sent[0].CommandId);
        Assert.Equal(sent[0].CommandId, sent[1]!.CommandId);
    }

    [Fact]
    public async Task ExecuteAsync_SeedWindowAcrossEveningRollover_GeneratesAllTdiValues()
    {
        var context = Substitute.For<IEventActorContext<FuturesTdiSignalEventActor>>();
        context.RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(Arg.Any<GenerateFuturesTdiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var date = SampleData.ValueDate.AddDays(1);
        var source = new FuturesRsiSignalsGeneratedEvent
        {
            CommandId = Guid.NewGuid(),
            EntityId = new(SampleData.ContractId, date, TimeFrameType.FiveMinutes, 13),
            PeriodLength = 13,
            FuturesRsiSignals = SampleData.TdiRsiSignals.Select((signal, index) => signal with
            {
                ValueDate = index < 28 ? date.AddDays(-1) : date,
                TimePeriod = TimeFrameType.FiveMinutes,
                SourceEventTimestamp = date.ToDateTime(new TimeOnly(22, 0)).AddMinutes(index * 5)
            }).Reverse().ToArray()
        };
        Assert.True(await source.ExecuteAsync(context, Substitute.For<ILogger<FuturesTdiSignalEventActor>>()));
        var command = Assert.Single(context.ReceivedCalls().SelectMany(call => call.GetArguments().OfType<GenerateFuturesTdiSignalCommand>()));
        Assert.Equal(34, command.FuturesRsiSignals.Length);
        var state = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State.FuturesTdiSignalCommandState();
        Assert.True(TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.GenerateFuturesTdiSignal.Execute(command, state).Success);
        var generated = Assert.Single(state.Events.OfType<FuturesTdiSignalGeneratedEvent>());
        Assert.Equal(date, generated.EntityId.ValueDate);
        Assert.True(double.IsFinite(generated.FuturesTdiSignal.PriceLine));
        Assert.True(double.IsFinite(generated.FuturesTdiSignal.SignalLine));
        Assert.True(double.IsFinite(generated.FuturesTdiSignal.MarketBaseLine));
        Assert.True(double.IsFinite(generated.FuturesTdiSignal.UpperVolatilityBand));
        Assert.True(double.IsFinite(generated.FuturesTdiSignal.LowerVolatilityBand));
    }
    [Fact]
    public async Task ExecuteAsync_NonIntradayRsiEvent_IsIgnored()
    {
        var context = Substitute.For<IEventActorContext<FuturesTdiSignalEventActor>>();
        var @event = new FuturesRsiSignalsGeneratedEvent
        {
            EntityId = new FuturesRsiSignalEntityId(
                SampleData.ContractId,
                SampleData.ValueDate,
                TimeFrameType.Daily,
                13),
            PeriodLength = 13,
            FuturesRsiSignals = SampleData.TdiRsiSignals
                .Select(signal => signal with { TimePeriod = TimeFrameType.Daily })
                .ToArray()
        };

        var handled = await @event.ExecuteAsync(context, Substitute.For<ILogger<FuturesTdiSignalEventActor>>());

        handled.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(default!);
    }
}
