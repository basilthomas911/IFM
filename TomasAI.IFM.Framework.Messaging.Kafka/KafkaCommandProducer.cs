using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.Kafka;

/// <summary>
/// kafka event producer
/// </summary>
public abstract class KafkaCommandProducer 
{
    static Dictionary<string, Type> _cmdPrimerMap;
    static SemaphoreSlim _asyncLock;
    readonly string _cmdProducerName;
    readonly IEventProducerOptions _options;
    readonly ILogger _logger;
    readonly IProducer<string,string> _kafkaProducer;

    public KafkaCommandProducer()
    {
    }

    /// <summary>
    /// kafka event producer constructor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public KafkaCommandProducer(IEventProducerOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _cmdProducerName = this.GetType().Name;
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            QueueBufferingBackpressureThreshold = 1, 
            EnableIdempotence = true,
            LingerMs = 0
        };
        _kafkaProducer = new ProducerBuilder<string, string>(config).Build();
        _cmdPrimerMap = _cmdPrimerMap ?? new Dictionary<string, Type>();
        _asyncLock = _asyncLock ?? new SemaphoreSlim(1, 1);
        _logger.LogInformationEvent(_cmdProducerName, "initialized successfully");
    }

    /// <summary>
    /// send event to kafka broker and wait to make dure event was delivered successfully
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cmdKey"></param>
    /// <param name="cmdValue"></param>
    /// <returns></returns>
    protected async Task SendCommandAsync<TKey, TValue>(TKey cmdKey, TValue cmdValue) where TValue:ICommand
    {
        try
        {
            // Note: Awaiting the asynchronous produce request below prevents flow of execution
            // from proceeding until the acknowledgement from the broker is received (at the 
            // expense of low throughput).
            if (cmdValue == null)
                throw new ArgumentNullException(nameof(cmdValue));

            var topicName = cmdValue.GetType().Name; 
            var value = JsonConvert.SerializeObject(cmdValue, Formatting.None);
            var message = new Message<string, string>
            {
                Key = $"{cmdKey}",
                Value = value,
                Headers = new Headers
                {
                    { "CommandAssemblyQualifiedName", Encoding.ASCII.GetBytes(cmdValue.GetType().AssemblyQualifiedName!) }
                }
            };
            var deliveryReport = await _kafkaProducer.ProduceAsync(topicName, message);
            if (deliveryReport.Status != PersistenceStatus.Persisted)
                _logger.LogInformationEvent(_cmdProducerName, $"{topicName} not persisted");
            if (!_cmdPrimerMap.ContainsKey(topicName))
            {
                await _asyncLock.WaitAsync();
                try
                {
                    if (!_cmdPrimerMap.ContainsKey(topicName))
                    {
                        _cmdPrimerMap.Add(topicName, cmdValue.GetType());
                        _logger.LogInformationEvent(_cmdProducerName, $"{topicName} primer mapped");
                        await _kafkaProducer.ProduceAsync(topicName, message);
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
            _logger?.LogErrorEvent(_cmdProducerName, ex, $"SendCommandAsync: failed to send command [{ex.Error.Code}]");
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(_cmdProducerName, ex, $"SendCommandAsync: failed to send command");
        }
    }

    /// <summary>
    /// stream command to broke without waiting for response whether command was successfully delivered 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="cmdKey"></param>
    /// <param name="cmdValue"></param>
    /// <returns></returns>
    protected async Task StreamCommandAsync<TKey, TValue>(TKey cmdKey, TValue cmdValue) where TValue : ICommand
    {
        try
        {
            if (cmdValue == null)
                throw new ArgumentNullException(nameof(cmdValue));

            var topicName = cmdValue.GetType().Name;  
            var value = JsonConvert.SerializeObject(cmdValue, Formatting.None);
            var message = new Message<string, string> { Key = $"{cmdKey}", Value = value };
            message.Headers = new Headers();
            message.Headers.Add("CommandAssemblyQualifiedName", Encoding.ASCII.GetBytes(cmdValue.GetType().AssemblyQualifiedName));
            _kafkaProducer.Produce(topicName, message);
            await Task.CompletedTask;
        }
        catch (ProduceException<string, string> ex)
        {
            var cmdName = cmdValue.GetType().Name;
            _logger?.LogErrorEvent(_cmdProducerName, ex, $"StreamEventAsync: failed to stream command {cmdName} [{ex.Error.Code}]");
        }
        catch (Exception ex)
        {
            var cmdName = cmdValue.GetType().Name;
            _logger?.LogErrorEvent(_cmdProducerName, ex, $"StreamEventAsync: failed to stream command {cmdName}");
        }
    }
    
    
}
