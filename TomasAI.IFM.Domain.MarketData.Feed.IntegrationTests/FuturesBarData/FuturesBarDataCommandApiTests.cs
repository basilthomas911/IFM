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

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesBarData;

public class FuturesBarDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesBarData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesBarDataInsertedEvent futuresBarDataInsertedEvent = default!;
        FuturesBarDataInsertedCompleteEvent futuresBarDataInsertedCompleteEvent = default!;
        FuturesBarDataInsertedFailEvent futuresBarDataInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesBarDataInsertedEvent.Actor)] = [FuturesBarDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresBarData = SampleData.FuturesBarData;
        await dbFixture.MarketDataDb.DeleteFuturesBarDataAsync(futuresBarData.Id);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesBarDataAsync(futuresBarData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresBarDataInsertedEvent.Should().NotBeNull();
        futuresBarDataInsertedCompleteEvent.Should().NotBeNull();
        futuresBarDataInsertedFailEvent.Should().BeNull();

        var insertedBarData = await dbFixture.MarketDataDb.GetFuturesBarDataAsync(
            futuresBarData.ContractId, futuresBarData.Symbol, futuresBarData.ValueDate,
            futuresBarData.BarDate.AddMinutes(-1), futuresBarData.BarDate.AddMinutes(1));
        insertedBarData.Should().NotBeNullOrEmpty();
        var barDataRecord = insertedBarData.First(e => e.ContractId == futuresBarData.ContractId);
        barDataRecord.Should().NotBeNull();
        barDataRecord.ContractId.Should().Be(futuresBarData.ContractId);
        barDataRecord.Symbol.Should().Be(futuresBarData.Symbol);
        barDataRecord.ValueDate.Should().Be(futuresBarData.ValueDate);
        barDataRecord.BarValue.Should().Be(futuresBarData.BarValue);
        barDataRecord.BarRateType.Should().Be(futuresBarData.BarRateType);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesBarDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataInsertedEvent>()!),
                _ when eventVerb == FuturesBarDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesBarDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesBarDataInsertedEvent inserted)
                    futuresBarDataInsertedEvent = inserted;
                if (@event is FuturesBarDataInsertedCompleteEvent insertedComplete)
                    futuresBarDataInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesBarDataInsertedFailEvent insertedFail)
                    futuresBarDataInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StartFuturesBarDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesBarDataStreamingStartedEvent futuresBarDataStreamingStartedEvent = default!;
        FuturesBarDataStreamingStartedCompleteEvent futuresBarDataStreamingStartedCompleteEvent = default!;
        FuturesBarDataStreamingStartedFailEvent futuresBarDataStreamingStartedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesBarDataStreamingStartedEvent.Actor)] = [FuturesBarDataStreamingStartedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresContracts = new[] { SampleData.FuturesContract };
        var valueDate = SampleData.ValueDate;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StartFuturesBarDataStreamingAsync(futuresContracts, valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresBarDataStreamingStartedEvent.Should().NotBeNull();
        futuresBarDataStreamingStartedCompleteEvent.Should().NotBeNull();
        futuresBarDataStreamingStartedFailEvent.Should().BeNull();

        futuresBarDataStreamingStartedEvent.ValueDate.Should().Be(valueDate);
        futuresBarDataStreamingStartedEvent.Contracts.Should().NotBeNullOrEmpty();
        futuresBarDataStreamingStartedEvent.Contracts!.Length.Should().Be(futuresContracts.Length);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesBarDataStreamingStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStartedEvent>()!),
                _ when eventVerb == FuturesBarDataStreamingStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStartedCompleteEvent>()!),
                _ when eventVerb == FuturesBarDataStreamingStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStartedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesBarDataStreamingStartedEvent started)
                    futuresBarDataStreamingStartedEvent = started;
                if (@event is FuturesBarDataStreamingStartedCompleteEvent startedComplete)
                    futuresBarDataStreamingStartedCompleteEvent = startedComplete;
                if (@event is FuturesBarDataStreamingStartedFailEvent startedFail)
                    futuresBarDataStreamingStartedFailEvent = startedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task DeleteFuturesBarData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesBarDataDeletedEvent futuresBarDataDeletedEvent = default!;
        FuturesBarDataDeletedCompleteEvent futuresBarDataDeletedCompleteEvent = default!;
        FuturesBarDataDeletedFailEvent futuresBarDataDeletedFailEvent = default!;

        // insert futures bar data via command api...
        var futuresBarData = SampleData.FuturesBarData;
        await dbFixture.MarketDataDb.DeleteFuturesBarDataAsync(futuresBarData.Id);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var insertResponse = await marketDataFeedApi.InsertFuturesBarDataAsync(futuresBarData);

        await Task.Delay(1000);

        // assert insert succeeded...
        insertResponse.Should().NotBeNull();
        insertResponse.Success.Should().BeTrue();
        insertResponse.Value.Should().NotBe(Guid.Empty);

        // verify bar data exists before delete...
        var existingBarData = await dbFixture.MarketDataDb.GetFuturesBarDataAsync(
            futuresBarData.ContractId, futuresBarData.Symbol, futuresBarData.ValueDate,
            futuresBarData.BarDate.AddMinutes(-1), futuresBarData.BarDate.AddMinutes(1));
        existingBarData.Should().NotBeNullOrEmpty();

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesBarDataDeletedEvent.Actor)] = [FuturesBarDataDeletedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresBarDataId = futuresBarData.Id;

        // act...
        var response = await marketDataFeedApi.DeleteFuturesBarDataAsync(futuresBarDataId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresBarDataDeletedEvent.Should().NotBeNull();
        futuresBarDataDeletedCompleteEvent.Should().NotBeNull();
        futuresBarDataDeletedFailEvent.Should().BeNull();

        var deletedBarData = await dbFixture.MarketDataDb.GetFuturesBarDataAsync(
            futuresBarDataId.ContractId, futuresBarDataId.Symbol, futuresBarDataId.ValueDate,
            futuresBarData.BarDate.AddMinutes(-1), futuresBarData.BarDate.AddMinutes(1));
        deletedBarData.Should().BeNullOrEmpty();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesBarDataDeletedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataDeletedEvent>()!),
                _ when eventVerb == FuturesBarDataDeletedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataDeletedCompleteEvent>()!),
                _ when eventVerb == FuturesBarDataDeletedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataDeletedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesBarDataDeletedEvent deleted)
                    futuresBarDataDeletedEvent = deleted;
                if (@event is FuturesBarDataDeletedCompleteEvent deletedComplete)
                    futuresBarDataDeletedCompleteEvent = deletedComplete;
                if (@event is FuturesBarDataDeletedFailEvent deletedFail)
                    futuresBarDataDeletedFailEvent = deletedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopFuturesBarDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesBarDataStreamingStoppedEvent futuresBarDataStreamingStoppedEvent = default!;
        FuturesBarDataStreamingStoppedCompleteEvent futuresBarDataStreamingStoppedCompleteEvent = default!;
        FuturesBarDataStreamingStoppedFailEvent futuresBarDataStreamingStoppedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesBarDataStreamingStoppedEvent.Actor)] = [FuturesBarDataStreamingStoppedEvent.Verb]
            },
            EventHandlerAsync
        );

        var valueDate = SampleData.ValueDate;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StopFuturesBarDataStreamingAsync(valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresBarDataStreamingStoppedEvent.Should().NotBeNull();
        futuresBarDataStreamingStoppedCompleteEvent.Should().NotBeNull();
        futuresBarDataStreamingStoppedFailEvent.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesBarDataStreamingStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStoppedEvent>()!),
                _ when eventVerb == FuturesBarDataStreamingStoppedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStoppedCompleteEvent>()!),
                _ when eventVerb == FuturesBarDataStreamingStoppedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesBarDataStreamingStoppedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesBarDataStreamingStoppedEvent stopped)
                    futuresBarDataStreamingStoppedEvent = stopped;
                if (@event is FuturesBarDataStreamingStoppedCompleteEvent stoppedComplete)
                    futuresBarDataStreamingStoppedCompleteEvent = stoppedComplete;
                if (@event is FuturesBarDataStreamingStoppedFailEvent stoppedFail)
                    futuresBarDataStreamingStoppedFailEvent = stoppedFail;
                return @event;
            }
        }
    }
}
