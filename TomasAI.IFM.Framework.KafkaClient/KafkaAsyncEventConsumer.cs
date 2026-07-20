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
    public abstract class KafkaAsyncEventConsumer : IEventConsumer
    {
        static ConcurrentDictionary<Guid, string> _duplicateEventMap;
        readonly IEventConsumerOptions _options;
        readonly ILogger _logger;
        EventConsumerState _consumerState;

        /// <summary>
        /// Kafka async event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public KafkaAsyncEventConsumer(IEventConsumerOptions options, ILogger logger)
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
                ConnectEvents();
                await Task.CompletedTask;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"{GetType().Name}.StartAsync:  {ex.GetErrorMessage()}");
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
        protected void Subscribe(string siteName, ICollection<IEvent> consumeEvents, Func<IEvent, Task> consumerAction)
        {
            IsArgumentNull.Check(consumeEvents);
            IsArgumentNull.Check(consumerAction);

            _consumerState = new EventConsumerState(siteName, new CancellationTokenSource());
            var waitId = _consumerState.Start(consumerAction);
            ThreadPool.QueueUserWorkItem(_ => ConsumerThread(waitId, consumeEvents));
            _consumerState.WaitForConsumerStart(waitId);
        }
       
        /// <summary>
        /// create consumer thread to process events we have subscribed to
        /// </summary>
        /// <param name="waitId"></param>
        /// <param name="consumeEvents"></param>
        void ConsumerThread(
            Guid waitId,
            ICollection<IEvent> consumeEvents)
        {
            var consumerConfig = new ConsumerConfig
            {
                // the group.id property must be specified when creating a consumer, even 
                // if you do not intend to use any consumer group functionality.
                GroupId = _consumerState.SiteName,
                BootstrapServers = _options.BootstrapServers,
                // partition offsets can be committed to a group even by consumers not
                // subscribed to the group.
                EnableAutoCommit = _options.EnableAutoCommit
            };

            try
            {
                using var eventBroker = new ConsumerBuilder<string, string>(consumerConfig)
                    .SetErrorHandler((_, e) => Console.WriteLine($"{GetType().Name} Error: {e.Reason}"))
                    .Build();
                eventBroker.Subscribe(consumeEvents.Select(e => e.GetType().Name));
                _consumerState.SetConsumerStart(ProcessConsumedEventAsync, waitId);
                while (true)
                {
                    var consumeResult = eventBroker.Consume(_consumerState.CancellationToken);
                    if (consumeResult is not null)
                        _consumerState.ConsumerEventQueue.EnqueueAndSignal(consumeResult);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, $"{GetType().Name}.ConsumerThread:  exiting thread");
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, $"{GetType().Name}.ConsumerThread:  consume exception {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetType().Name}ConsumerThread:  unhandled exception {ex.Message}");
            }
            return;

            async Task ProcessConsumedEventAsync(ConsumeResult<string,string> consumeResult)
            {
                var eventType = GetEventType(consumeResult.Message);
                if (consumeEvents.Any(e => e.GetType().Name == eventType.Name))
                {
                    var consumedEvent = JsonConvert.DeserializeObject(consumeResult.Message.Value, eventType) as IEvent;
                    if (!_duplicateEventMap.ContainsKey(consumedEvent.Id))
                    {
                        if (_duplicateEventMap.TryAdd(consumedEvent.Id, consumedEvent.GetType().Name))
                        {
                            consumedEvent
                                .SetEventSource(consumeResult.Topic)
                                .SetReceivedOn(consumeResult.Message.Timestamp.UtcDateTime);
                            await _consumerState.ConsumerAction(consumedEvent);
                        }
                        if (_duplicateEventMap.Count >= 1000)
                            _duplicateEventMap.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// return name of consumed event from assembly qualified name in message header
        /// </summary>
        /// <param name="consumeResult"></param>
        /// <returns></returns>
        Type GetEventType(Message<string, string> message)
        {
            var header = message.Headers.Single(e => e.Key == "EventAssemblyQualifiedName");
            var eventTypeName = Encoding.ASCII.GetString(header.GetValueBytes());
            return Type.GetType(eventTypeName);
        }

        class EventConsumerState
        {
            readonly Dictionary<Guid, AutoResetEvent> _resetEventMap;
            bool _started;
            ConcurrentAsyncEventQueue<ConsumeResult<string, string>> _consumerEventQueue;
            Func<IEvent, Task> _consumerAction;
            CancellationTokenSource _cts;
            CancellationToken _cancellationToken;

            public EventConsumerState(string siteName, CancellationTokenSource cts)
            {
                _cts = cts;
                _cancellationToken = cts.Token;
                _resetEventMap = new Dictionary<Guid, AutoResetEvent>();
                _started = false;
                Id = Guid.NewGuid();
                SiteName = siteName;
            }

            public Guid Id { get; }
            public string SiteName { get; }
            public bool Started => _started;
            public bool Stopped => !_started;
            public ConcurrentAsyncEventQueue<ConsumeResult<string, string>> ConsumerEventQueue => _consumerEventQueue;
            public Func<IEvent, Task> ConsumerAction => _consumerAction;
            public CancellationToken CancellationToken => _cancellationToken;

            public Guid Start(Func<IEvent, Task> consumerAction) 
            {
                var waitId = Guid.NewGuid();
                _resetEventMap.Add(waitId, new AutoResetEvent(false));
                _consumerAction = consumerAction;
                _started = true;
                return waitId;
            }

            public void Stop()
            {
                if (_started)
                {
                    _cts.Cancel();
                    _consumerEventQueue.Stop();
                    _resetEventMap.Clear();
                    _started = false;
                    _consumerAction = null;
                }
            }

            public void WaitForConsumerStart(Guid waitId)
            {
                if (_resetEventMap.ContainsKey(waitId))
                    _resetEventMap[waitId].WaitOne();
            }

            public void SetConsumerStart(Func< ConsumeResult<string, string>, Task> processConsumedEvent, Guid waitId) 
            {
                _consumerEventQueue = new ConcurrentAsyncEventQueue<ConsumeResult<string, string>>(processConsumedEvent);
                _consumerEventQueue.Start();
                if (_resetEventMap.ContainsKey(waitId))
                    _resetEventMap[waitId].Set();
            }

        }
    }
}
