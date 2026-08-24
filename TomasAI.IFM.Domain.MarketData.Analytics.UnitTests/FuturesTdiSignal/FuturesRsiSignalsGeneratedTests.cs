using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;
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
        var commandApi = Substitute.For<IEventActorContext>();
        commandApi.RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(
                Arg.Any<GenerateFuturesTdiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var context = Substitute.For<IEventActorContext>();
        var logger = Substitute.For<ILogger>();
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

        var handled = await @event.ExecuteAsync(context, commandApi, logger);

        handled.Should().BeTrue();
        await commandApi.Received(1)
            .RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(
                Arg.Is<GenerateFuturesTdiSignalCommand>(command =>
                    command.FuturesTdiSignalId.ContractId == SampleData.ContractId
                    && command.FuturesTdiSignalId.TimePeriod == TimeFrameType.OneMinute
                    && command.FuturesTdiSignalId.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId
                    && command.FuturesRsiSignals.Length == 34
                    && command.Configuration.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId
                    && command.CommandId == eventId));
    }

    [Fact]
    public async Task ExecuteAsync_NonIntradayRsiEvent_IsIgnored()
    {
        var commandApi = Substitute.For<IEventActorContext>();
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

        var handled = await @event.ExecuteAsync(
            Substitute.For<IEventActorContext>(),
            commandApi,
            Substitute.For<ILogger>());

        handled.Should().BeTrue();
        await commandApi.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(default!);
    }
}
