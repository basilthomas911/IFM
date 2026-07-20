using NATS.Client.Core;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a thread-based scheduler for processing actor messages in a dedicated thread.
/// </summary>
/// <remarks>This class manages the lifecycle of a thread dedicated to processing messages from an unbounded
/// channel. It ensures that messages are processed sequentially using a single reader, while allowing multiple writers.
/// The scheduler supports starting, stopping, and writing messages to the channel.</remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="ActorThreadScheduler"/> class, which schedules and processes actor
/// messages using a thread pool.
/// </remarks>
/// <param name="messageReader">A delegate that processes actor messages. Cannot be <see langword="null"/>.</param>
/// <param name="logger">An optional logger instance for logging diagnostic information. Can be <see langword="null"/>.</param>
public class ActorThreadScheduler(Func<NatsMsg<byte[]>, ValueTask> messageReader, ILogger logger)
        : IActorThreadScheduler
{
    readonly Func<NatsMsg<byte[]>, ValueTask> _messageReader = IsArgumentNull.Set(messageReader);
    readonly CancellationTokenSource _cancellationTokenSource = new();
    readonly ILogger _logger = logger;
    readonly string _serviceId = "ActorThreadScheduler";
    string _channelName = string.Empty;
    Thread? _messageChannelThread;
    IActorThreadQueue? _threadQueue;

    /// <summary>
    /// Gets a value indicating whether the message channel is currently running.
    /// </summary>
    public bool IsRunning
        => _threadQueue is not null;

    /// <summary>
    /// Starts the thread scheduler, initializing the message channel and processing thread if it is not already
    /// running.
    /// </summary>
    /// <remarks>This method initializes an unbounded message channel and starts a background thread with the
    /// highest priority  to process messages from the channel. If the scheduler is already running, this method does
    /// nothing.</remarks>
    public void Start(IActorThreadQueue threadQueue)
    {
        IsArgumentNull.Check(threadQueue);
        try
        {
            if (!IsRunning)
            {
                _threadQueue = threadQueue;
                _threadQueue.Start();
                _channelName = $"{threadQueue.Id}";
                /*
                _messageChannelThread = threadQueue.Id.ActorType == ActorType.Query
                    ? new Thread(ProcessQueryMessagesFromMessageChannel) { Priority = ThreadPriority.Highest, IsBackground = true }
                    : new Thread(ProcessMessagesFromMessageChannel) { Priority = ThreadPriority.Highest, IsBackground = true };
                */
                _messageChannelThread = new Thread(ProcessMessagesFromMessageChannel) { Priority = ThreadPriority.Highest, IsBackground = true };
                _messageChannelThread.Start();
                _logger?.LogInformationEvent(_serviceId, "{ChannelName} - started successfully.", _channelName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(_serviceId, ex, "{ChannelName} - failed to start.", _channelName!);
        }
    }

    /// <summary>
    /// Stops the thread scheduler and releases associated resources.
    /// </summary>
    /// <remarks>This method cancels any ongoing operations, completes the message channel, and disposes of
    /// internal resources.  After calling this method, the thread scheduler cannot be restarted, and any pending
    /// messages in the channel will be discarded.</remarks>
    public void Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _threadQueue!.Stop();
        }
        catch { }
        finally
        {
            _cancellationTokenSource.Dispose();
            _messageChannelThread = null;
            _threadQueue = null;
        }
        _logger?.LogInformationEvent(_serviceId, "{ChannelName} - stopped successfully.", _channelName);
    }

    /// <summary>
    /// Attempts to write the specified message to the internal thread queue.
    /// </summary>
    /// <remarks>If the internal thread queue is unavailable, the method returns false and the message is not
    /// written. This method does not throw exceptions for queue unavailability.</remarks>
    /// <param name="message">The message to be written. Contains the payload and associated metadata to be enqueued.</param>
    /// <returns>true if the message was successfully written to the thread queue; otherwise, false.</returns>
    public bool WriteData(in NatsMsg<byte[]> message)
        => _threadQueue is not null
                ? _threadQueue.Write(message)
                : false;

    /// <summary>
    /// Processes messages from the message channel using a single-threaded task scheduler.
    /// </summary>
    /// <remarks>This method reads messages from the message channel and processes them asynchronously using
    /// the provided message reader.  It ensures that message processing occurs on a single thread and handles any
    /// exceptions that occur during processing. If the message channel is empty or the processing is stopped, the
    /// method exits gracefully.</remarks>
    void ProcessMessagesFromMessageChannel()
    {
        try
        {
            var messageCount = 0;
            _logger?.LogInformationEvent(_serviceId, "{channelName} - message processing started up.", _channelName!);
            //SingleThreadTaskScheduler.Run(async () =>
            Task.Run(async () =>
            {
                /// wait for messages to be available...
                await foreach (var message in _threadQueue!.ReadAllAsync(_cancellationTokenSource.Token))
                {
                    try
                    {
                        await _messageReader(message);
                        messageCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogErrorEvent(_serviceId, ex, "{channelName} - error processing message in event channel.", _channelName!);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(_serviceId, ex, "{ChannelName} - error in event channel SingleThreadTaskScheduler loop.", _channelName!);
        }
    }

    void ProcessQueryMessagesFromMessageChannel()
    {
        try
        {
            var messageCount = 0;
            _logger?.LogInformationEvent(_serviceId, "{channelName} - query message processing started up.", _channelName!);
            //SingleThreadTaskScheduler.Run(async () =>
            Task.Run(async () =>
            {
                try
                {
                    /// wait for messages to be available...
                    foreach (var message in _threadQueue!.ReadAll(_cancellationTokenSource.Token))
                    {
                        try
                        {
                            await _messageReader(message);
                            messageCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogErrorEvent(_serviceId, ex, "{channelName} - error processing query message in event channel.", _channelName!);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                   _logger?.LogInformationEvent(_serviceId, "{channelName} - query message processing canceled.", _channelName!);   
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
                this.Stop();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(_serviceId, ex, "{ChannelName} - error in event channel SingleThreadTaskScheduler loop.", _channelName!);
        }
    }
}

