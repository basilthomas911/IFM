using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.StatusConsole
{
    /// <summary>
    /// status console event consumer
    /// </summary>
    public class StatusConsoleEventConsumer : KafkaEventConsumer, IStatusConsoleEventConsumer
    {
        private Action<StatusConsoleLoggedEvent> _eventAction;
        private Guid _siteId;

        /// <summary>
        /// status console event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public StatusConsoleEventConsumer(IEventConsumerOptions options, ILogger logger) : base(options, logger)
        {
        }

        /// <summary>
        /// subscribe to status console events
        /// </summary>
        protected override void ConnectEvents()
        {
            Subscribe($"{_siteId}",
                new IEvent[] { new StatusConsoleLoggedEvent { }.SetEventSource($"{EventTopic.StatusConsoleEvents}") },
                e => _eventAction((dynamic)e));
        }

        /// <summary>
        /// start status console consumer
        /// </summary>
        /// <param name="eventAction"></param>
        /// <param name="siteId"></param>
        /// <returns></returns>
        public async Task StartAsync(Action<StatusConsoleLoggedEvent> eventAction, Guid siteId)
        {
            _siteId = siteId;
            _eventAction = eventAction;
            await base.StartAsync();
        }

        /// <summary>
        /// stop status console consumer
        /// </summary>
        /// <param name="siteId"></param>
        /// <returns></returns>
        public async Task StopAsync(Guid siteId)
        {
            _siteId = Guid.Empty;
            _eventAction = null;
            await base.StopAsync();
        }
    }
}
