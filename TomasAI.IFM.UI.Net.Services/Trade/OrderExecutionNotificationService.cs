using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Trade;

public interface IOrderExecutionNotificationService
{
    IUiEventSubscription CreateSubscription(Action<OrderExecutionChangedEvent> executionChanged,
        Action<BrokerOrderChangedEvent> brokerChanged);
}

/// <summary>Creates an independent broadcast listener for each Order Fills view.</summary>
public sealed class OrderExecutionNotificationService(Func<IActorEventListener> createListener)
    : IOrderExecutionNotificationService
{
    public IUiEventSubscription CreateSubscription(Action<OrderExecutionChangedEvent> executionChanged,
        Action<BrokerOrderChangedEvent> brokerChanged)
    {
        var listener = createListener();
        return new OwnedUiEventSubscription(token =>
        {
            token.ThrowIfCancellationRequested();
            return listener.StartAsync($"OrderFills-{Guid.NewGuid():N}", new()
            {
                [new(ActorType.Event, OrderExecutionChangedEvent.Actor)] = [OrderExecutionChangedEvent.Verb],
                [new(ActorType.Event, BrokerOrderChangedEvent.Actor)] = [BrokerOrderChangedEvent.Verb]
            }, (verb, message) =>
            {
                if (verb == OrderExecutionChangedEvent.Verb)
                    executionChanged(message.AsEvent<OrderExecutionChangedEvent>()!);
                else if (verb == BrokerOrderChangedEvent.Verb)
                    brokerChanged(message.AsEvent<BrokerOrderChangedEvent>()!);
                return ValueTask.CompletedTask;
            });
        }, listener.StopAsync);
    }
}
