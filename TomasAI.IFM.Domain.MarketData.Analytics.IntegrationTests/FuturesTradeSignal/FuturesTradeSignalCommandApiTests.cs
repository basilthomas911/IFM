using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTradeSignal;

public class FuturesTradeSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task UpdateFuturesTradeSignal_Ok()
    {
        // arrange...
        const string tradeSignalUpdatedCompleteVerb = "TradeSignalUpdatedComplete";

        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTradeSignalUpdatedCompleteEvent futuresTradeSignalUpdatedCompleteEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTradeSignalEventActor.Actor)] = [tradeSignalUpdatedCompleteVerb]
            },
            EventHandlerAsync);

        var entityId = new FuturesTradeSignalEntityId(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.FifteenSeconds);
        var subject = new ActorSubject(ActorType.Command, UpdateFuturesTradeSignalCommand.Actor, UpdateFuturesTradeSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await analyticsApi.UpdateFuturesTradeSignalAsync(
            SampleData.FuturesEodData,
            CreateRsiSignal(),
            CreateTdiSignal(),
            CreateItiSignalData(),
            20m);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresTradeSignalUpdatedCompleteEvent.Should().NotBeNull();
        futuresTradeSignalUpdatedCompleteEvent.FuturesTradeSignal.Should().NotBeNull();
        futuresTradeSignalUpdatedCompleteEvent.FuturesTradeSignal!.ContractId.Should().Be(SampleData.ContractId);
        futuresTradeSignalUpdatedCompleteEvent.FuturesTradeSignal.ValueDate.Should().Be(SampleData.ValueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesTradeSignalAsync(SampleData.ContractId, SampleData.ValueDate);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(SampleData.ValueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == tradeSignalUpdatedCompleteVerb => SetEvent(eventMsg.AsEvent<FuturesTradeSignalUpdatedCompleteEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTradeSignalUpdatedCompleteEvent updatedComplete)
                    futuresTradeSignalUpdatedCompleteEvent = updatedComplete;
                return @event;
            }
        }
    }

    static FuturesRsiSignalReadModel CreateRsiSignal()
        => new(
            SampleData.ContractId,
            SampleData.ValueDate,
            SampleData.RSITimePeriod,
            14,
            TimeOnly.FromDateTime(SampleData.Timestamp),
            (decimal)SampleData.FuturesPrice,
            1m,
            1m,
            0m,
            0.5m,
            0.3m,
            1.67,
            SampleData.FuturesRSI,
            SampleData.FuturesRSI,
            SampleData.FuturesRSISlope);

    static FuturesTdiSignalReadModel CreateTdiSignal()
        => new(
            SampleData.ContractId,
            SampleData.ValueDate,
            TradeTimePeriodType.FifteenSeconds,
            TimeOnly.FromDateTime(SampleData.Timestamp),
            2,
            1,
            FuturesTrendDirectionType.UpTrending,
            FuturesTrendDirectionStrengthType.Medium);

    static FuturesItiSignalDataReadModel CreateItiSignalData()
        => new(
            SampleData.StartOfDayEvent.FuturesItiSignal,
            SampleData.StartOfDayEvent.FuturesItiSignal,
            SampleData.StartOfDayEvent.FuturesItiSignal);
}
