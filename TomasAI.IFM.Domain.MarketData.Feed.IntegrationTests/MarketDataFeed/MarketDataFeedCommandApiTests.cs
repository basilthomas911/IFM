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
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.MarketDataFeed;

public class MarketDataFeedCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task StartMarketDataFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        MarketDataFeedStartedEvent marketDataFeedStartedEvent = default!;
        MarketDataFeedStartedCompleteEvent marketDataFeedStartedCompleteEvent = default!;
        MarketDataFeedStartedFailEvent marketDataFeedStartedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, MarketDataFeedStartedEvent.Actor)] = [MarketDataFeedStartedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresContracts = new[] { SampleData.FuturesContract };
        var valueDate = SampleData.ValueDate;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StartMarketDataFeedAsync(futuresContracts, valueDate);

        await Task.Delay(10000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        marketDataFeedStartedEvent.Should().NotBeNull();
        marketDataFeedStartedCompleteEvent.Should().BeNull();
        marketDataFeedStartedFailEvent.Should().NotBeNull();

        marketDataFeedStartedEvent.ValueDate.Should().Be(valueDate);
        marketDataFeedStartedEvent.FuturesContracts.Should().NotBeNullOrEmpty();
        marketDataFeedStartedEvent.FuturesContracts!.Length.Should().Be(futuresContracts.Length);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == MarketDataFeedStartedEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStartedEvent>()!),
                _ when eventVerb == MarketDataFeedStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStartedCompleteEvent>()!),
                _ when eventVerb == MarketDataFeedStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStartedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is MarketDataFeedStartedEvent started)
                    marketDataFeedStartedEvent = started;
                if (@event is MarketDataFeedStartedCompleteEvent startedComplete)
                    marketDataFeedStartedCompleteEvent = startedComplete;
                if (@event is MarketDataFeedStartedFailEvent startedFail)
                    marketDataFeedStartedFailEvent = startedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopMarketDataFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        MarketDataFeedStoppedEvent marketDataFeedStoppedEvent = default!;
        MarketDataFeedStoppedCompleteEvent marketDataFeedStoppedCompleteEvent = default!;
        MarketDataFeedStoppedFailEvent marketDataFeedStoppedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, MarketDataFeedStoppedEvent.Actor)] = [MarketDataFeedStoppedEvent.Verb]
            },
            EventHandlerAsync
        );

        var valueDate = SampleData.ValueDate;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StopMarketDataFeedAsync(valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        marketDataFeedStoppedEvent.Should().NotBeNull();
        marketDataFeedStoppedCompleteEvent.Should().NotBeNull();
        marketDataFeedStoppedFailEvent.Should().BeNull();

        marketDataFeedStoppedEvent.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == MarketDataFeedStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStoppedEvent>()!),
                _ when eventVerb == MarketDataFeedStoppedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStoppedCompleteEvent>()!),
                _ when eventVerb == MarketDataFeedStoppedFailEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedStoppedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is MarketDataFeedStoppedEvent stopped)
                    marketDataFeedStoppedEvent = stopped;
                if (@event is MarketDataFeedStoppedCompleteEvent stoppedComplete)
                    marketDataFeedStoppedCompleteEvent = stoppedComplete;
                if (@event is MarketDataFeedStoppedFailEvent stoppedFail)
                    marketDataFeedStoppedFailEvent = stoppedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task ResetMarketDataFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        MarketDataFeedResetEvent marketDataFeedResetEvent = default!;
        MarketDataFeedResetCompleteEvent marketDataFeedResetCompleteEvent = default!;
        MarketDataFeedResetFailEvent marketDataFeedResetFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, MarketDataFeedResetEvent.Actor)] = [MarketDataFeedResetEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresContracts = new[] { SampleData.FuturesContract };
        var valueDate = SampleData.ValueDate;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.ResetMarketDataFeedAsync(futuresContracts, valueDate);

        await Task.Delay(10000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        marketDataFeedResetEvent.Should().NotBeNull();
        marketDataFeedResetCompleteEvent.Should().BeNull();
        marketDataFeedResetFailEvent.Should().NotBeNull();

        //marketDataFeedResetEvent.ValueDate.Should().Be(valueDate);
       //marketDataFeedResetEvent.FuturesContracts.Should().NotBeNullOrEmpty();
        //marketDataFeedResetEvent.FuturesContracts!.Length.Should().Be(futuresContracts.Length);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == MarketDataFeedResetEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedResetEvent>()!),
                _ when eventVerb == MarketDataFeedResetCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedResetCompleteEvent>()!),
                _ when eventVerb == MarketDataFeedResetFailEvent.Verb => SetEvent(eventMsg.AsEvent<MarketDataFeedResetFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is MarketDataFeedResetEvent reset)
                    marketDataFeedResetEvent = reset;
                if (@event is MarketDataFeedResetCompleteEvent resetComplete)
                    marketDataFeedResetCompleteEvent = resetComplete;
                if (@event is MarketDataFeedResetFailEvent resetFail)
                    marketDataFeedResetFailEvent = resetFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task AddTradeLiveFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradeLiveFeedAddedEvent tradeLiveFeedAddedEvent = default!;
        TradeLiveFeedAddedCompleteEvent tradeLiveFeedAddedCompleteEvent = default!;
        TradeLiveFeedAddedFailEvent tradeLiveFeedAddedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradeLiveFeedAddedEvent.Actor)] = [TradeLiveFeedAddedEvent.Verb]
            },
            EventHandlerAsync
        );

        var orderId = 1;
        var tradeId = 1;
        var valueDate = DateOnly.FromDateTime(DateTime.Today);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.AddTradeLiveFeedAsync(orderId, tradeId, valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradeLiveFeedAddedEvent.Should().NotBeNull();
        tradeLiveFeedAddedCompleteEvent.Should().BeNull();
        tradeLiveFeedAddedFailEvent.Should().BeNull();

        tradeLiveFeedAddedEvent.OrderId.Should().Be(orderId);
        tradeLiveFeedAddedEvent.TradeId.Should().Be(tradeId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradeLiveFeedAddedEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedAddedEvent>()!),
                _ when eventVerb == TradeLiveFeedAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedAddedCompleteEvent>()!),
                _ when eventVerb == TradeLiveFeedAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedAddedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is TradeLiveFeedAddedEvent added)
                    tradeLiveFeedAddedEvent = added;
                if (@event is TradeLiveFeedAddedCompleteEvent addedComplete)
                    tradeLiveFeedAddedCompleteEvent = addedComplete;
                if (@event is TradeLiveFeedAddedFailEvent addedFail)
                    tradeLiveFeedAddedFailEvent = addedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task RemoveTradeLiveFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradeLiveFeedRemovedEvent tradeLiveFeedRemovedEvent = default!;
        TradeLiveFeedRemovedCompleteEvent tradeLiveFeedRemovedCompleteEvent = default!;
        TradeLiveFeedRemovedFailEvent tradeLiveFeedRemovedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradeLiveFeedRemovedEvent.Actor)] = [TradeLiveFeedRemovedEvent.Verb]
            },
            EventHandlerAsync
        );

        var orderId = 1;
        var tradeId = 1;
        var valueDate = DateOnly.FromDateTime(DateTime.Today);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.RemoveTradeLiveFeedAsync(orderId, tradeId, valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradeLiveFeedRemovedEvent.Should().NotBeNull();
        tradeLiveFeedRemovedCompleteEvent.Should().BeNull();
        tradeLiveFeedRemovedFailEvent.Should().BeNull();

        tradeLiveFeedRemovedEvent.OrderId.Should().Be(orderId);
        tradeLiveFeedRemovedEvent.TradeId.Should().Be(tradeId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradeLiveFeedRemovedEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedRemovedEvent>()!),
                _ when eventVerb == TradeLiveFeedRemovedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedRemovedCompleteEvent>()!),
                _ when eventVerb == TradeLiveFeedRemovedFailEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedRemovedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is TradeLiveFeedRemovedEvent removed)
                    tradeLiveFeedRemovedEvent = removed;
                if (@event is TradeLiveFeedRemovedCompleteEvent removedComplete)
                    tradeLiveFeedRemovedCompleteEvent = removedComplete;
                if (@event is TradeLiveFeedRemovedFailEvent removedFail)
                    tradeLiveFeedRemovedFailEvent = removedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task HaltTradeLiveFeed_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradeLiveFeedHaltedEvent tradeLiveFeedHaltedEvent = default!;
        TradeLiveFeedHaltedCompleteEvent tradeLiveFeedHaltedCompleteEvent = default!;
        TradeLiveFeedHaltedFailEvent tradeLiveFeedHaltedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradeLiveFeedHaltedEvent.Actor)] = [TradeLiveFeedHaltedEvent.Verb]
            },
            EventHandlerAsync
        );

        var orderId = 1;
        var tradeId = 1;

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.HaltTradeLiveFeedAsync(orderId, tradeId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradeLiveFeedHaltedEvent.Should().NotBeNull();
        tradeLiveFeedHaltedCompleteEvent.Should().BeNull();
        tradeLiveFeedHaltedFailEvent.Should().BeNull();

        tradeLiveFeedHaltedEvent.OrderId.Should().Be(orderId);
        tradeLiveFeedHaltedEvent.TradeId.Should().Be(tradeId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradeLiveFeedHaltedEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedHaltedEvent>()!),
                _ when eventVerb == TradeLiveFeedHaltedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedHaltedCompleteEvent>()!),
                _ when eventVerb == TradeLiveFeedHaltedFailEvent.Verb => SetEvent(eventMsg.AsEvent<TradeLiveFeedHaltedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is TradeLiveFeedHaltedEvent halted)
                    tradeLiveFeedHaltedEvent = halted;
                if (@event is TradeLiveFeedHaltedCompleteEvent haltedComplete)
                    tradeLiveFeedHaltedCompleteEvent = haltedComplete;
                if (@event is TradeLiveFeedHaltedFailEvent haltedFail)
                    tradeLiveFeedHaltedFailEvent = haltedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task DeleteStreamingRequestId_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        StreamingRequestIdDeletedEvent streamingRequestIdDeletedEvent = default!;
        StreamingRequestIdDeletedCompleteEvent streamingRequestIdDeletedCompleteEvent = default!;
        StreamingRequestIdDeletedFailEvent streamingRequestIdDeletedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, StreamingRequestIdDeletedEvent.Actor)] = [StreamingRequestIdDeletedEvent.Verb]
            },
            EventHandlerAsync
        );

        var feedId = new FeedId(1);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.DeleteStreamingRequestIdAsync(feedId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        streamingRequestIdDeletedEvent.Should().NotBeNull();
        streamingRequestIdDeletedCompleteEvent.Should().BeNull();
        streamingRequestIdDeletedFailEvent.Should().BeNull();

        streamingRequestIdDeletedEvent.FeedId.Should().Be(feedId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == StreamingRequestIdDeletedEvent.Verb => SetEvent(eventMsg.AsEvent<StreamingRequestIdDeletedEvent>()!),
                _ when eventVerb == StreamingRequestIdDeletedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<StreamingRequestIdDeletedCompleteEvent>()!),
                _ when eventVerb == StreamingRequestIdDeletedFailEvent.Verb => SetEvent(eventMsg.AsEvent<StreamingRequestIdDeletedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is StreamingRequestIdDeletedEvent deleted)
                    streamingRequestIdDeletedEvent = deleted;
                if (@event is StreamingRequestIdDeletedCompleteEvent deletedComplete)
                    streamingRequestIdDeletedCompleteEvent = deletedComplete;
                if (@event is StreamingRequestIdDeletedFailEvent deletedFail)
                    streamingRequestIdDeletedFailEvent = deletedFail;
                return @event;
            }
        }
    }
}
