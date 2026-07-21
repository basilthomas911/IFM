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
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Plan;

public class TradePlanCommandApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task UpdateTradePlan_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanUpdatedEvent tradePlanUpdatedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradePlanUpdatedEvent.Actor)] = [TradePlanUpdatedEvent.Verb]
            },
            EventHandlerAsync
        );

        var tradePlan = SampleData.CreateTradePlan(orderId: 300, tradeId: 1);
        var entityId = new TradePlanEntityId(tradePlan.OrderId, tradePlan.TradeId, tradePlan.ValueDate);
        var subject = new ActorSubject(ActorType.Command, UpdateTradePlanCommand.Actor, UpdateTradePlanCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        await dbFixture.TradeDb.InsertTradePlanAsync(tradePlan);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new TradePlanCommandApi(commandServiceApi);
        var response = await tradeApi.UpdateTradePlanAsync(tradePlan);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradePlanUpdatedEvent.Should().NotBeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradePlanUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<TradePlanUpdatedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                tradePlanUpdatedEvent = (TradePlanUpdatedEvent)@event;
                return @event;
            }
        }
    }

    [Fact]
    public async Task UpdateTradePlanForwardLossLimit_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanForwardLossLimitUpdatedEvent forwardLossLimitUpdatedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradePlanForwardLossLimitUpdatedEvent.Actor)] = [TradePlanForwardLossLimitUpdatedEvent.Verb]
            },
            EventHandlerAsync
        );

        var forwardLossLimit = SampleData.CreateTradePlanForwardLossLimit(orderId: 301, tradeId: 1);
        var entityId = forwardLossLimit.EntityId;
        await dbFixture.TradeDb.DeleteTradePlanForwardLossLimitAsync(entityId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new TradePlanCommandApi(commandServiceApi);
        var response = await tradeApi.UpdateTradePlanForwardLossLimitAsync(forwardLossLimit);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        forwardLossLimitUpdatedEvent.Should().NotBeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradePlanForwardLossLimitUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<TradePlanForwardLossLimitUpdatedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                forwardLossLimitUpdatedEvent = (TradePlanForwardLossLimitUpdatedEvent)@event;
                return @event;
            }
        }
    }

    [Fact]
    public async Task ClearTradePlanForwardLossLimit_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanForwardLossLimitClearedEvent forwardLossLimitClearedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradePlanForwardLossLimitClearedEvent.Actor)] = [TradePlanForwardLossLimitClearedEvent.Verb]
            },
            EventHandlerAsync
        );

        var forwardLossLimit = SampleData.CreateTradePlanForwardLossLimit(orderId: 302, tradeId: 1);
        var entityId = forwardLossLimit.EntityId;
        await dbFixture.TradeDb.DeleteTradePlanForwardLossLimitAsync(entityId);
        await dbFixture.TradeDb.InsertTradePlanForwardLossLimitAsync(forwardLossLimit);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new TradePlanCommandApi(commandServiceApi);
        var response = await tradeApi.ClearTradePlanForwardLossLimitAsync(entityId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        forwardLossLimitClearedEvent.Should().NotBeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradePlanForwardLossLimitClearedEvent.Verb => SetEvent(eventMsg.AsEvent<TradePlanForwardLossLimitClearedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                forwardLossLimitClearedEvent = (TradePlanForwardLossLimitClearedEvent)@event;
                return @event;
            }
        }
    }
}
