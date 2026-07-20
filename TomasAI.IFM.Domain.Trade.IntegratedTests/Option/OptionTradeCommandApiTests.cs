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
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.IntegratedTests.Option;

public class OptionTradeCommandApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task OpenOptionTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OptionTradeToOpenEvent optionTradeToOpenEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OptionTradeToOpenEvent.Actor)] = [OptionTradeToOpenEvent.Verb]
            },
            EventHandlerAsync
        );

        var tradeOrder = SampleData.CreateTradeOrder();
        var subject = new ActorSubject(ActorType.Command, OpenOptionTradeCommand.Actor, OpenOptionTradeCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.OpenOptionTradeAsync(tradeOrder);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        optionTradeToOpenEvent.Should().NotBeNull();

        var newTrade = await dbFixture.TradeDb.GetOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        newTrade.Should().NotBeNull();
        newTrade!.OrderId.Should().Be(tradeOrder.OrderId);
        newTrade.TradeId.Should().Be(tradeOrder.TradeId);
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OptionTradeToOpenEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeToOpenEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OptionTradeToOpenEvent e)
                    optionTradeToOpenEvent = e;
                return @event;
            }
        }
    }

    [Fact]
    public async Task CloseOptionTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OptionTradeToCloseEvent optionTradeToCloseEvent = default!;

        var tradeOrder = SampleData.CreateTradeOrder(orderId: 101, tradeId: 2, orderActionType: OrderActionType.Close);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 101, tradeId: 2, tradeState: TradeState.TradeToClose);

        // ensure trade exists before closing
        var subject = new ActorSubject(ActorType.Command, CloseOptionTradeCommand.Actor, CloseOptionTradeCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OptionTradeToCloseEvent.Actor)] = [OptionTradeToCloseEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.CloseOptionTradeAsync(tradeOrder);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        optionTradeToCloseEvent.Should().NotBeNull();
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OptionTradeToCloseEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeToCloseEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OptionTradeToCloseEvent e)
                    optionTradeToCloseEvent = e;
                return @event;
            }
        }
    }

    [Fact]
    public async Task DeleteOptionTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OptionTradeDeletedEvent optionTradeDeletedEvent = default!;

        var tradeOrder = SampleData.CreateTradeOrder(orderId: 102, tradeId: 3);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 102, tradeId: 3);

        // insert trade first so delete can find it
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var subject = new ActorSubject(ActorType.Command, DeleteOptionTradeCommand.Actor, DeleteOptionTradeCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OptionTradeDeletedEvent.Actor)] = [OptionTradeDeletedEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.DeleteAsync(tradeOrder.OrderId, tradeOrder.TradeId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        optionTradeDeletedEvent.Should().NotBeNull();
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OptionTradeDeletedEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeDeletedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OptionTradeDeletedEvent e)
                    optionTradeDeletedEvent = e;
                return @event;
            }
        }
    }

    [Fact]
    public async Task PlaceOptionTradeOrder_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OptionTradeOrderPlacedEvent optionTradeOrderPlacedEvent = default!;

        var tradeOrder = SampleData.CreateTradeOrder(orderId: 103, tradeId: 4);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 103, tradeId: 4);

        var subject = new ActorSubject(ActorType.Command, PlaceOptionTradeOrderCommand.Actor, PlaceOptionTradeOrderCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OptionTradeOrderPlacedEvent.Actor)] = [OptionTradeOrderPlacedEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.PlaceOrderAsync(tradeOrder, optionTrade);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        optionTradeOrderPlacedEvent.Should().NotBeNull();
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OptionTradeOrderPlacedEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeOrderPlacedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OptionTradeOrderPlacedEvent e)
                    optionTradeOrderPlacedEvent = e;
                return @event;
            }
        }
    }

    [Fact]
    public async Task SnapshotOptionTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OptionTradeSnapshotEvent optionTradeSnapshotEvent = default!;

        var tradeOrder = SampleData.CreateTradeOrder(orderId: 104, tradeId: 5);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 104, tradeId: 5);

        // insert trade first so snapshot can find it
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var subject = new ActorSubject(ActorType.Command, SnapshotOptionTradeCommand.Actor, SnapshotOptionTradeCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OptionTradeSnapshotEvent.Actor)] = [OptionTradeSnapshotEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.SnapshotAsync(tradeOrder.OrderId, tradeOrder.TradeId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        optionTradeSnapshotEvent.Should().NotBeNull();
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OptionTradeSnapshotEvent.Verb => SetEvent(eventMsg.AsEvent<OptionTradeSnapshotEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OptionTradeSnapshotEvent e)
                    optionTradeSnapshotEvent = e;
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertOptionTradeSpreadData_Ok()
    {
        // arrange...
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 105, tradeId: 6);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 105, tradeId: 6);
        var valueDate = new DateOnly(2025, 1, 15);

        // ensure trade exists
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);
        await dbFixture.TradeDb.DeleteOptionTradeSpreadDataAsync(tradeOrder.OrderId, tradeOrder.TradeId, valueDate, TradeType.PutCreditSpread);

        var spreadData = new OptionTradeSpreadsDataModel
        {
            OrderId = tradeOrder.OrderId,
            TradeId = tradeOrder.TradeId,
            ValueDate = valueDate,
            TradeType = TradeType.PutCreditSpread
        };

        var subject = new ActorSubject(ActorType.Command, InsertOptionTradeSpreadDataCommand.Actor, InsertOptionTradeSpreadDataCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.InsertOptionTradeSpreadDataAsync(spreadData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertOptionTradeSpreadBarData_Ok()
    {
        // arrange...
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 106, tradeId: 7);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 106, tradeId: 7);
        var valueDate = new DateOnly(2025, 1, 15);

        // ensure trade exists
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);
        await dbFixture.TradeDb.DeleteOptionTradeSpreadBarDataAsync(tradeOrder.OrderId, tradeOrder.TradeId, valueDate, TradeType.PutCreditSpread);

        var spreadBarData = new OptionTradeSpreadBarsDataModel
        {
            OrderId = tradeOrder.OrderId,
            TradeId = tradeOrder.TradeId,
            ValueDate = valueDate,
            TradeType = TradeType.PutCreditSpread
        };

        var subject = new ActorSubject(ActorType.Command, InsertOptionTradeSpreadBarDataCommand.Actor, InsertOptionTradeSpreadBarDataCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.InsertOptionTradeSpreadBarDataAsync(spreadBarData);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteOptionTradeSpreadBarData_Ok()
    {
        // arrange...
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 107, tradeId: 8);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 107, tradeId: 8);
        var valueDate = new DateOnly(2025, 1, 15);
        var entityId = new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId);

        // ensure trade exists with spread bar data
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var subject = new ActorSubject(ActorType.Command, DeleteOptionTradeSpreadBarDataCommand.Actor, DeleteOptionTradeSpreadBarDataCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.DeleteOptionTradeSpreadBarDataAsync(entityId, TradeType.PutCreditSpread, valueDate);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UpdateDailyProfitTarget_Ok()
    {
        // arrange...
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 108, tradeId: 9);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 108, tradeId: 9);

        // ensure trade exists
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var subject = new ActorSubject(ActorType.Command, UpdateOptionTradeDailyProfitTargetCommand.Actor, UpdateOptionTradeDailyProfitTargetCommand.Verb, $"{tradeOrder.OrderId}:{tradeOrder.TradeId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.UpdateTradeLimitDailyProfitTargetAsync(tradeOrder.OrderId, tradeOrder.TradeId, 5, 30);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteOptionTrades_Ok()
    {
        // arrange...
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 109, tradeId: 10);
        var optionTrade = SampleData.CreateOptionTrade(orderId: 109, tradeId: 10);

        // ensure trade exists
        await dbFixture.TradeDb.DeleteOptionTradeAsync(tradeOrder.OrderId, tradeOrder.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var subject = new ActorSubject(ActorType.Command, DeleteOptionTradesCommand.Actor, DeleteOptionTradesCommand.Verb, $"{tradeOrder.OrderId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeCommandApi(commandServiceApi);
        var response = await tradeApi.DeleteAsync(tradeOrder.OrderId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
