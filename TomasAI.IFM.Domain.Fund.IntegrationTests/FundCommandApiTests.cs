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
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

/// <summary>
/// Provides integration tests for fund-related command operations, including creating funds, adding and removing orders
/// and trades, and verifying event-driven state changes using a test web application and database fixture.
/// </summary>
/// <remarks>These tests validate the behavior of fund command APIs and their event-driven workflows in a
/// controlled test environment. Each test ensures that commands produce the expected results and events, and that the
/// underlying data is updated accordingly. The class uses xUnit and is intended for end-to-end integration testing of
/// fund management scenarios.</remarks>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances and utilities for fund-related data setup and
/// cleanup.</param>
public class FundCommandApiTests(WebApplicationFactory<Program> factory, FundDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<FundDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task CreateFund_Ok()
    {

        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FundCreatedEvent fundCreatedEvent = default!;
        FundCreatedCompleteEvent fundCreatedCompleteEvent = default!;
        FundCreatedFailEvent fundCreatedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FundCreatedEvent.Actor)] 
                    = [FundCreatedEvent.Verb, FundCreatedCompleteEvent.Verb, FundCreatedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, $"{fund.FundId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        fundCreatedEvent.Should().NotBeNull();
        fundCreatedCompleteEvent.Should().NotBeNull();
        fundCreatedFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);
        newFund.Name.Should().Be(fund.Name);
        newFund.Description.Should().Be(fund.Description);
        newFund.Balance.Should().Be(fund.Balance);
        newFund.IsProduction.Should().Be(fund.IsProduction);
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FundCreatedEvent.Verb => SetEvent(eventMsg.AsEvent<FundCreatedEvent>()!),
                _ when eventVerb == FundCreatedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FundCreatedCompleteEvent>()!),
                _ when eventVerb == FundCreatedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FundCreatedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FundCreatedEvent fundCreated)
                    fundCreatedEvent = fundCreated;
                if (@event is FundCreatedCompleteEvent fundCreatedComplete)
                    fundCreatedCompleteEvent = fundCreatedComplete;
                if (@event is FundCreatedFailEvent fundCreatedFail)
                    fundCreatedFailEvent = fundCreatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task CreateFund_WithFundAlreadyCreated()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        // create fund first time...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert first creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // act... attempt to create the same fund again
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        fundApi = new FundCommandApi(commandServiceApi);
        response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert... second creation should fail
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.Value.Should().NotBe(Guid.Empty);
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain($"fundId {fund.FundId} already exists");
    }

    [Fact]
    public async Task AddOrderToFund_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OrderAddedToFundEvent orderAddedToFundEvent = default!;
        OrderAddedToFundCompleteEvent orderAddedToFundCompleteEvent = default!;
        OrderAddedToFundFailEvent orderAddedToFundFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        var deletedFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        deletedFund.Should().BeNull();

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
           "TestEventListener",
           new()
           {
               [new ActorMailboxId(ActorType.Event, OrderAddedToFundEvent.Actor)] 
                    = [
                        OrderAddedToFundEvent.Verb,
                        OrderAddedToFundCompleteEvent.Verb
                    ]
           },
           EventHandlerAsync
       );

        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);

        subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, fund.Id.Format());
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        orderAddedToFundEvent.Should().NotBeNull();
        orderAddedToFundCompleteEvent.Should().NotBeNull();
        orderAddedToFundFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var newFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        newFundOrder.Should().NotBeNull();
        newFundOrder!.FundId.Should().Be(fundOrder.FundId);
        newFundOrder.OrderId.Should().Be(fundOrder.OrderId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OrderAddedToFundEvent.Verb => SetEvent(eventMsg.AsEvent<OrderAddedToFundEvent>()!),
                _ when eventVerb == OrderAddedToFundEvent.Complete => SetEvent(eventMsg.AsEvent<OrderAddedToFundCompleteEvent>()!),
                _ when eventVerb == OrderAddedToFundEvent.Fail => SetEvent(eventMsg.AsEvent<OrderAddedToFundFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OrderAddedToFundEvent orderAdded)
                    orderAddedToFundEvent = orderAdded;
                else if (@event is OrderAddedToFundCompleteEvent orderAddedComplete)
                    orderAddedToFundCompleteEvent = orderAddedComplete;
                else if (@event is OrderAddedToFundFailEvent orderAddedFail)
                    orderAddedToFundFailEvent = orderAddedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task AddOrderToFund_WithOrderAlreadyAddedToFund()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        // create fund...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund first time...
        var fundOrder = SampleData.FundOrder;
        response = await fundApi.AddOrderToFundAsync(fundOrder);

        //await Task.Delay(1000);

        // assert first order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // act... attempt to add the same order again
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        fundApi = new FundCommandApi(commandServiceApi);
        response = await fundApi.AddOrderToFundAsync(fundOrder);

        //await Task.Delay(1000);

        // assert... second order addition should fail
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.Value.Should().NotBe(Guid.Empty);
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain($"orderId {fundOrder.OrderId} already exists");
    }

    [Fact]
    public async Task AddTradeToFundOrder_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradeAddedToFundOrderEvent tradeAddedToFundOrderEvent = default!;
        TradeAddedToFundOrderCompleteEvent tradeAddedToFundOrderCompleteEvent = default!;
        TradeAddedToFundOrderFailEvent tradeAddedToFundOrderFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradeAddedToFundOrderEvent.Actor)] 
                    = [TradeAddedToFundOrderEvent.Verb, TradeAddedToFundOrderCompleteEvent.Verb, TradeAddedToFundOrderFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // add trade to fund order...
        var fundOrderTrade = SampleData.FundOrderTrade;
        await dbFixture.FundDb.DeleteFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);

        subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, fund.Id.Format());
        response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);
        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradeAddedToFundOrderEvent.Should().NotBeNull();
        tradeAddedToFundOrderCompleteEvent.Should().NotBeNull();
        tradeAddedToFundOrderFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var newFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        newFundOrder.Should().NotBeNull();
        newFundOrder!.FundId.Should().Be(fundOrder.FundId);
        newFundOrder.OrderId.Should().Be(fundOrder.OrderId);

        var newFundOrderTrade = await dbFixture.FundDb.GetFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        newFundOrderTrade.Should().NotBeNull();
        newFundOrderTrade!.FundId.Should().Be(fundOrderTrade.FundId);
        newFundOrderTrade.OrderId.Should().Be(fundOrderTrade.OrderId);
        newFundOrderTrade.TradeId.Should().Be(fundOrderTrade.TradeId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradeAddedToFundOrderEvent.Verb => SetEvent(eventMsg.AsEvent<TradeAddedToFundOrderEvent>()!),
                _ when eventVerb == TradeAddedToFundOrderEvent.Complete => SetEvent(eventMsg.AsEvent<TradeAddedToFundOrderCompleteEvent>()!),
                _ when eventVerb == TradeAddedToFundOrderEvent.Fail => SetEvent(eventMsg.AsEvent<TradeAddedToFundOrderFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is TradeAddedToFundOrderEvent tradeAdded)
                    tradeAddedToFundOrderEvent = tradeAdded;
                if (@event is TradeAddedToFundOrderCompleteEvent tradeAddedComplete)
                    tradeAddedToFundOrderCompleteEvent = tradeAddedComplete;
                if (@event is TradeAddedToFundOrderFailEvent tradeAddedFail)
                    tradeAddedToFundOrderFailEvent = tradeAddedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task AddTradeToFundOrder_WithTradeAlreadyAddedToFundOrder()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        // create fund...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add trade to fund order first time...
        var fundOrderTrade = SampleData.FundOrderTrade;
        response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);
        //await Task.Delay(1000);

        // assert first trade addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // act... attempt to add the same trade again
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        fundApi = new FundCommandApi(commandServiceApi);
        response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);

        //await Task.Delay(1000);

        // assert... second trade addition should fail
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.Value.Should().NotBe(Guid.Empty);
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain($"tradeId {fundOrderTrade.TradeId} already exists");
    }

    [Fact]
    public async Task ChangeFundOrderTradeState_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FundOrderTradeStateChangedEvent fundOrderTradeStateChangedEvent = default!;
        FundOrderTradeStateChangedCompleteEvent fundOrderTradeStateChangedCompleteEvent = default!;
        FundOrderTradeStateChangedFailEvent fundOrderTradeStateChangedFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);
        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add trade to fund order...
        var fundOrderTrade = SampleData.FundOrderTrade;
        await dbFixture.FundDb.DeleteFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);
        //await Task.Delay(1000);

        // assert trade addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FundOrderTradeStateChangedEvent.Actor)] 
                    = [FundOrderTradeStateChangedEvent.Verb, FundOrderTradeStateChangedCompleteEvent.Verb, FundOrderTradeStateChangedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // change fund order trade state...
        var fundOrderTradeId = new FundOrderTradeId(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        var newTradeState = TradeState.TradeToClose;

        subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, fund.Id.Format());
        response = await fundApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, newTradeState);
        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        fundOrderTradeStateChangedEvent.Should().NotBeNull();
        fundOrderTradeStateChangedCompleteEvent.Should().NotBeNull();
        fundOrderTradeStateChangedFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var newFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        newFundOrder.Should().NotBeNull();
        newFundOrder!.FundId.Should().Be(fundOrder.FundId);
        newFundOrder.OrderId.Should().Be(fundOrder.OrderId);

        var updatedFundOrderTrade = await dbFixture.FundDb.GetFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        updatedFundOrderTrade.Should().NotBeNull();
        updatedFundOrderTrade!.FundId.Should().Be(fundOrderTrade.FundId);
        updatedFundOrderTrade.OrderId.Should().Be(fundOrderTrade.OrderId);
        updatedFundOrderTrade.TradeId.Should().Be(fundOrderTrade.TradeId);
        updatedFundOrderTrade.TradeState.Should().Be(newTradeState);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FundOrderTradeStateChangedEvent.Verb => SetEvent(eventMsg.AsEvent<FundOrderTradeStateChangedEvent>()!),
                _ when eventVerb == FundOrderTradeStateChangedEvent.CompleteEventName => SetEvent(eventMsg.AsEvent<FundOrderTradeStateChangedCompleteEvent>()!),
                _ when eventVerb == FundOrderTradeStateChangedEvent.Fail => SetEvent(eventMsg.AsEvent<FundOrderTradeStateChangedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FundOrderTradeStateChangedEvent stateChanged)
                    fundOrderTradeStateChangedEvent = stateChanged;
                if (@event is FundOrderTradeStateChangedCompleteEvent stateChangedComplete)
                    fundOrderTradeStateChangedCompleteEvent = stateChangedComplete;
                if (@event is FundOrderTradeStateChangedFailEvent stateChangedFail)
                    fundOrderTradeStateChangedFailEvent = stateChangedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task RemoveOrderFromFund_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        OrderRemovedFromFundEvent orderRemovedFromFundEvent = default!;
        OrderRemovedFromFundCompleteEvent orderRemovedFromFundCompleteEvent = default!;
        OrderRemovedFromFundFailEvent orderRemovedFromFundFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(200);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, OrderRemovedFromFundEvent.Actor)] 
                    = [OrderRemovedFromFundEvent.Verb, OrderRemovedFromFundCompleteEvent.Verb, OrderRemovedFromFundFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // remove order from fund...
        var fundOrderId = new FundOrderId(fundOrder.FundId, fundOrder.OrderId);

        subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, fund.Id.Format());
        response = await fundApi.RemoveOrderFromFundAsync(fundOrderId);
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        orderRemovedFromFundEvent.Should().NotBeNull();
        orderRemovedFromFundCompleteEvent.Should().NotBeNull();
        orderRemovedFromFundFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var removedFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        removedFundOrder.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == OrderRemovedFromFundEvent.Verb => SetEvent(eventMsg.AsEvent<OrderRemovedFromFundEvent>()!),
                _ when eventVerb == OrderRemovedFromFundEvent.CompleteName => SetEvent(eventMsg.AsEvent<OrderRemovedFromFundCompleteEvent>()!),
                _ when eventVerb == OrderRemovedFromFundEvent.Fail => SetEvent(eventMsg.AsEvent<OrderRemovedFromFundFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is OrderRemovedFromFundEvent orderRemoved)
                    orderRemovedFromFundEvent = orderRemoved;
                if (@event is OrderRemovedFromFundCompleteEvent orderRemovedComplete)
                    orderRemovedFromFundCompleteEvent = orderRemovedComplete;
                if (@event is OrderRemovedFromFundFailEvent orderRemovedFail)
                    orderRemovedFromFundFailEvent = orderRemovedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task RemoveTradeFromFundOrder_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradeRemovedFromFundOrderEvent tradeRemovedFromFundOrderEvent = default!;
        TradeRemovedFromFundOrderCompleteEvent tradeRemovedFromFundOrderCompleteEvent = default!;
        TradeRemovedFromFundOrderFailEvent tradeRemovedFromFundOrderFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add trade to fund order...
        var fundOrderTrade = SampleData.FundOrderTrade;
        await dbFixture.FundDb.DeleteFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);
        //await Task.Delay(1000);

        // assert trade addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, TradeRemovedFromFundOrderEvent.Actor)] 
                    = [TradeRemovedFromFundOrderEvent.Verb, TradeRemovedFromFundOrderCompleteEvent.Verb, TradeRemovedFromFundOrderFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // remove trade from fund order...
        var fundOrderTradeId = new FundOrderTradeId(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);

        subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, fund.Id.Format());
        response = await fundApi.RemoveTradeFromFundOrderAsync(fundOrderTradeId);
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        tradeRemovedFromFundOrderEvent.Should().NotBeNull();
        tradeRemovedFromFundOrderCompleteEvent.Should().NotBeNull();
        tradeRemovedFromFundOrderFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var newFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        newFundOrder.Should().NotBeNull();
        newFundOrder!.FundId.Should().Be(fundOrder.FundId);
        newFundOrder.OrderId.Should().Be(fundOrder.OrderId);

        var removedFundOrderTrade = await dbFixture.FundDb.GetFundOrderTradeAsync(fundOrderTrade.FundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
        removedFundOrderTrade.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == TradeRemovedFromFundOrderEvent.Verb => SetEvent(eventMsg.AsEvent<TradeRemovedFromFundOrderEvent>()!),
                _ when eventVerb == TradeRemovedFromFundOrderEvent.CompleteName => SetEvent(eventMsg.AsEvent<TradeRemovedFromFundOrderCompleteEvent>()!),
                _ when eventVerb == TradeRemovedFromFundOrderEvent.Fail => SetEvent(eventMsg.AsEvent<TradeRemovedFromFundOrderFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is TradeRemovedFromFundOrderEvent tradeRemoved)
                    tradeRemovedFromFundOrderEvent = tradeRemoved;
                if (@event is TradeRemovedFromFundOrderCompleteEvent tradeRemovedComplete)
                    tradeRemovedFromFundOrderCompleteEvent = tradeRemovedComplete;
                if (@event is TradeRemovedFromFundOrderFailEvent tradeRemovedFail)
                    tradeRemovedFromFundOrderFailEvent = tradeRemovedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task CloseFundOrder_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FundOrderClosedEvent fundOrderClosedEvent = default!;
        FundOrderClosedCompleteEvent fundOrderClosedCompleteEvent = default!;
        FundOrderClosedFailEvent fundOrderClosedFailEvent = default!;

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FundOrderClosedEvent.Actor)] 
                    = [FundOrderClosedEvent.Verb, FundOrderClosedCompleteEvent.Verb, FundOrderClosedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // close fund order...
        var fundOrderId = new FundOrderId(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.CloseFundOrderAsync(fundOrderId);
        //await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        fundOrderClosedEvent.Should().NotBeNull();
        fundOrderClosedCompleteEvent.Should().NotBeNull();
        fundOrderClosedFailEvent.Should().BeNull();

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var closedFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        closedFundOrder.Should().NotBeNull();
        closedFundOrder!.FundId.Should().Be(fundOrder.FundId);
        closedFundOrder.OrderId.Should().Be(fundOrder.OrderId);
        closedFundOrder.OrderStatus.Should().Be(Shared.OrderStatus.Closed);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FundOrderClosedEvent.Verb => SetEvent(eventMsg.AsEvent<FundOrderClosedEvent>()!),
                _ when eventVerb == FundOrderClosedEvent.ClosedComplete => SetEvent(eventMsg.AsEvent<FundOrderClosedCompleteEvent>()!),
                _ when eventVerb == FundOrderClosedEvent.Fail => SetEvent(eventMsg.AsEvent<FundOrderClosedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FundOrderClosedEvent orderClosed)
                    fundOrderClosedEvent = orderClosed;
                if (@event is FundOrderClosedCompleteEvent orderClosedComplete)
                    fundOrderClosedCompleteEvent = orderClosedComplete;
                if (@event is FundOrderClosedFailEvent orderClosedFail)
                    fundOrderClosedFailEvent = orderClosedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task GenerateFundMaxProfit_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FundMaxProfitGeneratedEvent fundMaxProfitGeneratedEvent = default!;
        FundMaxProfitGeneratedCompleteEvent fundMaxProfitGeneratedCompleteEvent = default!;
        await eventListener.StartAsync(
           "TestEventListener",
           new()
           {
               [new ActorMailboxId(ActorType.Event, FundMaxProfitGeneratedEvent.Actor)] 
                    = [FundMaxProfitGeneratedEvent.Verb, FundMaxProfitGeneratedCompleteEvent.Verb, FundMaxProfitGeneratedFailEvent.Verb]
           },
           EventHandlerAsync
       );

        // create fund...
        var fund = SampleData.NewFund;
        var subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, fund.Id.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);
        var response = await fundApi.CreateFundAsync(fund);

        //await Task.Delay(1000);

        // assert fund creation succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // add order to fund...
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        response = await fundApi.AddOrderToFundAsync(fundOrder);
        //await Task.Delay(1000);

        // assert order addition succeeded...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // generate fund max profit...
        subject = new ActorSubject(ActorType.Command, GenerateFundMaxProfitCommand.Actor, GenerateFundMaxProfitCommand.Verb, fund.Id.Format());
        response = await fundApi.GenerateFundMaxProfitAsync(fundOrder, IFM.Shared.MarketDataAnalytics.TradeTimePeriodType.Daily);
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        var newFund = await dbFixture.FundDb.GetFundAsync(fund.FundId);
        newFund.Should().NotBeNull();
        newFund!.FundId.Should().Be(fund.FundId);

        var newFundOrder = await dbFixture.FundDb.GetFundOrderAsync(fundOrder.FundId, fundOrder.OrderId);
        newFundOrder.Should().NotBeNull();
        newFundOrder!.FundId.Should().Be(fundOrder.FundId);
        newFundOrder.OrderId.Should().Be(fundOrder.OrderId);

        fundMaxProfitGeneratedEvent.Should().NotBeNull();
        fundMaxProfitGeneratedEvent.FundMaxProfit.Should().BeNull();
        fundMaxProfitGeneratedCompleteEvent.Should().NotBeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FundMaxProfitGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FundMaxProfitGeneratedEvent>()!),
                _ when eventVerb == FundMaxProfitGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FundMaxProfitGeneratedCompleteEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FundMaxProfitGeneratedEvent maxProfitGenerated)
                    fundMaxProfitGeneratedEvent = maxProfitGenerated;
                if (@event is FundMaxProfitGeneratedCompleteEvent maxProfitGeneratedComplete)
                    fundMaxProfitGeneratedCompleteEvent = maxProfitGeneratedComplete;
                return @event;
            }
        }
    }
}