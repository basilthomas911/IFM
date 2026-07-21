using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTdiSignal;

public class FuturesTdiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesTdiSignal_Ok()
    {
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTdiSignalGeneratedEvent futuresTdiSignalGeneratedEvent = default!;
        FuturesTdiSignalGeneratedCompleteEvent futuresTdiSignalGeneratedCompleteEvent = default!;
        FuturesTdiSignalGeneratedFailEvent futuresTdiSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor)] =
                [
                    FuturesTdiSignalGeneratedEvent.Verb,
                    FuturesTdiSignalGeneratedCompleteEvent.Verb,
                    FuturesTdiSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync);

        var valueDate = new DateOnly(2099, 12, 31);
        var timestamp = new TimeOnly(10, 0, 0);
        var futuresTdiSignalId = new FuturesTdiSignalId(SampleData.ContractId, valueDate, timestamp);
        var entityId = new FuturesTdiSignalEntityId(SampleData.ContractId, valueDate, TradeTimePeriodType.Daily);
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await analyticsApi.GenerateFuturesTdiSignalAsync(futuresTdiSignalId, CreateRsiSignals(valueDate));

        await Task.Delay(1000);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresTdiSignalGeneratedEvent.Should().NotBeNull();
        futuresTdiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresTdiSignalGeneratedFailEvent.Should().BeNull();
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Should().NotBeNull();
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ValueDate.Should().Be(valueDate);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.TimePeriod.Should().Be(TradeTimePeriodType.Daily);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Timestamp.Should().Be(timestamp);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(SampleData.ContractId, valueDate);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(valueDate);
        lastSignal.Timestamp.Should().Be(timestamp);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesTdiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesTdiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesTdiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTdiSignalGeneratedEvent generated)
                    futuresTdiSignalGeneratedEvent = generated;
                if (@event is FuturesTdiSignalGeneratedCompleteEvent generatedComplete)
                    futuresTdiSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesTdiSignalGeneratedFailEvent generatedFail)
                    futuresTdiSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }

    static FuturesRsiSignalReadModel[] CreateRsiSignals(DateOnly valueDate)
        =>
        [
            new(
                SampleData.ContractId,
                valueDate,
                SampleData.RSITimePeriod,
                14,
                new TimeOnly(9, 58, 0),
                5500m,
                1m,
                1m,
                0m,
                0.5m,
                0.2m,
                2.5,
                56,
                54,
                0.2),
            new(
                SampleData.ContractId,
                valueDate,
                SampleData.RSITimePeriod,
                14,
                new TimeOnly(9, 59, 0),
                5501m,
                1m,
                1m,
                0m,
                0.6m,
                0.2m,
                3.0,
                58,
                55,
                0.3),
            new(
                SampleData.ContractId,
                valueDate,
                SampleData.RSITimePeriod,
                14,
                new TimeOnly(10, 0, 0),
                5502m,
                1m,
                1m,
                0m,
                0.7m,
                0.2m,
                3.5,
                60,
                56,
                0.4)
        ];
}
