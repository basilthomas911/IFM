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
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesClosingPrice;

public class FuturesClosingPriceCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesClosingPrice_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesClosingPriceInsertedEvent futuresClosingPriceInsertedEvent = default!;
        FuturesClosingPriceInsertedCompleteEvent futuresClosingPriceInsertedCompleteEvent = default!;
        FuturesClosingPriceInsertedFailEvent futuresClosingPriceInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesClosingPriceInsertedEvent.Actor)] = [FuturesClosingPriceInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.FuturesContractId;
        var valueDate = SampleData.ValueDate;
        var closingPrice = 5450.25m;
        var futuresDataId = FuturesDataId.Create(contractId, valueDate);

        await dbFixture.MarketDataDb.DeleteFuturesClosingPriceAsync(contractId, valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesClosingPriceAsync(futuresDataId, closingPrice);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresClosingPriceInsertedEvent.Should().NotBeNull();
        futuresClosingPriceInsertedCompleteEvent.Should().NotBeNull();
        futuresClosingPriceInsertedFailEvent.Should().BeNull();

        var insertedClosingPrice = await dbFixture.MarketDataDb.GetFuturesClosingPriceAsync(futuresDataId);
        insertedClosingPrice.Should().NotBeNull();
        insertedClosingPrice!.ContractId.Should().Be(contractId);
        insertedClosingPrice.ValueDate.Should().Be(valueDate);
        insertedClosingPrice.ClosingPrice.Should().Be(closingPrice);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesClosingPriceInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedEvent>()!),
                _ when eventVerb == FuturesClosingPriceInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesClosingPriceInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesClosingPriceInsertedEvent inserted)
                    futuresClosingPriceInsertedEvent = inserted;
                if (@event is FuturesClosingPriceInsertedCompleteEvent insertedComplete)
                    futuresClosingPriceInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesClosingPriceInsertedFailEvent insertedFail)
                    futuresClosingPriceInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }
}
