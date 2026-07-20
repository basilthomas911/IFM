using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
///  event channel logger type
/// </summary>
public class EventChannel { }

/// <summary>
/// Represents a concurrent asynchronous event channel for processing data of type <typeparamref name="TData"/>.
/// </summary>
/// <remarks>This class provides functionality to initialize, start, and stop an event channel that processes data
/// asynchronously. It supports multiple writers and a single reader, ensuring that data is processed in a thread-safe
/// manner. The channel can be used to handle high-throughput scenarios where data needs to be processed concurrently.
/// Logging is supported to track the channel's operations and any errors that occur during processing.</remarks>
/// <typeparam name="TData">The type of data to be processed by the event channel.</typeparam>
public class ConcurrentAsyncEventChannel<TData>
{
    readonly string _channelName;
    readonly Func<TData, ValueTask> _eventChannelAsyncMessageReader;
    readonly CancellationTokenSource _cancellationTokenSource = new();
    readonly ILogger<EventChannel>? _logger;
    Channel<TData>? _eventChannel;
    Thread? _eventChannelThread;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentAsyncEventChannel{TData}"/> class with the specified
    /// channel name, event reader, and logger.
    /// </summary>
    /// <remarks>The constructor sets up the event channel with the provided name and reader function. If a
    /// logger is provided, an informational log entry is created upon initialization.</remarks>
    /// <param name="channelName">The name of the event channel. Cannot be null.</param>
    /// <param name="eventChannelAsyncReader">A function that processes events asynchronously. Cannot be null.</param>
    /// <param name="logger">An optional logger for logging events related to the event channel.</param>
    public ConcurrentAsyncEventChannel(string channelName, Func<TData, ValueTask> eventChannelAsyncReader, ILogger<EventChannel>? logger)
    {
        _channelName = IsArgumentNull.Set(channelName);
        _eventChannelAsyncMessageReader = IsArgumentNull.Set(eventChannelAsyncReader);
        _logger = logger;
        _logger?.LogInformationEvent("EventChannel - {ChannelName}", "ConcurrentAsyncEventChannel initialized.", _channelName);
    }

    /// <summary>
    /// Gets a value indicating whether the event channel is currently open.
    /// </summary>
    public bool IsOpen
        => _eventChannel is not null;

    /// <summary>
    /// Initializes and starts the concurrent event channel for processing data asynchronously.
    /// </summary>
    /// <remarks>This method sets up an unbounded channel for data of type <typeparamref name="TData"/> and 
    /// starts a background thread with the highest priority to process messages asynchronously.  The channel is
    /// configured to support multiple writers and a single reader.  If the channel starts successfully, an
    /// informational log entry is created.  In case of failure, an error log entry is recorded.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentAsyncEventChannel{TData}"/> to allow method chaining.</returns>
    public ConcurrentAsyncEventChannel<TData> Start()
    {
        try
        {
            _eventChannel = Channel.CreateUnbounded<TData>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
            _eventChannelThread = new Thread(ProcessMessagesFromEventChannel) { Priority = ThreadPriority.Highest, IsBackground = true };
            _eventChannelThread.Start();
            _logger?.LogInformationEvent("EventChannel - {ChannelName}", "ConcurrentAsyncEventChannel started successfully.", _channelName);
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent("EventChannel - {ChannelName}", ex, "Failed to start ConcurrentAsyncEventChannel.", _channelName);
        }
        return this;
    }

    /// <summary>
    /// Stops the event channel, cancels any ongoing operations, and releases associated resources.
    /// </summary>
    /// <remarks>This method cancels the internal operations of the event channel, completes the writer,  and
    /// disposes of the cancellation token source. After calling this method, the event channel  is no longer usable. A
    /// log entry is created to indicate the successful stop operation.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentAsyncEventChannel{TData}"/>, allowing for method chaining.</returns>
    public ConcurrentAsyncEventChannel<TData> Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _eventChannel?.Writer.Complete();
        }
        catch { }
        finally
        {
            _cancellationTokenSource.Dispose();
            _eventChannelThread = null;
            _eventChannel = null;
        }
        _logger?.LogInformationEvent("EventChannel - {ChannelName}", "ConcurrentAsyncEventChannel stopped successfully.", _channelName);
        return this;
    }

    /// <summary>
    /// Attempts to write the specified data to the event channel.
    /// </summary>
    /// <remarks>This method returns <see langword="false"/> if the event channel is not available or  if the
    /// write operation fails.</remarks>
    /// <param name="jobData">The data to be written to the event channel.</param>
    /// <returns><see langword="true"/> if the data was successfully written to the event channel;  otherwise, <see
    /// langword="false"/>. </returns>
    public bool WriteData(TData jobData)
        => _eventChannel?.Writer.TryWrite(jobData) ?? false;

    /// <summary>
    /// Processes messages from the event channel in a single-threaded task scheduler.
    /// </summary>
    /// <remarks>This method continuously reads messages from the event channel and processes them
    /// asynchronously  using the provided message reader delegate. It ensures that message processing occurs in a 
    /// single-threaded context. If an exception occurs during message processing, it is logged, and  processing
    /// continues with the next message. The method stops processing when the event channel  becomes unavailable or the
    /// cancellation token is triggered.</remarks>
    void ProcessMessagesFromEventChannel()
    {
        try
        {
            SingleThreadTaskScheduler.Run(async () =>
            {
                _logger?.LogInformationEvent("EventChannel - {ChannelName}", "Message processing started....", _channelName);
                while (_eventChannel is not null && await _eventChannel!.Reader.WaitToReadAsync(_cancellationTokenSource.Token))
                {
                    while (_eventChannel is not null && _eventChannel.Reader.TryRead(out var message))
                    {
                        try
                        {
                            await _eventChannelAsyncMessageReader(message!);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogErrorEvent("EventChannel - {ChannelName}", ex, "Error processing message in event channel.", _channelName);
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent("EventChannel - {ChannelName}", ex, "Error in event channel SingleThreadTaskScheduler loop.", _channelName);
        }
        finally
        {
            _logger?.LogInformationEvent("EventChannel - {ChannelName}", "Message processing stopped.", _channelName);
        }
    }
}
