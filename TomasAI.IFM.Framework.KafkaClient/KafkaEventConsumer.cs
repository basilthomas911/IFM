using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventQueue;

namespace TomasAI.IFM.Framework.Messaging.Kafka
{
    /// <summary>
    /// Kafka event consumer
    /// </summary>
    public abstract class KafkaEventConsumer : IEventConsumer
    {
        static ConcurrentDictionary<Guid, string> _duplicateEventMap;
        readonly IEventConsumerOptions _options;
        readonly ILogger _logger;
        EventConsumerState _consumerState;
        Thread _consumerThread;

        /// <summary>
        /// Kafka event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public KafkaEventConsumer(IEventConsumerOptions options, ILogger logger)
        {
            _options = IsArgumentNull.Set(options);
            _logger = IsArgumentNull.Set(logger);
            _duplicateEventMap ??= new ConcurrentDictionary<Guid, string>();
        }

        public async Task ClearConsumerEventQueueAsync()
        {
            _consumerState?.ConsumerEventQueue?.Clear();
            await Task.CompletedTask;
        }

        /// <summary>
        /// start all consumer events defined from overriden ConnectEvents method
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                this.ConnectEvents();
                await Task.CompletedTask;
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent("KafkaEventConsumer", ex, $"StartAsync:  {ex.GetErrorMessage()}");
            }
        }
  
        /// <summary>
        /// stop consumer event thread
        /// </summary>
        public async Task StopAsync()
        {
            _consumerState?.Stop();
            await Task.CompletedTask;
        }

        /// <summary>
        /// call event connection method On for each event that will be consumed
        /// </summary>
        protected abstract void ConnectEvents();

        /// <summary>
        /// subscribe to list of events that are consumed from event broker
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="consumeEvents"></param>
        /// <param name="consumerAction"></param>
        protected void Subscribe(string siteName, ICollection<IEvent> consumeEvents, Action<IEvent> consumerAction)
        {
            IsArgumentNull.Check(consumeEvents);
            IsArgumentNull.Check(consumerAction);
            _consumerState = new EventConsumerState(siteName);
            var waitId = _consumerState.Start();
            //ThreadPool.QueueUserWorkItem(_ => ConsumerThread(waitId, consumeEvents.ToArray(), e => consumerAction(e)));

            _consumerThread = new Thread(_ => ConsumerThread(waitId, consumeEvents.ToArray(), e => consumerAction(e)));
            _consumerThread.Priority = ThreadPriority.AboveNormal;
            _consumerThread.Start();
            _consumerState.WaitForConsumerStart(waitId);
        }
       
        /// <summary>
        /// create consumer thread to process events we have subscribed to
        /// </summary>
        /// <param name="waitId"></param>
        /// <param name="consumeEvents"></param>
        /// <param name="consumerAction"></param>
        void ConsumerThread(
            Guid waitId,
            ICollection<IEvent> consumeEvents,
            Action<IEvent> consumerAction)
        {
            var consumerConfig = new ConsumerConfig
            {
                // the group.id property must be specified when creating a consumer, even 
                // if you do not intend to use any consumer group functionality.
                GroupId = _consumerState.SiteName,
                BootstrapServers = _options.BootstrapServers,
                // partition offsets can be committed to a group even by consumers not
                // subscribed to the group.
                EnableAutoCommit = true, // _options.EnableAutoCommit,
                //EnableAutoOffsetStore = false,
                //AutoOffsetReset = AutoOffsetReset.Latest,
                //MaxPollIntervalMs = 10000
            };
            _consumerState.SetConsumerEventQueue(new ConcurrentEventQueue<ConsumeResult<string, string>>( ProcessConsumedEvent ));
            try
            {
                using (var consumer = new ConsumerBuilder<string, string>(consumerConfig)
                        .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                        .Build())
                {
                    _consumerState.ConsumerEventQueue.Start();
                    consumer.Subscribe(consumeEvents.Select(e => e.GetType().Name));
                    _consumerState.SetConsumerStart(waitId);
                    while (!_consumerState.ExitThread)
                    {
                        var consumeResult = consumer.Consume();
                        if (consumeResult is not null)
                            _consumerState.ConsumerEventQueue.EnqueueAndSignal(consumeResult);
                        if (_consumerState.Stopped)
                            break;
                    }
                    _consumerState.ConsumerEventQueue.Stop();
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogErrorEvent("KafkaEventConsumer", ex, $"ConsumerThread:  consume exception {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent("KafkaEventConsumer", ex, $"ConsumerThread:  unhandled exception {ex.Message}");
            }
            _logger.LogWarning($"KafkaEventConsumer.ConsumerThread:  exiting thread");
            return;

            void ProcessConsumedEvent(ConsumeResult<string,string> consumeResult)
            {
                var eventType = GetEventType(consumeResult.Message);
                if (consumeEvents.Any(e => e.GetType() == eventType))
                {
                    var consumedEvent = JsonConvert.DeserializeObject(consumeResult.Message.Value, eventType) as IEvent;
                    if (!_duplicateEventMap.ContainsKey(consumedEvent.Id))
                    {
                        if (_duplicateEventMap.TryAdd(consumedEvent.Id, consumedEvent.GetType().Name))
                        {
                            consumedEvent
                                .SetEventSource(consumeResult.Topic)
                                .SetReceivedOn(consumeResult.Message.Timestamp.UtcDateTime);
                            consumerAction(consumedEvent);
                        }
                        if (_duplicateEventMap.Count >= 1000)
                            _duplicateEventMap.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// return type of consume event from assembly qualified name in message header
        /// </summary>
        /// <param name="consumeResult"></param>
        /// <returns></returns>
        Type GetEventType(Message<string, string> message)
        {
            var header = message.Headers.Single(e => e.Key == "EventAssemblyQualifiedName");
            var eventAssemblyName = Encoding.ASCII.GetString(header.GetValueBytes());
            return Type.GetType(eventAssemblyName);
        }

        class EventConsumerState
        {
            readonly Dictionary<Guid, AutoResetEvent> _resetEventMap;
            bool _exitThread;
            bool _started;
            ConcurrentEventQueue<ConsumeResult<string, string>> _consumerEventQueue;


            public EventConsumerState(string siteName)
            {
                _resetEventMap = new Dictionary<Guid, AutoResetEvent>();
                _exitThread = false;
                Id = Guid.NewGuid();
                SiteName = siteName;
            }

            public Guid Id { get; }
            public string SiteName { get; }
            public bool ExitThread => _exitThread;
            public bool Started => _started;
            public bool Stopped => !_started;
            public ConcurrentEventQueue<ConsumeResult<string, string>> ConsumerEventQueue => _consumerEventQueue;

            public Guid Start() 
            {
                _exitThread = false;
                _started = true;
                var waitId = Guid.NewGuid();
                _resetEventMap.Add(waitId, new AutoResetEvent(false));
                return waitId;
            }

            public void Stop()
            {
                _resetEventMap.Clear();
                _exitThread = true;
                _started = false;
            }

            public void WaitForConsumerStart(Guid waitId)
            {
                if (_resetEventMap.ContainsKey(waitId))
                    _resetEventMap[waitId].WaitOne();
            }

            public void SetConsumerStart(Guid waitId) 
            {
                if (_resetEventMap.ContainsKey(waitId))
                    _resetEventMap[waitId].Set();
            }

            public void SetConsumerEventQueue(ConcurrentEventQueue<ConsumeResult<string, string>> consumerEventQueue) => _consumerEventQueue = consumerEventQueue;

        }
    }
}
