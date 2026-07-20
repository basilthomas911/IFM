using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventQueue;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public class EventActionBlock : IEventActionBlock
    {
        private readonly IEventHandlerResolver _eventHandlerResolver;
        private readonly ILogDbContext _logDb;
        private readonly IStatusConsoleServiceApi _statusConsoleServiceApi;
        private readonly ConcurrentEventQueue<(long EventId, string EventTypeName, string SerializedEvent)> _eventQueue;

        public EventActionBlock(IEventHandlerResolver eventHandlerResolver, ILogDbContext logDb, IStatusConsoleServiceApi statusConsoleServiceApi)
        {
            _eventHandlerResolver = eventHandlerResolver;
            _logDb = logDb;
            _statusConsoleServiceApi = statusConsoleServiceApi;
            _eventQueue = new ConcurrentEventQueue<(long EventId, string EventTypeName, string SerializedEvent)>(async e => await HandlePostEvent(e));
            _eventQueue.Start();
        }

        public async Task PublishEventAsync(long eventId, string eventTypeName, string serializedEvent)
        {
            var eventType = Type.GetType(eventTypeName);
            await HandleEvent(eventType, serializedEvent);
        }

        public void PostEvent(long eventId, string eventTypeName, string serializedEvent)
        {
            _eventQueue.EnqueueForSignal((eventId, eventTypeName, serializedEvent));
            _eventQueue.Signal();
        }

        private async Task HandlePostEvent((long EventId, string EventTypeName, string SerializedEvent) e)
        {
            var eventType = Type.GetType(e.EventTypeName);
            try
            {
                await HandleEvent(eventType, e.SerializedEvent);
            }
            catch (Exception ex)
            {
                await _logDb.InsertEventQueueLogAsync(e.EventId, eventType.FullName, EventQueueStatus.Failed, DateTime.Now, ex.Message);
                await _statusConsoleServiceApi.StatusConsoleLogUpdatedAsync(new StatusConsoleLoggedEvent
                {
                    StatusConsoleLog = new StatusConsoleLogReadModel
                    {
                        StatusDate = DateTime.Now,
                        StatusCode = -1000,
                        Source = LogSourceType.Event,
                        Message = $"EventError: {eventType.Name} => {ex.GetErrorMessage()}",
                        DataType = ex.GetType().Name,
                        Data = ex.ToString()
                    }
                });

            }
        }

        private async Task HandleEvent(Type eventType, string serializedEvent)
        {
            var domainEvent = JsonConvert.DeserializeObject(serializedEvent, eventType);
            var eventHandlerType = typeof(IAsyncEventHandler<>).MakeGenericType(eventType);
            var eventHandlers = _eventHandlerResolver.Resolve(eventHandlerType);
            if (eventHandlers != null)
            {
                // execute all event handlers that can handle event...
                foreach (dynamic eventHandler in eventHandlers)
                    await eventHandler?.ExecuteAsync((dynamic)domainEvent);
            }
        }
    }
}
