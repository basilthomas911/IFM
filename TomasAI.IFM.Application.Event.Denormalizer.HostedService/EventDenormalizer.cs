using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    public class EventDenormalizer : IEventDenormalizer
    {
        private readonly IEventDenormalizerResolver _eventDenormalizerResolver;
        private readonly ILogger _logger;

         public EventDenormalizer(IEventDenormalizerResolver eventDenormalizerResolver, ILogger logger)
        {
            _eventDenormalizerResolver = eventDenormalizerResolver;
            _logger = logger;
        }

        public async Task DenormalizeEventAsync(IEvent domainEvent)
        {
            try
            {
                var eventhandlerType = typeof(IAsyncEventHandler<>);
                var eventDenormalizerType = eventhandlerType.MakeGenericType(domainEvent.GetType());
                dynamic eventDenormalizer = _eventDenormalizerResolver.Resolve(eventDenormalizerType);
                if (eventDenormalizer != null)
                    await eventDenormalizer.ExecuteAsync((dynamic)domainEvent);
            }
            catch(Exception ex)
            {
                _logger?.LogError(ex, $"EventDenormalizer.DenormalizeEventAsync: denormalizer action failed => {domainEvent.GetType().Name}:{domainEvent}");
            }
        }

        public IAsyncEventHandler<TEvent> GetEventHandler<TEvent>() where TEvent : IEvent
        {
            try
            {
                var eventHandlerType = typeof(IAsyncEventHandler<>);
                var eventDenormalizerType = eventHandlerType.MakeGenericType(typeof(TEvent));
                return _eventDenormalizerResolver.Resolve(eventDenormalizerType) as IAsyncEventHandler<TEvent>;
            }
            catch(Exception ex)
            {
                _logger?.LogError(ex, "EventDenormalizer.GetEventHandler: denormalizer action failed");
            }
            return null;
        }
    }
}
