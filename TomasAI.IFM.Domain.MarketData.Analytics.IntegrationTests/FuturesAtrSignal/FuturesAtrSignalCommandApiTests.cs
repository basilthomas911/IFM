using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAtrSignal;

public class FuturesAtrSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesAtrSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesAtrSignalGeneratedEvent futuresAtrSignalGeneratedEvent = default!;
        FuturesAtrSignalGeneratedCompleteEvent futuresAtrSignalGeneratedCompleteEvent = default!;
        FuturesAtrSignalGeneratedFailEvent futuresAtrSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesAtrSignalGeneratedEvent.Actor)] = [FuturesAtrSignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var atrSignalId = SampleData.AtrSignalId;
        var futuresItiSignals = SampleData.CreateItiSignalsForAtr();

        var entityId = SampleData.AtrEntityId;
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.MarketDataDb.DeleteFuturesAtrSignalAsync(contractId, valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await marketDataAnalyticsApi.GenerateFuturesAtrSignalAsync(atrSignalId, futuresItiSignals);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresAtrSignalGeneratedEvent.Should().NotBeNull();
        futuresAtrSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresAtrSignalGeneratedFailEvent.Should().BeNull();

        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.Should().NotBeNull();
        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.ContractId.Should().Be(contractId);
        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.ValueDate.Should().Be(valueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesAtrSignalAsync(contractId, valueDate, atrSignalId.TimePeriod, atrSignalId.PeriodLength);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesAtrSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesAtrSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesAtrSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesAtrSignalGeneratedEvent generated)
                    futuresAtrSignalGeneratedEvent = generated;
                if (@event is FuturesAtrSignalGeneratedCompleteEvent generatedComplete)
                    futuresAtrSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesAtrSignalGeneratedFailEvent generatedFail)
                    futuresAtrSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }
}
