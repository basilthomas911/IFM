using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public class EventHandlerResolver : IEventHandlerResolver
    {
        private Func<Type, object[]> _resolverFunction;

        /// <summary>
        /// create event handler resolver
        /// </summary>
        /// <param name="resolverFunction">function that will return query handler using dependancy injection</param>
        public EventHandlerResolver(Func<Type, object[]> resolverFunction)
            => _resolverFunction = resolverFunction;

        /// <summary>
        /// call resolver function to return event handler from dependancy injection container
        /// </summary>
        /// <param name="eventHandlerType"></param>
        /// <returns></returns>
        public object[] Resolve(Type eventHandlerType)
        {
            var eventHandlers = default(object[]);
            try
            {
                eventHandlers = _resolverFunction(eventHandlerType);
            }
            catch { }
            return eventHandlers;
        }
        
    }
}
