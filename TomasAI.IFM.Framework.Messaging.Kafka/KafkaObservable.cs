using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventQueue;

namespace TomasAI.IFM.Framework.Messaging.Kafka
{
    /// <summary>
    /// Kafka event observable
    /// </summary>
    public abstract class KafkaObservable : IObservable<IEvent>
    {
        private static ConcurrentDictionary<Guid, string> _duplicateEventMap;
        private readonly ConcurrentStack<KafkaObserver> _observers;
        private readonly IEventConsumerOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Kafka event consumer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public KafkaObservable(IEventConsumerOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _duplicateEventMap ??= new ConcurrentDictionary<Guid, string>();
            _observers = new ConcurrentStack<KafkaObserver>();
        }

        /// <summary>
        /// observer subscription that will consume events from event broker
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<IEvent> observer)
        {
            var kafkaObserver = observer is KafkaObserver ? observer as KafkaObserver : throw new ArgumentNullException(nameof(observer));
            _observers.Push(kafkaObserver);
            return observer as IDisposable;
        }

        /// <summary>
        /// start all consumer events defined from overriden ConnectEvents method
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                if (_observers.Count != 1) throw new InvalidOperationException("KafkaObservable.StartAsync: KafkaObservable has not been subscribed to by a single KafkaObserver");
                KafkaObserver observer;
                _observers.TryPop(out observer); 
                if (observer.ConsumeEvents is null) throw new ArgumentNullException(nameof(observer.ConsumeEvents));
                if (observer.ConsumerAction is null) throw new ArgumentNullException(nameof(observer.ConsumerAction));

                observer.Start();
                ThreadPool.QueueUserWorkItem(o => ConsumerThread(o as KafkaObserver), observer);
                observer.WaitForConsumerStart(); 
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"KafkaEventConsumer.StartAsync:  {ex.GetErrorMessage()}");
            }
        }

        /// <summary>
        /// create consumer thread to process events we have subscribed to
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="waitId"></param>
        /// <param name="subscribeEvents"></param>
        /// <param name="consumeEvents"></param>
        /// <param name="consumerAction"></param>
        private void ConsumerThread(KafkaObserver observer)
        {
            var consumerConfig = new ConsumerConfig
            {
                // the group.id property must be specified when creating a consumer, even 
                // if you do not intend to use any consumer group functionality.
                GroupId = observer.SiteName,
                BootstrapServers = _options.BootstrapServers,
                // partition offsets can be committed to a group even by consumers not
                // subscribed to the group.
                EnableAutoCommit = _options.EnableAutoCommit
            };
            var retryCount = 0;
            var consumerEventQueue = new ConcurrentEventQueue<ConsumeResult<string, string>>(o => ProcessConsumedEvent(o));
            do
            {
                try
                {
                    using (var consumer = new ConsumerBuilder<string, string>(consumerConfig)
                            .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                            .Build())
                    {
                        consumerEventQueue.Start();
                        consumer.Subscribe(observer.ConsumeEvents.Select(e => e.GetType().Name));
                        observer.SetConsumerStart();
                        while (!observer.ExitThread)
                        {
                            var consumeResult = consumer.Consume();
                            if (consumeResult != null)
                            {
                                consumerEventQueue.EnqueueForSignal(consumeResult);
                                consumerEventQueue.Signal();
                            }
                            if (observer.Stopped)
                                return;
                        }
                        consumerEventQueue.Stop();
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, $"KafkaEventConsumer.ConsumerThread:  consume exception {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"KafkaEventConsumer.ConsumerThread:  unhandled exception {ex.Message}");
                }
            }
            while (!observer.ExitThread && ++retryCount < 5);
            _logger.LogWarning($"KafkaObservable.ConsumerThread:  Leaving thread - retry count := {retryCount}");
            return;

            void ProcessConsumedEvent(ConsumeResult<string, string> consumeResult)
            {
                var eventType = GetEventType(consumeResult.Message);
                if (observer.ConsumeEvents.Any(e => e.GetType() == eventType))
                {
                    var consumedEvent = JsonConvert.DeserializeObject(consumeResult.Message.Value, eventType) as IEvent;
                    if (!_duplicateEventMap.ContainsKey(consumedEvent.Id))
                    {
                        if (_duplicateEventMap.TryAdd(consumedEvent.Id, consumedEvent.GetType().Name))
                        {
                            consumedEvent
                                .SetEventSource(consumeResult.Topic)
                                .SetReceivedOn(consumeResult.Message.Timestamp.UtcDateTime);
                            observer.ConsumerAction(consumedEvent);
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
        private Type GetEventType(Message<string, string> message)
        {
            var header = message.Headers.Single(e => e.Key == "EventAssemblyQualifiedName");
            var eventTypeName = Encoding.ASCII.GetString(header.GetValueBytes());
            return Type.GetType(eventTypeName);
        }

       
    }

    /// <summary>
    /// kafka event observer 
    /// </summary>
    public class KafkaObserver : IObserver<IEvent>, IDisposable
    {
        readonly Dictionary<Guid, AutoResetEvent> _resetEventMap;

        /// <summary>
        /// kafka event observer constructor
        /// </summary>
        /// <param name="consumeEvents">events we will consume from event broker</param>
        /// <param name="consumerAction">lamda function that will execute when selected event has been received from event broker</param>
        public KafkaObserver(
            string siteName,
            ICollection<IEvent> consumeEvents,
            Action<IEvent> consumerAction)
        {
            _resetEventMap = new Dictionary<Guid, AutoResetEvent>();
            Id = Guid.NewGuid();
            SiteName = siteName;
            ConsumeEvents = consumeEvents;
            ConsumerAction = consumerAction;
            Started = false;
            ExitThread = false;
        }

        /// <summary>
        /// public properties
        /// </summary>
        public Guid Id { get; }
        public string SiteName { get; }
        public Guid WaitId { get; private set; }
        public ICollection<IEvent> ConsumeEvents { get; }
        public Action<IEvent> ConsumerAction { get; }
        public bool ExitThread { get; set; }
        public bool Started { get; set; }
        public bool Stopped => !Started;

        /// <summary>
        /// private properties
        /// </summary>
  
        /// <summary>
        /// execute consumer action on consumed event
        /// </summary>
        /// <param name="consumedEvent"></param>
        public void OnNext(IEvent consumedEvent) => ConsumerAction(consumedEvent);

        /// <summary>
        /// stop listening to event broker 
        /// </summary>
        public void OnCompleted()
        {
            Stop();
        }

        /// <summary>
        /// stop listening to event broker log error
        /// </summary>
        /// <param name="error"></param>
        public void OnError(Exception error)
        {
            Stop();
            throw error;
        }

        /// <summary>
        /// start listening for consumed events
        /// </summary>
        /// <returns>handle to wait on to notify when consumer thread has started</returns>
        public void Start()
        {
            ExitThread = false;
            Started = true;
            WaitId = Guid.NewGuid();
            _resetEventMap.Add(WaitId, new AutoResetEvent(false));
        }

        /// <summary>
        /// stop listening for consumed events
        /// </summary>
        public void Stop()
        {
            _resetEventMap.Clear();
            ExitThread = true;
            Started = false;
        }

        /// <summary>
        /// wait for consumer thread to start
        /// </summary>
        /// <param name="waitId">consumer thread wait handle</param>
        public void WaitForConsumerStart()
        {
            if (_resetEventMap.ContainsKey(WaitId))
                _resetEventMap[WaitId].WaitOne();
        }

        /// <summary>
        /// indicate to thread that is waiting for consumer thread start, that thread has started
        /// </summary>
        /// <param name="waitId">consumer thread wait handle </param>
        public void SetConsumerStart()
        {
            if (_resetEventMap.ContainsKey(WaitId))
                _resetEventMap[WaitId].Set();
        }

        /// <summary>
        /// stop observer processing
        /// </summary>
        public void Dispose() => Stop();
    }
}
