using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventQueue;

namespace TomasAI.IFM.Framework.Messaging.Kafka;

/// <summary>
/// Represents an abstract base class for consuming events from a Kafka event broker.
/// </summary>
/// <remarks>This class provides the foundational functionality for consuming events from Kafka, including
/// subscribing to events, managing consumer threads, and processing consumed events. Derived classes must implement the
/// <see cref="ConnectEvents"/> method to define the specific events to consume.</remarks>
public abstract class KafkaEventConsumer : IEventConsumer
{
    static ConcurrentDictionary<Guid, string>? _duplicateEventMap;
    readonly IEventConsumerOptions _options;
    readonly ILogger _logger;
    EventConsumerState? _consumerState;
    Thread? _consumerThread;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaEventConsumer"/> class with the specified options and logger.
    /// </summary>
    /// <param name="options">The configuration options for the event consumer. Cannot be <see langword="null"/>.</param>
    /// <param name="logger">The logger instance used for logging events and diagnostics. Cannot be <see langword="null"/>.</param>
    public KafkaEventConsumer(IEventConsumerOptions options, ILogger logger)
    {
        _options = IsArgumentNull.Set(options);
        _logger = IsArgumentNull.Set(logger);
        _duplicateEventMap ??= new ConcurrentDictionary<Guid, string>();
    }

    /// <summary>
    /// Starts the asynchronous operation to initialize the event consumer.
    /// </summary>
    /// <remarks>This method connects the necessary event handlers for the consumer.  It completes immediately
    /// and does not block the calling thread.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask StartAsync()
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
    /// Stops the asynchronous operation of the consumer.
    /// </summary>
    /// <remarks>This method halts the consumer's activity if it is currently running.  It ensures that any
    /// ongoing operations are stopped gracefully.</remarks>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async ValueTask StopAsync()
    {
        _consumerState?.Stop();
        await Task.CompletedTask;
    }

    /// <summary>
    /// call event connection method On for each event that will be consumed
    /// </summary>
    protected abstract void ConnectEvents();

    /// <summary>
    /// Subscribes to a collection of events and processes them using the specified consumer action.
    /// </summary>
    /// <remarks>This method initializes the event consumer state and starts a dedicated thread to process the
    /// specified events. The consumer action is invoked for each event in the collection.</remarks>
    /// <param name="siteName">The name of the site associated with the subscription.</param>
    /// <param name="consumeEvents">A collection of events to be consumed. Cannot be <see langword="null"/>.</param>
    /// <param name="consumerAction">The action to execute for each consumed event. Cannot be <see langword="null"/>.</param>
    protected void Subscribe(string siteName, ICollection<IEvent> consumeEvents, Action<IEvent> consumerAction)
    {
        IsArgumentNull.Check(consumeEvents);
        IsArgumentNull.Check(consumerAction);
        _consumerState = new EventConsumerState(siteName);
        var waitId = _consumerState.Start();

        _consumerThread = new Thread(_ => ConsumerThread(waitId, consumeEvents, e => consumerAction(e)));
        _consumerThread.Priority = ThreadPriority.AboveNormal;
        _consumerThread.Start();
        _consumerState.WaitForConsumerStart(waitId);
    }
   
    /// <summary>
    /// Executes a consumer thread that processes events from a Kafka topic and invokes a specified action for each
    /// consumed event.
    /// </summary>
    /// <remarks>This method initializes a Kafka consumer with the specified configuration, subscribes to the
    /// provided event types,  and continuously consumes messages from the Kafka topic until the thread is signaled to
    /// stop.   The method ensures that duplicate events are filtered out based on their unique identifiers.  If the
    /// duplicate event map reaches a size of 1000, it is cleared to manage memory usage.  Exceptions during consumption
    /// are logged, and the thread exits gracefully in case of errors or when explicitly stopped.</remarks>
    /// <param name="waitId">A unique identifier used to track the start of the consumer thread.</param>
    /// <param name="consumeEvents">A collection of event types to be consumed. Only events matching these types will be processed.</param>
    /// <param name="consumerAction">An action to be invoked for each successfully consumed event. The action receives the event as a parameter.</param>
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
            using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
                    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                    .Build();
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
        catch (ConsumeException ex)
        {
            _logger.LogErrorEvent("KafkaEventConsumer", ex, $"ConsumerThread:  consume exception {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent("KafkaEventConsumer", ex, $"ConsumerThread:  unhandled exception {ex.Message}");
            return;
        }
        _logger.LogWarning($"KafkaEventConsumer.ConsumerThread:  exiting thread");
        return;

        void ProcessConsumedEvent(ConsumeResult<string,string> consumeResult)
        {
            var eventType = GetEventType(consumeResult.Message);
            if (consumeEvents.Any(e => e.GetType() == eventType))
            {
                var consumedEvent = (JsonConvert.DeserializeObject(consumeResult.Message.Value, eventType) as IEvent)!;
                if (!_duplicateEventMap!.ContainsKey(consumedEvent.Id))
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
    static Type GetEventType(Message<string, string> message)
    {
        var header = message.Headers.Single(e => e.Key == "EventAssemblyQualifiedName");
        var eventAssemblyName = Encoding.ASCII.GetString(header.GetValueBytes());
        return Type.GetType(eventAssemblyName)!;
    }

    /// <summary>
    /// Represents the state and control mechanisms for an event consumer, including its lifecycle, synchronization, and
    /// associated event queue.
    /// </summary>
    /// <remarks>This class provides methods to manage the lifecycle of an event consumer, including starting
    /// and stopping the consumer, synchronizing its initialization using wait handles, and associating it with a
    /// concurrent event queue. It is designed to facilitate thread-safe operations and coordination between producer
    /// and consumer threads.</remarks>
    /// <param name="siteName"></param>
    class EventConsumerState(string siteName)
    {
        readonly Dictionary<Guid, AutoResetEvent> _resetEventMap = [];
        bool _exitThread = false;
        bool _started;
        ConcurrentEventQueue<ConsumeResult<string, string>>? _consumerEventQueue;

        public Guid Id { get; } = Guid.NewGuid();
        public string SiteName { get; } = siteName;
        public bool ExitThread => _exitThread;
        public bool Started => _started;
        public bool Stopped => !_started;
        public ConcurrentEventQueue<ConsumeResult<string, string>> ConsumerEventQueue => _consumerEventQueue!;

        /// <summary>
        /// Starts the process and generates a unique identifier for synchronization.
        /// </summary>
        /// <remarks>The returned <see cref="Guid"/> is added to an internal map and associated with a
        /// synchronization event. Ensure that the identifier is used appropriately to manage synchronization.</remarks>
        /// <returns>A <see cref="Guid"/> that serves as a unique identifier for the operation. This identifier can be used to
        /// synchronize with the process.</returns>
        public Guid Start() 
        {
            _exitThread = false;
            _started = true;
            var waitId = Guid.NewGuid();
            _resetEventMap.Add(waitId, new AutoResetEvent(false));
            return waitId;
        }

        /// <summary>
        /// Stops the current operation and resets the internal state.
        /// </summary>
        /// <remarks>This method clears all registered reset events, sets the internal state to indicate
        /// that the operation  should exit, and marks the operation as no longer started. Once called, the operation
        /// cannot be restarted  without reinitialization.</remarks>
        public void Stop()
        {
            _resetEventMap.Clear();
            _exitThread = true;
            _started = false;
        }

        /// <summary>
        /// Waits for the consumer associated with the specified identifier to start.
        /// </summary>
        /// <remarks>This method blocks the calling thread until the consumer associated with the
        /// specified  <paramref name="waitId"/> signals that it has started. If the identifier does not exist  in the
        /// internal map, the method does nothing.</remarks>
        /// <param name="waitId">The unique identifier of the consumer to wait for.</param>
        public void WaitForConsumerStart(Guid waitId)
        {
            if (_resetEventMap.ContainsKey(waitId))
                _resetEventMap[waitId].WaitOne();
        }

        /// <summary>
        /// Signals the start of a consumer operation associated with the specified wait identifier.
        /// </summary>
        /// <remarks>If the specified <paramref name="waitId"/> exists in the internal map, the
        /// corresponding event is set. This method has no effect if the <paramref name="waitId"/> is not
        /// found.</remarks>
        /// <param name="waitId">The unique identifier for the consumer operation to signal.</param>
        public void SetConsumerStart(Guid waitId) 
        {
            if (_resetEventMap.ContainsKey(waitId))
                _resetEventMap[waitId].Set();
        }

        /// <summary>
        /// Sets the event queue used to handle consumer events.
        /// </summary>
        /// <param name="consumerEventQueue">The event queue that will receive <see cref="ConsumeResult{TKey, TValue}"/> instances representing messages
        /// consumed from the Kafka topic. Cannot be null.</param>
        public void SetConsumerEventQueue(ConcurrentEventQueue<ConsumeResult<string, string>> consumerEventQueue) 
            => _consumerEventQueue = consumerEventQueue;

    }
}
