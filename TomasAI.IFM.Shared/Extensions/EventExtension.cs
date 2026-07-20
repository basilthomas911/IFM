using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Extensions;

/// <summary>
/// Provides extension methods for the IEvent interface to facilitate event manipulation, routing, and serialization
/// within domain-driven applications.
/// </summary>
/// <remarks>This static class includes methods for setting event properties such as source, subject, and received
/// timestamp, as well as for routing events and serializing them to JSON. These extensions are intended to streamline
/// common event-handling scenarios and promote consistency when working with domain events.</remarks>
public  static class EventExtension
{
    public static void CheckForEmptyCommandId(this IEvent @event)
    {
        if (@event.CommandId == Guid.Empty)
            throw new InvalidOperationException($"CheckForEmptyCommandId: {@event.GetType().Name}.CommandId is empty");
    }

    public static void CheckForEmptyCommandId(this ICommand cmd)
    {
        if (cmd.CommandId == Guid.Empty)
            throw new InvalidOperationException($"CheckForEmptyCommandId: {cmd.GetType().Name}.CommandId is empty");
    }


    public static IEvent SetEventSource(this IEvent @event, string eventSource)
    {
        EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
        return @event;
    }

    public static IEvent SetReceivedOn(this IEvent @event, DateTime receivedOn)
    {
        EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.ReceivedOn), receivedOn);
        return @event;
    }


    public static IEvent RoutedFrom(this IEvent @event, Guid correlationId, string aggregateId, string eventSource)
    {
        EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.CommandId), correlationId);
        EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.AggregateId), aggregateId);
        EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
        return @event;
    }

    public static IEvent RoutedFrom(this IEvent @event)
        => RoutedFrom(@event, @event.CommandId, @event.AggregateId, @event.EventSource);

    public static IEvent RoutedFrom(this IEvent @event, ICommand command) 
        => RoutedFrom(@event, command.CommandId, command.StreamId, command.EventSource);

    public static string ToEventData(this IEvent domainEvent) 
        => JsonConvert.SerializeObject(domainEvent, Formatting.Indented);

}
