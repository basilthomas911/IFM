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
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesEodData;

public class FuturesEodDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesEodData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesEodDataInsertedEvent futuresEodDataInsertedEvent = default!;
        FuturesEodDataInsertedCompleteEvent futuresEodDataInsertedCompleteEvent = default!;
        FuturesEodDataInsertedFailEvent futuresEodDataInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesEodDataInsertedEvent.Actor)] = [FuturesEodDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var valueDate = SampleData.ValueDate;
        var contractId = SampleData.FuturesContractId;
        var futuresTickData = SampleData.UnderlyingFuturesTickData;
        var contract = SampleData.FuturesContract;
        var eodDataToday = SampleData.FuturesEodDataRange[0];
        var eodDataRange = SampleData.FuturesEodDataRange;
        var normCurveData = new NormalCurveTableReadModel([new NormalCurveDataReadModel(0, 50.0)]);
        var windowSize = 20;
        var vixEodData = Array.Empty<VixFuturesEodDataReadModel>();
        var futuresDataId = new FuturesDataId(contractId, valueDate);

        var yesterDaysClosingPrice = (await dbFixture.MarketDataDb.GetYesterdaysFuturesClosingPriceAsync(futuresDataId))?.ClosingPrice;
        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesEodDataAsync(
            valueDate, futuresTickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresEodDataInsertedEvent.Should().NotBeNull();
        futuresEodDataInsertedCompleteEvent.Should().NotBeNull();
        futuresEodDataInsertedFailEvent.Should().BeNull();

        var insertedEodData = await dbFixture.MarketDataDb.GetFuturesEodDataAsync(contractId, valueDate);
        insertedEodData.Should().NotBeNull();
        insertedEodData!.ContractId.Should().Be(contractId);
        insertedEodData.ValueDate.Should().Be(valueDate);
        insertedEodData.Symbol.Should().Be(eodDataToday.Symbol);
        insertedEodData.OpenPrice.Should().Be(yesterDaysClosingPrice);
        insertedEodData.HighPrice.Should().Be(eodDataToday.HighPrice);
        insertedEodData.LowPrice.Should().Be(eodDataToday.LowPrice);
        insertedEodData.ClosePrice.Should().Be(eodDataToday.ClosePrice);
        insertedEodData.Volume.Should().Be(eodDataToday.Volume);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesEodDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedEvent>()!),
                _ when eventVerb == FuturesEodDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesEodDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesEodDataInsertedEvent inserted)
                    futuresEodDataInsertedEvent = inserted;
                if (@event is FuturesEodDataInsertedCompleteEvent insertedComplete)
                    futuresEodDataInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesEodDataInsertedFailEvent insertedFail)
                    futuresEodDataInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertVixFuturesEodData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        VixFuturesEodDataInsertedEvent vixFuturesEodDataInsertedEvent = default!;
        VixFuturesEodDataInsertedCompleteEvent vixFuturesEodDataInsertedCompleteEvent = default!;
        VixFuturesEodDataInsertedFailEvent vixFuturesEodDataInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, VixFuturesEodDataInsertedEvent.Actor)] = [VixFuturesEodDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var valueDate = SampleData.ValueDate;
        var vixContractId = "VXZ25";
        var vixFuturesTickData = new FuturesTickDataV2ReadModel(
            contractId: vixContractId,
            valueDate: valueDate,
            tickId: 1,
            tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
            price: 18.50m,
            size: 100);

        await dbFixture.MarketDataDb.DeleteVixFuturesEodDataAsync(vixContractId, valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertVixFuturesEodDataAsync(vixFuturesTickData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        vixFuturesEodDataInsertedEvent.Should().NotBeNull();
        vixFuturesEodDataInsertedCompleteEvent.Should().NotBeNull();
        vixFuturesEodDataInsertedFailEvent.Should().BeNull();

        var insertedVixEodData = await dbFixture.MarketDataDb.GetVixFuturesEodDataAsync(vixContractId, valueDate);
        insertedVixEodData.Should().NotBeNull();
        insertedVixEodData!.ContractId.Should().Be(vixContractId);
        insertedVixEodData.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == VixFuturesEodDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedEvent>()!),
                _ when eventVerb == VixFuturesEodDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedCompleteEvent>()!),
                _ when eventVerb == VixFuturesEodDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is VixFuturesEodDataInsertedEvent inserted)
                    vixFuturesEodDataInsertedEvent = inserted;
                if (@event is VixFuturesEodDataInsertedCompleteEvent insertedComplete)
                    vixFuturesEodDataInsertedCompleteEvent = insertedComplete;
                if (@event is VixFuturesEodDataInsertedFailEvent insertedFail)
                    vixFuturesEodDataInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }
}
