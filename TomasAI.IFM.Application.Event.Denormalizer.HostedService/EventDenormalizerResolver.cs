using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// event denormalizer resolver
    /// </summary>
    public class EventDenormalizerResolver : IEventDenormalizerResolver
    {
        private Func<Type, object> _resolverFunction;

        /// <summary>
        /// create event handler resolver
        /// </summary>
        /// <param name="resolverFunction">function that will return event handler using dependancy injection</param>
        public EventDenormalizerResolver(Func<Type, object> resolverFunction) => _resolverFunction = resolverFunction;

        /// <summary>
        /// call resolver function to return event handler from dependancy injection container
        /// </summary>
        /// <param name="eventHandlerType"></param>
        /// <returns></returns>
        public object Resolve(Type eventHandlerType)
        {
            var eventHandler = default(object);
            try
            {
                eventHandler = _resolverFunction(eventHandlerType);
            }
            catch { }
            return eventHandler;
        }
    }

}