using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.EventConsumers
{
    /// <summary>
    /// trade strategy event consumer 
    /// </summary>
    public class TradeStrategyEventConsumer : KafkaObservable, ITradeStrategyEventConsumer
    {
        readonly SemaphoreSlim _semaphore;
        private IDisposable _observer;

        /// <summary>
        /// trade strategy event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public TradeStrategyEventConsumer(IEventConsumerOptions options, ILogger logger)
            :base(options, logger)
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// start event consumer
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="consumeEvents"></param>
        /// <param name="eventAction"></param>
        /// <returns></returns>
        public async Task StartAsync(Guid siteId, ICollection<IEvent> consumeEvents, Action<IEvent> consumerAction)
        {
            await _semaphore.WaitAsync();
            try
            {
                _observer = Subscribe(new KafkaObserver($"{siteId}", consumeEvents, consumerAction));
                await StartAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// stop event consumer
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync() 
        {
            _observer?.Dispose();
            await Task.CompletedTask;
        }
        
    }
}
