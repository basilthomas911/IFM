using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.Kafka
{
    /// <summary>
    /// kafka event producer
    /// </summary>
    public abstract class KafkaEventProducer : IEventProducer
    {
        private static Dictionary<string, Type> _eventPrimerMap;
        private static SemaphoreSlim _asyncLock;
        private readonly string _eventProducerName;
        private readonly IEventProducerOptions _options;
        private readonly ILogger _logger;
        private readonly IProducer<string,string> _producer;
        private readonly Dictionary<string, string> _startupMap;

        public KafkaEventProducer()
        {
        }

        /// <summary>
        /// kafka event producer constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public KafkaEventProducer(IEventProducerOptions options, ILogger logger)
        {
            _options = options;
            _logger = logger;
            _eventProducerName = this.GetType().Name;
            var config = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                QueueBufferingBackpressureThreshold = 1, 
                EnableIdempotence = true,
                LingerMs = 0
            };
            _producer = new ProducerBuilder<string, string>(config).Build();
            _startupMap = new Dictionary<string, string>();
            _eventPrimerMap = _eventPrimerMap ?? new Dictionary<string, Type>();
            _asyncLock = _asyncLock ?? new SemaphoreSlim(1, 1);
            _logger.LogInformationEvent(_eventProducerName,"successfully initialized");
        }

        /// <summary>
        /// derived class must implement this in order for external classes to send events to kafka event broker
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        public abstract Task PostEventAsync(IEvent @event);

        /// <summary>
        /// send event to kafka broker and wait to make dure event was delivered successfully
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="eventKey"></param>
        /// <param name="eventValue"></param>
        /// <returns></returns>
        protected async Task SendEventAsync<TKey, TValue>(TKey eventKey, TValue eventValue) where TValue:IEvent
        {
            try
            {
                // Note: Awaiting the asynchronous produce request below prevents flow of execution
                // from proceeding until the acknowledgement from the broker is received (at the 
                // expense of low throughput).
                if (eventValue == null)
                    throw new ArgumentNullException(nameof(eventValue));
                if (string.IsNullOrWhiteSpace(eventValue.EventSource))
                    throw new InvalidOperationException($"SendEventAsync: {eventValue.GetType().Name}.EventSource is empty");
                eventValue.CheckForEmptyCommandId();

                var topicName = eventValue.GetType().Name; // eventValue.EventSource; 
                var value = JsonConvert.SerializeObject(eventValue, Formatting.None);
                var message = new Message<string, string> { Key = $"{eventKey}", Value = value };
                message.Headers = new Headers();
                message.Headers.Add("EventAssemblyQualifiedName", Encoding.ASCII.GetBytes(eventValue.GetType().AssemblyQualifiedName));
                var deliveryReport = await _producer.ProduceAsync(topicName, message);
                if (deliveryReport.Status != PersistenceStatus.Persisted)
                    _logger.LogInformationEvent(_eventProducerName, $"{topicName} not persisted");
                if (!_eventPrimerMap.ContainsKey(topicName))
                {
                    await _asyncLock.WaitAsync();
                    try
                    {
                        if (!_eventPrimerMap.ContainsKey(topicName))
                        {
                            _eventPrimerMap.Add(topicName, eventValue.GetType());
                            _logger.LogInformationEvent(_eventProducerName, $"{topicName} primer mapped");
                            await _producer.ProduceAsync(topicName, message);
                        }
                    }
                    finally
                    {
                        _asyncLock.Release();
                    }
                }

            }
            catch (ProduceException<string, string> ex)
            {
                _logger?.LogErrorEvent(_eventProducerName, ex, $"SendEventAsync: failed to send event [{ex.Error.Code}]");
            }
            catch (Exception ex)
            {
                _logger?.LogErrorEvent(_eventProducerName, ex, $"SendEventAsync: failed to send event");
            }
        }

        /// <summary>
        /// stream event to broke without waiting for response whether event was successfully delivered 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="eventKey"></param>
        /// <param name="eventValue"></param>
        /// <returns></returns>
        protected async Task StreamEventAsync<TKey, TValue>(TKey eventKey, TValue eventValue) where TValue : IEvent
        {
            try
            {
                if (eventValue == null)
                    throw new ArgumentNullException(nameof(eventValue));
                if (string.IsNullOrWhiteSpace(eventValue.EventSource))
                    throw new InvalidOperationException($"StreamEventAsync: {eventValue.GetType().Name}.EventSource is empty");
                eventValue.CheckForEmptyCommandId();
                var topicName = eventValue.GetType().Name;  
                var value = JsonConvert.SerializeObject(eventValue, Formatting.None);
                var message = new Message<string, string> { Key = $"{eventKey}", Value = value };
                message.Headers = new Headers();
                message.Headers.Add("EventAssemblyQualifiedName", Encoding.ASCII.GetBytes(eventValue.GetType().AssemblyQualifiedName));
                _producer.Produce(topicName, message);
                await Task.CompletedTask;
            }
            catch (ProduceException<string, string> ex)
            {
                var eventName = eventValue.EventName ?? "Unknown";
                _logger?.LogErrorEvent(_eventProducerName, ex, $"StreamEventAsync: failed to stream event {eventName} [{ex.Error.Code}]");
            }
            catch (Exception ex)
            {
                var eventName = eventValue.EventName ?? "Unknown";
                _logger?.LogErrorEvent(_eventProducerName, ex, $"StreamEventAsync: failed to stream event {eventName}");
            }
        }
        
    }
}
