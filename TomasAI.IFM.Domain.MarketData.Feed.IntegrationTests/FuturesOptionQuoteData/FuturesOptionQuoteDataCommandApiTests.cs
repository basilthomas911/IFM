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
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesOptionQuoteData;

public class FuturesOptionQuoteDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task StartFuturesOptionQuoteDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionQuoteDataStreamingStartedEvent futuresOptionQuoteDataStreamingStartedEvent = default!;
        FuturesOptionQuoteDataStreamingStartedCompleteEvent futuresOptionQuoteDataStreamingStartedCompleteEvent = default!;
        FuturesOptionQuoteDataStreamingStartedFailEvent futuresOptionQuoteDataStreamingStartedFailEvent = default!;
        var domainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionQuoteDataStreamingStartedEvent.Actor)] =
                [
                    FuturesOptionQuoteDataStreamingStartedEvent.Verb,
                    FuturesOptionQuoteDataStreamingStartedCompleteEvent.Verb,
                    FuturesOptionQuoteDataStreamingStartedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var quoteId = Random.Shared.Next(1, int.MaxValue);
        var futuresOptionQuotes = new[]
        {
            new FuturesOptionQuoteReadModel(quoteId, SampleData.ShortOptionContract.ContractId, 100, "IntegrationTest", DateTime.UtcNow),
            new FuturesOptionQuoteReadModel(quoteId, SampleData.LongOptionContract.ContractId, 101, "IntegrationTest", DateTime.UtcNow)
        };
        var futuresOptionContracts = new[] { SampleData.ShortOptionContract, SampleData.LongOptionContract };

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StartFuturesOptionQuoteDataStreamingAsync(quoteId, futuresOptionQuotes, futuresOptionContracts);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await Task.WhenAll(domainEventReceived.Task, terminalEventReceived.Task).WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresOptionQuoteDataStreamingStartedEvent.Should().NotBeNull();
        futuresOptionQuoteDataStreamingStartedFailEvent.Should().BeNull(futuresOptionQuoteDataStreamingStartedFailEvent?.ErrorMessage);
        futuresOptionQuoteDataStreamingStartedCompleteEvent.Should().NotBeNull();

        futuresOptionQuoteDataStreamingStartedEvent.QuoteId.Should().Be(quoteId);
        futuresOptionQuoteDataStreamingStartedEvent.FuturesOptionQuotes.Should().NotBeNullOrEmpty();
        futuresOptionQuoteDataStreamingStartedEvent.FuturesOptionQuotes!.Length.Should().Be(futuresOptionQuotes.Length);
        futuresOptionQuoteDataStreamingStartedEvent.FuturesOptionContracts.Should().NotBeNullOrEmpty();
        futuresOptionQuoteDataStreamingStartedEvent.FuturesOptionContracts!.Length.Should().Be(futuresOptionContracts.Length);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionQuoteDataStreamingStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStartedEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataStreamingStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStartedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataStreamingStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStartedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionQuoteDataStreamingStartedEvent started)
                {
                    futuresOptionQuoteDataStreamingStartedEvent = started;
                    domainEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataStreamingStartedCompleteEvent startedComplete)
                {
                    futuresOptionQuoteDataStreamingStartedCompleteEvent = startedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataStreamingStartedFailEvent startedFail)
                {
                    futuresOptionQuoteDataStreamingStartedFailEvent = startedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopFuturesOptionQuoteDataStreaming_Ok()
    {
        // arrange... start streaming first
        var quoteId = Random.Shared.Next(1, int.MaxValue);
        var futuresOptionQuotes = new[]
        {
            new FuturesOptionQuoteReadModel(quoteId, SampleData.ShortOptionContract.ContractId, 100, "IntegrationTest", DateTime.UtcNow),
            new FuturesOptionQuoteReadModel(quoteId, SampleData.LongOptionContract.ContractId, 101, "IntegrationTest", DateTime.UtcNow)
        };
        var futuresOptionContracts = new[] { SampleData.ShortOptionContract, SampleData.LongOptionContract };

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var startResponse = await marketDataFeedApi.StartFuturesOptionQuoteDataStreamingAsync(quoteId, futuresOptionQuotes, futuresOptionContracts);

        // assert start succeeded...
        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue(startResponse.ErrorMessage);
        startResponse.Value.Should().NotBe(Guid.Empty);

        // arrange... listen for stop events
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionQuoteDataStreamingStoppedEvent futuresOptionQuoteDataStreamingStoppedEvent = default!;
        FuturesOptionQuoteDataStreamingStoppedCompleteEvent futuresOptionQuoteDataStreamingStoppedCompleteEvent = default!;
        FuturesOptionQuoteDataStreamingStoppedFailEvent futuresOptionQuoteDataStreamingStoppedFailEvent = default!;
        var domainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionQuoteDataStreamingStoppedEvent.Actor)] =
                [
                    FuturesOptionQuoteDataStreamingStoppedEvent.Verb,
                    FuturesOptionQuoteDataStreamingStoppedCompleteEvent.Verb,
                    FuturesOptionQuoteDataStreamingStoppedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        // act... stop streaming
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StopFuturesOptionQuoteDataStreamingAsync(quoteId);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await Task.WhenAll(domainEventReceived.Task, terminalEventReceived.Task).WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresOptionQuoteDataStreamingStoppedEvent.Should().NotBeNull();
        futuresOptionQuoteDataStreamingStoppedFailEvent.Should().BeNull(futuresOptionQuoteDataStreamingStoppedFailEvent?.ErrorMessage);
        futuresOptionQuoteDataStreamingStoppedCompleteEvent.Should().NotBeNull();

        futuresOptionQuoteDataStreamingStoppedEvent.QuoteId.Should().Be(quoteId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionQuoteDataStreamingStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStoppedEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataStreamingStoppedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStoppedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataStreamingStoppedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataStreamingStoppedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionQuoteDataStreamingStoppedEvent stopped)
                {
                    futuresOptionQuoteDataStreamingStoppedEvent = stopped;
                    domainEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataStreamingStoppedCompleteEvent stoppedComplete)
                {
                    futuresOptionQuoteDataStreamingStoppedCompleteEvent = stoppedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataStreamingStoppedFailEvent stoppedFail)
                {
                    futuresOptionQuoteDataStreamingStoppedFailEvent = stoppedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertFuturesOptionQuoteData_Ok()
    {
        // arrange... start streaming first
        var quoteId = Random.Shared.Next(1, int.MaxValue);
        var futuresOptionQuotes = new[]
        {
            new FuturesOptionQuoteReadModel(quoteId, SampleData.ShortOptionContract.ContractId, 100, "IntegrationTest", DateTime.UtcNow),
            new FuturesOptionQuoteReadModel(quoteId, SampleData.LongOptionContract.ContractId, 101, "IntegrationTest", DateTime.UtcNow)
        };
        var futuresOptionContracts = new[] { SampleData.ShortOptionContract, SampleData.LongOptionContract };

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var startResponse = await marketDataFeedApi.StartFuturesOptionQuoteDataStreamingAsync(quoteId, futuresOptionQuotes, futuresOptionContracts);

        // assert start succeeded...
        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue(startResponse.ErrorMessage);
        startResponse.Value.Should().NotBe(Guid.Empty);

        // arrange... listen for insert events
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionQuoteDataInsertedEvent futuresOptionQuoteDataInsertedEvent = default!;
        FuturesOptionQuoteDataInsertedCompleteEvent futuresOptionQuoteDataInsertedCompleteEvent = default!;
        FuturesOptionQuoteDataInsertedFailEvent futuresOptionQuoteDataInsertedFailEvent = default!;
        var domainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionQuoteDataInsertedEvent.Actor)] =
                [
                    FuturesOptionQuoteDataInsertedEvent.Verb,
                    FuturesOptionQuoteDataInsertedCompleteEvent.Verb,
                    FuturesOptionQuoteDataInsertedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ShortOptionContract.ContractId;
        var quoteTime = DateTime.UtcNow;
        QuoteData[] quoteDataUpdates =
        [
            new(quoteTime, QuoteLevelType.LevelOne, 0, 1, QuoteSide.Ask, QuoteType.Price, 12.50, 0),
            new(quoteTime, QuoteLevelType.LevelOne, 0, 1, QuoteSide.Ask, QuoteType.Size, 0, 10),
            new(quoteTime, QuoteLevelType.LevelOne, 0, 1, QuoteSide.Bid, QuoteType.Price, 12.25, 0),
            new(quoteTime, QuoteLevelType.LevelOne, 0, 1, QuoteSide.Bid, QuoteType.Size, 0, 8)
        ];

        // act... insert quote data
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        foreach (var quoteData in quoteDataUpdates)
        {
            var updateResponse = await marketDataFeedApi.InsertFuturesOptionQuoteDataAsync(quoteId, contractId, quoteData);
            updateResponse.Should().NotBeNull();
            updateResponse.Success.Should().BeTrue(updateResponse.ErrorMessage);
            updateResponse.Value.Should().NotBe(Guid.Empty);
        }

        await Task.WhenAll(domainEventReceived.Task, terminalEventReceived.Task).WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresOptionQuoteDataInsertedEvent.Should().NotBeNull();
        futuresOptionQuoteDataInsertedFailEvent.Should().BeNull(futuresOptionQuoteDataInsertedFailEvent?.ErrorMessage);
        futuresOptionQuoteDataInsertedCompleteEvent.Should().NotBeNull();

        futuresOptionQuoteDataInsertedEvent.QuoteId.Should().Be(quoteId);
        futuresOptionQuoteDataInsertedEvent.ContractId.Should().Be(contractId);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.Should().NotBeNull();
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.QuoteId.Should().Be(quoteId);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.ContractId.Should().Be(contractId);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.AskPrice.Should().Be(12.50m);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.AskSize.Should().Be(10);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.BidPrice.Should().Be(12.25m);
        futuresOptionQuoteDataInsertedEvent.OptionQuoteData.BidSize.Should().Be(8);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionQuoteDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataInsertedEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionQuoteDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionQuoteDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesOptionQuoteDataInsertedEvent inserted)
                {
                    futuresOptionQuoteDataInsertedEvent = inserted;
                    domainEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataInsertedCompleteEvent insertedComplete)
                {
                    futuresOptionQuoteDataInsertedCompleteEvent = insertedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesOptionQuoteDataInsertedFailEvent insertedFail)
                {
                    futuresOptionQuoteDataInsertedFailEvent = insertedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
