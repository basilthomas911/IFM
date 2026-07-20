using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.Kafka;

/// <summary>
/// Provides an abstract base class for consuming events asynchronously from a Kafka stream.
/// </summary>
/// <remarks>The <see cref="KafkaAsyncEventConsumer"/> class is designed to facilitate the consumption of events
/// from a Kafka stream in an asynchronous manner. It provides methods to start and stop the consumer, subscribe to
/// events, and manage the consumer's lifecycle. Derived classes must implement the <see cref="ConnectEvents"/> method
/// to establish event connections specific to their needs.</remarks>
/// <param name="options"></param>
/// <param name="jsonSerializer"></param>
/// <param name="logger"></param>
public abstract class KafkaAsyncEventConsumer(IEventConsumerOptions options, IJsonSerializer jsonSerializer, ILogger<EventChannel> logger)
    : IEventConsumer
{
    static readonly ConcurrentDictionary<Guid, string> _duplicateEventMap = [];
    readonly IEventConsumerOptions _options = IsArgumentNull.Set(options);
    readonly IJsonSerializer _jsonSerializer = IsArgumentNull.Set(jsonSerializer);
    readonly ILogger<EventChannel> _logger = IsArgumentNull.Set(logger);
    EventConsumerState? _consumerState;

    /// <summary>
    /// Gets the name of the consumer, which is the name of the current type.
    /// </summary>
    string ConsumerName => GetType().Name;

    /// <summary>
    /// Initiates the asynchronous start process for the consumer.
    /// </summary>
    /// <remarks>This method connects necessary events for the consumer. It logs any exceptions that occur
    /// during the start process.</remarks>
    /// <returns></returns>
    public ValueTask StartAsync()
    {
        try
        {
            ConnectEvents();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"{ConsumerName}.StartAsync:  {ex.GetErrorMessage()}");
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously stops the consumer operation.
    /// </summary>
    /// <remarks>This method attempts to stop the consumer operation and logs any exceptions that occur during
    /// the process.</remarks>
    /// <returns></returns>
    public ValueTask StopAsync()
    {
        try
        {
            _consumerState?.Stop();
        }
         catch (Exception ex)
        {
            _logger.LogError(ex, $"{ConsumerName}.StopAsync:  {ex.GetErrorMessage()}");
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// clear all items from consumer event queue
    /// </summary>
    /// <returns></returns>
    public Task ClearConsumerEventQueueAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Establishes connections to the necessary events for the derived class.
    /// </summary>
    /// <remarks>This method must be implemented by derived classes to connect event handlers to the
    /// appropriate events. It is called during the initialization process.</remarks>
    protected abstract void ConnectEvents();

    /// <summary>
    /// Subscribes to a collection of events and processes them using the specified consumer action.
    /// </summary>
    /// <remarks>This method initializes the consumer state and starts a background task to process the events
    /// asynchronously.</remarks>
    /// <param name="groupId">The identifier for the group of events to be consumed.</param>
    /// <param name="consumeEvents">A collection of events to be consumed. Cannot be <see langword="null"/>.</param>
    /// <param name="consumerAction">A function that defines the action to be performed on each event. Cannot be <see langword="null"/>.</param>
    protected void Subscribe(string groupId, ICollection<IEvent> consumeEvents, Func<IEvent, ValueTask> consumerAction)
    {
        IsArgumentNull.Check(consumeEvents);
        IsArgumentNull.Check(consumerAction);

        _consumerState = new EventConsumerState(groupId, new CancellationTokenSource(), _logger);
        var waitId = _consumerState.Start(consumerAction);
        _ = Task.Run( async() => await ConsumerThread(waitId, consumeEvents));
        _consumerState.WaitForConsumerStart(waitId);
    }

    /// <summary>
    /// Creates a consumer thread to process events we have subscribed to.
    /// </summary>
    /// <param name="waitId"></param>
    /// <param name="consumeEvents"></param>
    ValueTask ConsumerThread(
        Guid waitId,
        ICollection<IEvent> consumeEvents)
    {
        var consumerConfig = new ConsumerConfig
        {
            // the group.id property must be specified when creating a consumer, even 
            // if you do not intend to use any consumer group functionality.
            GroupId = _consumerState?.GroupId,
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
            _consumerState?.StartEventConsumer(ConsumerName, ProcessConsumedEventAsync, waitId);
            while (!_consumerState!.Stopped)
            {
                var consumeResult = eventBroker.Consume(_consumerState.CancellationToken);
                if (consumeResult is not null)
                    _consumerState.ConsumerEventChannel.WriteData(consumeResult);
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
        return new ValueTask();

        /// <summary>
        /// Processes a consumed event asynchronously.
        /// </summary>
        async ValueTask ProcessConsumedEventAsync(ConsumeResult<string,string> consumeResult)
        {
            var eventType = GetEventType(consumeResult.Message);
            if (consumeEvents.Any(e => e.GetType().Name == eventType.Name))
            {
                var consumedEvent = (_jsonSerializer.Deserialize(consumeResult.Message.Value, eventType) as IEvent)!;
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
    static Type GetEventType(Message<string, string> message)
    {
        var header = message.Headers.Single(e => e.Key == "EventAssemblyQualifiedName");
        var eventTypeName = Encoding.ASCII.GetString(header.GetValueBytes());
        return Type.GetType(eventTypeName)!;
    }

    /// <summary>
    /// Represents the state of an event consumer, managing its lifecycle and associated resources.
    /// </summary>
    /// <remarks>This class is responsible for starting and stopping the event consumer, managing the
    /// cancellation token, and handling the event processing channel. It provides mechanisms to wait for the consumer
    /// to start and to set up the consumer's event processing logic.</remarks>
    /// <param name="groupId"></param>
    /// <param name="cts"></param>
    /// <param name="logger"></param>
    class EventConsumerState(string groupId, CancellationTokenSource cts, ILogger<EventChannel> logger)
    {
        readonly Dictionary<Guid, AutoResetEvent> _resetEventMap = [];
        readonly ILogger<EventChannel> _logger = IsArgumentNull.Set(logger);
        bool _started = false;
        CancellationTokenSource _cts = cts;
        CancellationToken _cancellationToken = cts.Token;
        ConcurrentAsyncEventChannel<ConsumeResult<string, string>>? _consumerEventChannel;
        Func<IEvent, ValueTask>? _consumerAction;

        public Guid Id { get; } = Guid.NewGuid();
        public string GroupId { get; } = groupId;
        public bool Started => _started;
        public bool Stopped => !_started;
        public ConcurrentAsyncEventChannel<ConsumeResult<string, string>> ConsumerEventChannel => _consumerEventChannel!;
        public Func<IEvent, ValueTask> ConsumerAction => _consumerAction!;
        public CancellationToken CancellationToken => _cancellationToken;

        /// <summary>
        /// Initiates the event processing with the specified consumer action.
        /// </summary>
        /// <remarks>The returned <see cref="Guid"/> can be used to manage or track the event processing
        /// session.  Ensure that the <paramref name="consumerAction"/> is capable of handling events in an asynchronous
        /// manner.</remarks>
        /// <param name="consumerAction">A function that processes each event asynchronously. This function is invoked for each event received.</param>
        /// <returns>A <see cref="Guid"/> that uniquely identifies the started event processing session.</returns>
        public Guid Start(Func<IEvent, ValueTask> consumerAction) 
        {
            var waitId = Guid.NewGuid();
            _resetEventMap.Add(waitId, new AutoResetEvent(false));
            _consumerAction = consumerAction;
            _started = true;
            return waitId;
        }

        /// <summary>
        /// Stops the consumer and cancels any ongoing operations.
        /// </summary>
        /// <remarks>This method cancels the current operation if the consumer has been started.  It
        /// clears the reset event map and sets the consumer to an unstarted state.</remarks>
        public void Stop()
        {
            if (_started)
            {
                _cts.Cancel();
                _consumerEventChannel?.Stop();
                _resetEventMap.Clear();
                _started = false;
                _consumerAction = null;
            }
        }

        /// <summary>
        /// Waits for the consumer associated with the specified identifier to start processing.
        /// </summary>
        /// <remarks>This method blocks the calling thread until the consumer associated with the given
        /// <paramref name="waitId"/> has started. Ensure that the <paramref name="waitId"/> corresponds to a valid
        /// consumer in the system.</remarks>
        /// <param name="waitId">The unique identifier for the consumer whose start is being awaited.</param>
        public void WaitForConsumerStart(Guid waitId)
        {
            if (_resetEventMap.ContainsKey(waitId))
                _resetEventMap[waitId].WaitOne();
        }

        /// <summary>
        /// Initializes and starts a consumer event channel for processing consumed events.
        /// </summary>
        /// <remarks>This method sets up a new consumer event channel with the specified consumer name and
        /// processing function. If the <paramref name="waitId"/> is present in the reset event map, the corresponding
        /// event is set to signal that the consumer has been successfully started.</remarks>
        /// <param name="consumerName">The name of the consumer to be used for identifying the event channel.</param>
        /// <param name="processConsumedEvent">A function that processes each consumed event asynchronously.</param>
        /// <param name="waitId">A unique identifier used to signal the completion of the consumer setup.</param>
        public void StartEventConsumer(string consumerName, Func<ConsumeResult<string, string>, ValueTask> processConsumedEvent, Guid waitId)
        {
            _consumerEventChannel = new ConcurrentAsyncEventChannel<ConsumeResult<string, string>>(consumerName, processConsumedEvent, _logger);
            _consumerEventChannel.Start();
            if (_resetEventMap.ContainsKey(waitId))
                _resetEventMap[waitId].Set();
        }

    }
}
