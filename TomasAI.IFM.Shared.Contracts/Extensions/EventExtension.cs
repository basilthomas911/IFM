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
public static class EventExtension
{

    extension(IEvent @event)
    {
        public void CheckForEmptyCommandId()
        {
            ArgumentNullException.ThrowIfNull(@event);
            if (@event.CommandId == Guid.Empty)
                throw new InvalidOperationException($"CheckForEmptyCommandId: {@event.GetType().Name}.CommandId is empty");
        }
    }

    extension(ICommand cmd)
    {
        public void CheckForEmptyCommandId()
        {
            ArgumentNullException.ThrowIfNull(cmd);
            if (cmd.CommandId == Guid.Empty)
                throw new InvalidOperationException($"CheckForEmptyCommandId: {cmd.GetType().Name}.CommandId is empty");
        }
    }

    extension(IEvent @event)
    {
        public IEvent SetEventSource(string eventSource)
        {
            EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
            return @event;
        }

        public IEvent SetReceivedOn(DateTime receivedOn)
        {
            EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.ReceivedOn), receivedOn);
            return @event;
        }

        public IEvent RoutedFrom(Guid correlationId, string aggregateId, string eventSource)
        {
            EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.CommandId), correlationId);
            EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.AggregateId), aggregateId);
            EventModelActor.EventInitHelper.SetProperty(@event, nameof(IEvent.EventSource), eventSource);
            return @event;
        }

        public IEvent RoutedFrom()

        {
            ArgumentNullException.ThrowIfNull(@event);
            return RoutedFrom(@event, @event.CommandId, @event.AggregateId, @event.EventSource);
        }

        public IEvent RoutedFrom(ICommand command)

        {
            ArgumentNullException.ThrowIfNull(command);
            return RoutedFrom(@event, command.CommandId, command.StreamId, command.EventSource);
        }
    }

    extension(IEvent domainEvent)
    {
        public string ToEventData()
            => JsonConvert.SerializeObject(domainEvent, Formatting.Indented);
    }

}
