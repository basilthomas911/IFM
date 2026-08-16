using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Plan;

public class TradePlanCommandApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task UpdateTradePlan_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanUpdatedEvent tradePlanUpdatedEvent = default!;
        var eventReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        await ClearEventStreamAsync(subject);

        await dbFixture.TradeDb.InsertTradePlanAsync(tradePlan);

        // act...
        var tradeApi = new TradePlanCommandApi(_actorProducer);
        var response = await tradeApi.UpdateTradePlanAsync(tradePlan);
        await eventReceived.Task.WaitAsync(EventTimeout);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
                eventReceived.TrySetResult(true);
                return @event;
            }
        }
    }

    [Fact(Skip = "Legacy TradePlan forward-loss actors are deferred until replacement by the trade-monitor workflow pipeline.")]
    public async Task UpdateTradePlanForwardLossLimit_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanForwardLossLimitUpdatedEvent forwardLossLimitUpdatedEvent = default!;
        var eventReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command,
            UpdateTradePlanForwardLossLimitCommand.Actor,
            UpdateTradePlanForwardLossLimitCommand.Verb,
            entityId.Format()));
        await dbFixture.TradeDb.DeleteTradePlanForwardLossLimitAsync(entityId);

        // act...
        var tradeApi = new TradePlanCommandApi(_actorProducer);
        var response = await tradeApi.UpdateTradePlanForwardLossLimitAsync(forwardLossLimit);
        await eventReceived.Task.WaitAsync(EventTimeout);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
                eventReceived.TrySetResult(true);
                return @event;
            }
        }
    }

    [Fact(Skip = "Legacy TradePlan forward-loss actors are deferred until replacement by the trade-monitor workflow pipeline.")]
    public async Task ClearTradePlanForwardLossLimit_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        TradePlanForwardLossLimitClearedEvent forwardLossLimitClearedEvent = default!;
        var eventReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command,
            ClearTradePlanForwardLossLimitCommand.Actor,
            ClearTradePlanForwardLossLimitCommand.Verb,
            entityId.Format()));
        await dbFixture.TradeDb.DeleteTradePlanForwardLossLimitAsync(entityId);
        await dbFixture.TradeDb.InsertTradePlanForwardLossLimitAsync(forwardLossLimit);

        // act...
        var tradeApi = new TradePlanCommandApi(_actorProducer);
        var response = await tradeApi.ClearTradePlanForwardLossLimitAsync(entityId);
        await eventReceived.Task.WaitAsync(EventTimeout);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
                eventReceived.TrySetResult(true);
                return @event;
            }
        }
    }

    async Task ClearEventStreamAsync(ActorSubject subject)
    {
        dbFixture.BlackboardService.EventSourcing.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
    }
}
