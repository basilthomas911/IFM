using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public class LocalEventBus : IEventBus
    {
        private IEventHandlerResolver _eventHandlerResolver;

        public LocalEventBus(IEventHandlerResolver eventHandlerResolver)
            => _eventHandlerResolver = eventHandlerResolver;

        /// <summary>
        /// call event handler for each domain event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="domainEvents"></param>
        public async Task PublishAsync(object sender, IDomainEvent[] domainEvents)
        {
            if (domainEvents != null)
                foreach (var domainEvent in domainEvents)
                    await ExecuteAsync(domainEvent);
        }

        /// <summary>
        /// call async handler for each domain event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="domainEvents"></param>
        /// <returns></returns>
        public async Task PostAsync(object sender, IDomainEvent[] domainEvents)
        {
            if (domainEvents != null)
                foreach (var domainEvent in domainEvents)
                    await ExecuteAsync(domainEvent);
        }

        /// <summary>
        /// execute event handler resolved from reolved event type
        /// </summary>
        /// <param name="domainEvent"></param>
        private void Execute(IDomainEvent domainEvent)
        {
            try
            {
                // domain event cannot be null...
                if (domainEvent == null)
                    throw new ArgumentException("LocalEventBus.Execute: domainEvent parameter is null");

                // instantiate event handler...
                var eventHandlerType = typeof(IEventHandler<>).MakeGenericType(domainEvent.GetType());
                var eventHandlers = _eventHandlerResolver.Resolve(eventHandlerType);

                // check if query handler exists...
                if (eventHandlers == null)
                    throw new InvalidOperationException($"LocalEventBus.Execute: unable to load any event handers for type: {eventHandlerType.Name}");

                // execute all event handlers that can handle event...
                foreach (dynamic eventHandler in eventHandlers)
                    try { eventHandler.Execute((dynamic)domainEvent); }
                    catch { }
            }
            catch (Exception ex)
            {
                //_logger.Error(ex, "SimpleEventBus.Execute fatal error: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// execute event handler resolved from reolved event type
        /// </summary>
        /// <param name="domainEvent"></param>
        private async Task ExecuteAsync(IDomainEvent domainEvent)
        {
            try
            {
                // domain event cannot be null...
                if (domainEvent == null)
                    throw new ArgumentException("LocalEventBus.ExecuteAsync: domainEvent parameter is null");

                // instantiate event handler...
                var eventHandlerType = typeof(IAsyncEventHandler<>).MakeGenericType(domainEvent.GetType());
                var eventHandlers = _eventHandlerResolver.Resolve(eventHandlerType);

                // check if query handler exists...
                if (eventHandlers == null)
                    throw new InvalidOperationException($"LocalEventBus.ExecuteAsync: unable to load any event handers for type: {eventHandlerType.Name}");

                // execute all event handlers that can handle event...
                foreach (dynamic eventHandler in eventHandlers)
                    try { await eventHandler.ExecuteAsync((dynamic)domainEvent); }
                    catch { }
            }
            catch (Exception ex)
            {
                //_logger.Error(ex, "SimpleEventBus.ExecuteAsync fatal error: {ErrorMessage}", ex.Message);
            }
        }
    }
}
