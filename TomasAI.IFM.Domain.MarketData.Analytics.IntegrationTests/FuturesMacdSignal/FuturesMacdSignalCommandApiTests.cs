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
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesMacdSignal;

public class FuturesMacdSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesMacdSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesMacdSignalGeneratedEvent futuresMacdSignalGeneratedEvent = default!;
        FuturesMacdSignalGeneratedCompleteEvent futuresMacdSignalGeneratedCompleteEvent = default!;
        FuturesMacdSignalGeneratedFailEvent futuresMacdSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesMacdSignalGeneratedEvent.Actor)] = [FuturesMacdSignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var macdSignalId = SampleData.MacdSignalId;
        var futuresPrice = (decimal)SampleData.FuturesPrice;

        var entityId = SampleData.MacdEntityId;
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await marketDataAnalyticsApi.GenerateFuturesMacdSignalAsync(macdSignalId, futuresPrice);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresMacdSignalGeneratedEvent.Should().NotBeNull();
        futuresMacdSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresMacdSignalGeneratedFailEvent.Should().BeNull();

        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.Should().NotBeNull();
        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.ContractId.Should().Be(contractId);
        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.ValueDate.Should().Be(valueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(contractId, valueDate, SampleData.TimePeriod, SampleData.PeriodLength);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesMacdSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesMacdSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesMacdSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesMacdSignalGeneratedEvent generated)
                    futuresMacdSignalGeneratedEvent = generated;
                if (@event is FuturesMacdSignalGeneratedCompleteEvent generatedComplete)
                    futuresMacdSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesMacdSignalGeneratedFailEvent generatedFail)
                    futuresMacdSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }
}
