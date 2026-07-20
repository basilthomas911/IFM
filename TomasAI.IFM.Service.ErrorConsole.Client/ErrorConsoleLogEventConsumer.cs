using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.ErrorConsole.Client
{
    /// <summary>
    /// error console event consumer
    /// </summary>
    public class ErrorConsoleLogEventConsumer : KafkaEventConsumer, IErrorConsoleLogEventConsumer
    {
        private Action<ErrorConsoleLoggedEvent> _eventAction;
        private Guid _siteId;

        /// <summary>
        /// error console event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public ErrorConsoleLogEventConsumer(IEventConsumerOptions options, ILogger logger) : base(options, logger)
        {
        }

        /// <summary>
        /// consume only error console logged event
        /// </summary>
        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new ErrorConsoleLoggedEvent { }.SetEventSource($"{EventTopic.ErrorConsoleEvents}") }, 
                e => _eventAction?.Invoke(e as ErrorConsoleLoggedEvent));

        /// <summary>
        /// start event consumer
        /// </summary>
        /// <param name="eventAction"></param>
        /// <param name="actionGuid"></param>
        /// <returns></returns>
        public async Task StartAsync(Action<ErrorConsoleLoggedEvent> eventAction, Guid siteId)
        {
            _eventAction = eventAction;
            _siteId = siteId;
            await base.StartAsync();

        }

        /// <summary>
        /// stop event consumer
        /// </summary>
        /// <param name="actionGuid"></param>
        /// <returns></returns>
        public async Task StopAsync(Guid actionGuid)
        {
            _eventAction = null;
            _siteId = Guid.Empty;
            await base.StopAsync();
        }
    }
}
