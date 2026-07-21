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
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesOptionTickData;

public class FuturesOptionTickDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesOptionTickData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionTickDataInsertedEvent futuresOptionTickDataInsertedEvent = default!;
        FuturesOptionTickDataInsertedCompleteEvent futuresOptionTickDataInsertedCompleteEvent = default!;
        FuturesOptionTickDataInsertedFailEvent futuresOptionTickDataInsertedFailEvent = default!;
        OptionTradeTickPriceDataUpdatedEvent futuresOptionTickDataUpdatedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataInsertedEvent.Actor)] = [FuturesOptionTickDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var optionTickData = SampleData.ShortOptionTickData;

        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesOptionTickDataAsync(contract, optionTickData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionTickDataInsertedEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedCompleteEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedFailEvent.Should().BeNull();
        futuresOptionTickDataUpdatedEvent.Should().NotBeNull();

        futuresOptionTickDataInsertedEvent.Contract.ContractId.Should().Be(contract.ContractId);
        futuresOptionTickDataInsertedEvent.TickData.ContractId.Should().Be(optionTickData.ContractId);
        futuresOptionTickDataInsertedEvent.TickData.ValueDate.Should().Be(optionTickData.ValueDate);

        // verify data was inserted into database...
        var lastTickDataId = await dbFixture.MarketDataDb.GetLastFuturesOptionTickDataIdAsync(optionTickData.ContractId, optionTickData.ValueDate);
        lastTickDataId.Should().NotBeNull();
        lastTickDataId!.ContractId.Should().Be(optionTickData.ContractId);
        lastTickDataId.ValueDate.Should().Be(optionTickData.ValueDate);

        var insertedTickData = await dbFixture.MarketDataDb.GetFuturesOptionTickDataAsync(lastTickDataId);
        insertedTickData.Should().NotBeNull();
        insertedTickData!.ContractId.Should().Be(optionTickData.ContractId);
        insertedTickData.ValueDate.Should().Be(optionTickData.ValueDate);
        insertedTickData.BidPrice.Should().Be(optionTickData.BidPrice);
        insertedTickData.AskPrice.Should().Be(optionTickData.AskPrice);
        insertedTickData.BidSize.Should().Be(optionTickData.BidSize);
        insertedTickData.AskSize.Should().Be(optionTickData.AskSize);
        insertedTickData.ImpliedVolatility.Should().Be(optionTickData.ImpliedVolatility);
        insertedTickData.Delta.Should().Be(optionTickData.Delta);
        insertedTickData.Gamma.Should().Be(optionTickData.Gamma);
        insertedTickData.Vega.Should().Be(optionTickData.Vega);
        insertedTickData.Theta.Should().Be(optionTickData.Theta);
        insertedTickData.Rho.Should().Be(optionTickData.Rho);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionTickDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedFailEvent>()!),
                _ when eventVerb == OptionTradeTickPriceDataUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeTickPriceDataUpdatedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionTickDataInsertedEvent inserted)
                    futuresOptionTickDataInsertedEvent = inserted;
                if (@event is FuturesOptionTickDataInsertedCompleteEvent insertedComplete)
                    futuresOptionTickDataInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesOptionTickDataInsertedFailEvent insertedFail)
                    futuresOptionTickDataInsertedFailEvent = insertedFail;
                if (@event is OptionTradeTickPriceDataUpdatedEvent updated)
                    futuresOptionTickDataUpdatedEvent = updated;
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertFuturesOptionTickData_WithDifferentOptionContracts_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionTickDataInsertedEvent futuresOptionTickDataInsertedEvent = default!;
        FuturesOptionTickDataInsertedCompleteEvent futuresOptionTickDataInsertedCompleteEvent = default!;
        FuturesOptionTickDataInsertedFailEvent futuresOptionTickDataInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataInsertedEvent.Actor)] = [FuturesOptionTickDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var optionTickData = SampleData.LongOptionTickData;

        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesOptionTickDataAsync(contract, optionTickData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionTickDataInsertedEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedCompleteEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedFailEvent.Should().BeNull();

        futuresOptionTickDataInsertedEvent.TickData.ContractId.Should().Be(optionTickData.ContractId);
        futuresOptionTickDataInsertedEvent.TickData.OptionPrice.Should().Be(optionTickData.OptionPrice);

        // verify data was inserted into database...
        var lastTickDataId = await dbFixture.MarketDataDb.GetLastFuturesOptionTickDataIdAsync(optionTickData.ContractId, optionTickData.ValueDate);
        lastTickDataId.Should().NotBeNull();
        lastTickDataId!.ContractId.Should().Be(optionTickData.ContractId);

        var insertedTickData = await dbFixture.MarketDataDb.GetFuturesOptionTickDataAsync(lastTickDataId);
        insertedTickData.Should().NotBeNull();
        insertedTickData!.ContractId.Should().Be(optionTickData.ContractId);
        insertedTickData.OptionPrice.Should().Be(optionTickData.OptionPrice);
        insertedTickData.UnderlyingPrice.Should().Be(optionTickData.UnderlyingPrice);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionTickDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionTickDataInsertedEvent inserted)
                    futuresOptionTickDataInsertedEvent = inserted;
                if (@event is FuturesOptionTickDataInsertedCompleteEvent insertedComplete)
                    futuresOptionTickDataInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesOptionTickDataInsertedFailEvent insertedFail)
                    futuresOptionTickDataInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertFuturesOptionTickData_WithCallOptionContracts_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionTickDataInsertedEvent futuresOptionTickDataInsertedEvent = default!;
        FuturesOptionTickDataInsertedCompleteEvent futuresOptionTickDataInsertedCompleteEvent = default!;
        FuturesOptionTickDataInsertedFailEvent futuresOptionTickDataInsertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataInsertedEvent.Actor)] = [FuturesOptionTickDataInsertedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var optionTickData = SampleData.ShortCallOptionTickData;

        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesOptionTickDataAsync(contract, optionTickData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionTickDataInsertedEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedCompleteEvent.Should().NotBeNull();
        futuresOptionTickDataInsertedFailEvent.Should().BeNull();

        futuresOptionTickDataInsertedEvent.Contract.ContractId.Should().Be(contract.ContractId);
        futuresOptionTickDataInsertedEvent.TickData.ContractId.Should().Be(optionTickData.ContractId);
        futuresOptionTickDataInsertedEvent.TickData.ValueDate.Should().Be(optionTickData.ValueDate);

        // verify data was inserted into database...
        var lastTickDataId = await dbFixture.MarketDataDb.GetLastFuturesOptionTickDataIdAsync(optionTickData.ContractId, optionTickData.ValueDate);
        lastTickDataId.Should().NotBeNull();
        lastTickDataId!.ContractId.Should().Be(optionTickData.ContractId);
        lastTickDataId.ValueDate.Should().Be(optionTickData.ValueDate);

        var insertedTickData = await dbFixture.MarketDataDb.GetFuturesOptionTickDataAsync(lastTickDataId);
        insertedTickData.Should().NotBeNull();
        insertedTickData!.ContractId.Should().Be(optionTickData.ContractId);
        insertedTickData.ValueDate.Should().Be(optionTickData.ValueDate);
        insertedTickData.OptionPrice.Should().Be(optionTickData.OptionPrice);
        insertedTickData.BidPrice.Should().Be(optionTickData.BidPrice);
        insertedTickData.AskPrice.Should().Be(optionTickData.AskPrice);
        insertedTickData.BidSize.Should().Be(optionTickData.BidSize);
        insertedTickData.AskSize.Should().Be(optionTickData.AskSize);
        insertedTickData.ImpliedVolatility.Should().Be(optionTickData.ImpliedVolatility);
        insertedTickData.UnderlyingPrice.Should().Be(optionTickData.UnderlyingPrice);
        insertedTickData.Delta.Should().Be(optionTickData.Delta);
        insertedTickData.Gamma.Should().Be(optionTickData.Gamma);
        insertedTickData.Vega.Should().Be(optionTickData.Vega);
        insertedTickData.Theta.Should().Be(optionTickData.Theta);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionTickDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionTickDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionTickDataInsertedEvent inserted)
                    futuresOptionTickDataInsertedEvent = inserted;
                if (@event is FuturesOptionTickDataInsertedCompleteEvent insertedComplete)
                    futuresOptionTickDataInsertedCompleteEvent = insertedComplete;
                if (@event is FuturesOptionTickDataInsertedFailEvent insertedFail)
                    futuresOptionTickDataInsertedFailEvent = insertedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StartFuturesOptionTickDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionTickDataStreamingStartedEvent futuresOptionTickDataStreamingStartedEvent = default!;
        FuturesOptionTickDataStreamingStartedCompleteEvent futuresOptionTickDataStreamingStartedCompleteEvent = default!;
        FuturesOptionTickDataStreamingStartedFailEvent futuresOptionTickDataStreamingStartedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataStreamingStartedEvent.Actor)] = [FuturesOptionTickDataStreamingStartedEvent.Verb]
            },
            EventHandlerAsync
        );

        var entityId = new FuturesOptionTickEntityId(SampleData.ShortOptionContract.ContractId, SampleData.ValueDate);
        var optionContract = SampleData.ShortOptionContract;
        var baseContract = SampleData.FuturesContract;
        var valueDate = SampleData.ValueDate;
        var maturityDate = new DateOnly(2025, 12, 19);
        var riskFreeRate = 0.05;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StartFuturesOptionTickDataStreamingAsync(entityId, optionContract, baseContract, valueDate, maturityDate, riskFreeRate);

        await Task.Delay(7000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionTickDataStreamingStartedEvent.Should().NotBeNull();
        //futuresOptionTickDataStreamingStartedCompleteEvent.Should().NotBeNull();
        futuresOptionTickDataStreamingStartedFailEvent.Should().NotBeNull();

        futuresOptionTickDataStreamingStartedEvent.Contract.ContractId.Should().Be(optionContract.ContractId);
        futuresOptionTickDataStreamingStartedEvent.BaseContract.ContractId.Should().Be(baseContract.ContractId);
        futuresOptionTickDataStreamingStartedEvent.ValueDate.Should().Be(valueDate);
        futuresOptionTickDataStreamingStartedEvent.MaturityDate.Should().Be(maturityDate);
        futuresOptionTickDataStreamingStartedEvent.RiskFreeRate.Should().Be(riskFreeRate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionTickDataStreamingStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStartedEvent>()!),
                _ when eventVerb == FuturesOptionTickDataStreamingStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStartedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionTickDataStreamingStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStartedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionTickDataStreamingStartedEvent started)
                    futuresOptionTickDataStreamingStartedEvent = started;
                if (@event is FuturesOptionTickDataStreamingStartedCompleteEvent startedComplete)
                    futuresOptionTickDataStreamingStartedCompleteEvent = startedComplete;
                if (@event is FuturesOptionTickDataStreamingStartedFailEvent startedFail)
                    futuresOptionTickDataStreamingStartedFailEvent = startedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopFuturesOptionTickDataStreaming_Ok()
    {
        // arrange... start streaming first
        var entityId = new FuturesOptionTickEntityId(SampleData.ShortOptionContract.ContractId, SampleData.ValueDate);
        var optionContract = SampleData.ShortOptionContract;
        var baseContract = SampleData.FuturesContract;
        var valueDate = SampleData.ValueDate;
        var maturityDate = new DateOnly(2025, 12, 19);
        var riskFreeRate = 0.05;

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var startResponse = await marketDataFeedApi.StartFuturesOptionTickDataStreamingAsync(entityId, optionContract, baseContract, valueDate, maturityDate, riskFreeRate);

        await Task.Delay(7000);

        // assert start succeeded...
        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue();
        startResponse.Value.Should().NotBe(Guid.Empty);

        // arrange... listen for stop events
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionTickDataStreamingStoppedEvent futuresOptionTickDataStreamingStoppedEvent = default!;
        FuturesOptionTickDataStreamingStoppedCompleteEvent futuresOptionTickDataStreamingStoppedCompleteEvent = default!;
        FuturesOptionTickDataStreamingStoppedFailEvent futuresOptionTickDataStreamingStoppedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionTickDataStreamingStoppedEvent.Actor)] = [FuturesOptionTickDataStreamingStoppedEvent.Verb]
            },
            EventHandlerAsync
        );

        // act... stop streaming
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StopFuturesOptionTickDataStreamingAsync(entityId, optionContract.ContractId);

        await Task.Delay(7000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionTickDataStreamingStoppedEvent.Should().NotBeNull();
        futuresOptionTickDataStreamingStoppedCompleteEvent.Should().BeNull();
        futuresOptionTickDataStreamingStoppedFailEvent.Should().NotBeNull();

        futuresOptionTickDataStreamingStoppedEvent.EntityId.Should().Be(entityId);
        futuresOptionTickDataStreamingStoppedEvent.ContractId.Should().Be(optionContract.ContractId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionTickDataStreamingStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStoppedEvent>()!),
                _ when eventVerb == FuturesOptionTickDataStreamingStoppedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStoppedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionTickDataStreamingStoppedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionTickDataStreamingStoppedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionTickDataStreamingStoppedEvent stopped)
                    futuresOptionTickDataStreamingStoppedEvent = stopped;
                if (@event is FuturesOptionTickDataStreamingStoppedCompleteEvent stoppedComplete)
                    futuresOptionTickDataStreamingStoppedCompleteEvent = stoppedComplete;
                if (@event is FuturesOptionTickDataStreamingStoppedFailEvent stoppedFail)
                    futuresOptionTickDataStreamingStoppedFailEvent = stoppedFail;
                return @event;
            }
        }
    }
}
