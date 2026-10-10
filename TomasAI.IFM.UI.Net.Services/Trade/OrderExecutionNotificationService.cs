using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Trade;

public interface IOrderExecutionNotificationService
{
    /// <summary>Creates a listener for committed and projected Fund setup lifecycle changes.</summary>
    IUiEventSubscription CreateFundSubscription(Action<TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundManualOrderChangedEvent> changed);
    IUiEventSubscription CreateSubscription(Action<OrderExecutionChangedEvent> executionChanged,
        Action<BrokerOrderChangedEvent> brokerChanged);
}

/// <summary>Creates an independent broadcast listener for each Order Fills view.</summary>
public sealed class OrderExecutionNotificationService(Func<IActorEventListener> createListener)
    : IOrderExecutionNotificationService
{
    /// <summary>Creates an independently owned Fund listener for a Trade Orders editor.</summary>
    public IUiEventSubscription CreateFundSubscription(Action<TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundManualOrderChangedEvent> changed)
    {
        var listener = createListener();
        return new OwnedUiEventSubscription(token =>
        {
            token.ThrowIfCancellationRequested();
            return listener.StartAsync($"FundTradeLifecycle-{Guid.NewGuid():N}", new()
            {
                [new(ActorType.Event, TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundManualOrderChangedEvent.Actor)] =
                    [TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundManualOrderChangedEvent.Verb]
            }, (_, message) =>
            {
                changed(message.AsEvent<TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundManualOrderChangedEvent>()!);
                return ValueTask.CompletedTask;
            });
        }, listener.StopAsync);
    }

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
