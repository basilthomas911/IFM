using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
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
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        commandApi.GenerateFuturesTdiSignalAsync(
                Arg.Any<FuturesTdiSignalId>(),
                Arg.Any<FuturesRsiSignalReadModel[]>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<FuturesTdiConfiguration>(),
                Arg.Any<Guid?>())
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
        await commandApi.Received(1).GenerateFuturesTdiSignalAsync(
            Arg.Is<FuturesTdiSignalId>(id =>
                id.ContractId == SampleData.ContractId
                && id.TimePeriod == TimeFrameType.OneMinute
                && id.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId),
            Arg.Is<FuturesRsiSignalReadModel[]>(signals => signals.Length == 34),
            TimeFrameType.OneMinute,
            Arg.Is<FuturesTdiConfiguration>(configuration =>
                configuration.ConfigurationId == FuturesTdiConfiguration.StandardConfigurationId),
            eventId);
    }

    [Fact]
    public async Task ExecuteAsync_NonIntradayRsiEvent_IsIgnored()
    {
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
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
        await commandApi.DidNotReceiveWithAnyArgs().GenerateFuturesTdiSignalAsync(
            default!, default!, default, default, default);
    }
}
