using System.Collections.Concurrent;

namespace TomasAI.IFM.Shared.EventQueue;

/// <summary>
/// Provides a thread-safe asynchronous event queue for processing items of type <typeparamref name="TData"/>.
/// </summary>
/// <remarks>This class allows enqueuing items to be processed asynchronously by a user-provided delegate.  The
/// processing occurs in a dedicated thread, ensuring that items are handled one at a time in the order they are
/// enqueued.</remarks>
/// <typeparam name="TData">The type of data to be processed by the event queue.</typeparam>
/// <param name="eventQueueReader"></param>
/// <param name="delayAfterReadInMs"></param>
public class ConcurrentAsyncEventQueue<TData>(Func<TData, Task> eventQueueReader, double delayAfterReadInMs = 0)
{
    ConcurrentQueue<TData>? _eventQueue;
    Func<TData,Task> _eventQueueReader = eventQueueReader;
    AutoResetEvent? _eventQueueWaitEvent;
    AutoResetEvent? _eventQueueRunEvent;
    Thread? _eventQueueThread;
    readonly CancellationTokenSource _cancellationTokenSource = new();
    bool _isStarted = false;
    double _delayAfterReadInMs = delayAfterReadInMs;

    public bool IsStarted => _isStarted;
    public bool IsEmpty => _eventQueue == null || _eventQueue.IsEmpty;

    /// <summary>
    /// Starts the event queue, initializing its internal state and processing thread.
    /// </summary>
    /// <remarks>This method initializes the event queue and begins processing events asynchronously  on a
    /// dedicated thread. The queue will process events until it is stopped or the  associated cancellation token is
    /// triggered.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentAsyncEventQueue{TData}"/>, allowing for method chaining.</returns>
    public ConcurrentAsyncEventQueue<TData> Start()
    {
        try
        {
            _isStarted = true;
            _eventQueue = new ConcurrentQueue<TData>();
            _eventQueueWaitEvent = new (false);
            _eventQueueRunEvent = new(false);
            _eventQueueThread = new Thread(_ =>
            {
                ReadEventQueue(_cancellationTokenSource.Token);
            })
            {
                Priority = ThreadPriority.AboveNormal
            };
            _eventQueueThread.Start();
        }
        catch { }
        return this;
    }

    /// <summary>
    /// Stops the event queue, cancels any ongoing operations, and releases associated resources.
    /// </summary>
    /// <remarks>This method halts the processing of the event queue, cancels any pending tasks, and disposes
    /// of  internal resources such as synchronization events and cancellation tokens. After calling this method,  the
    /// queue is no longer operational and must not be used until restarted.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentAsyncEventQueue{TData}"/>, allowing for method chaining.</returns>
    public ConcurrentAsyncEventQueue<TData> Stop()
    {
        try
        {
            _isStarted = false;
            _cancellationTokenSource.Cancel();
            _eventQueueWaitEvent?.Set();
            _eventQueueWaitEvent?.Close();
            _eventQueueWaitEvent?.Dispose();
            _eventQueueRunEvent?.Close();
            _eventQueueRunEvent?.Dispose();
        }
        catch { }
        finally
        {
            _eventQueueThread = null!;
            _eventQueue = null!;
            _eventQueueWaitEvent = null!;
            _eventQueueRunEvent = null!;
            _cancellationTokenSource.Dispose();
        }
        return this;
    }

    /// <summary>
    /// Adds an item to the event queue and signals the waiting event to notify that an item is available.
    /// </summary>
    /// <remarks>This method enqueues the specified item into the event queue and signals the associated wait
    /// event, allowing any waiting threads to proceed. Ensure that the event queue and wait event are properly
    /// initialized before calling this method.</remarks>
    /// <param name="queueItem">The item to add to the event queue. Cannot be null.</param>
    public void EnqueueAndSignal(TData queueItem)
    {
        _eventQueue?.Enqueue(queueItem);
        _eventQueueWaitEvent?.Set();
    }

    /// <summary>
    /// Removes all items from the event queue.
    /// </summary>
    /// <remarks>This method clears the event queue by dequeuing all items until the queue is empty. If the
    /// queue is already empty, the method has no effect.</remarks>
    public void Clear()
    {
        while (!IsEmpty)
            _eventQueue?.TryDequeue(out var _);
    }

   /// <summary>
   /// Processes items from the event queue in a loop until cancellation is requested.
   /// </summary>
   /// <remarks>This method continuously reads items from the event queue and processes them using the
   /// configured  event queue reader delegate. If a delay is specified, it waits for the specified duration after 
   /// processing each item before continuing. The method exits when the cancellation token is triggered  or when the
   /// processing loop is stopped.  The method is designed to handle concurrent processing of queue items and ensures
   /// that the  processing respects the specified delay (if any) between items. Callers should ensure that the  event
   /// queue and related synchronization objects are properly initialized before invoking this method.</remarks>
   /// <param name="cancelToken">A <see cref="CancellationToken"/> used to signal the cancellation of the queue processing loop.</param>
    void ReadEventQueue(CancellationToken cancelToken)
    {
        try
        {
            do
            {
                _eventQueueWaitEvent?.WaitOne();
                if (cancelToken.IsCancellationRequested)
                    break;
                do
                {
                    var queueItems = new List<TData>();
                    while (!IsEmpty)
                        if (_eventQueue!.TryDequeue(out var queueItem))
                            queueItems.Add(queueItem);
                    if (queueItems.Count > 0)
                    {
                        if (_delayAfterReadInMs > 0)
                        {
                            var queueItem = queueItems.LastOrDefault();
                            if (queueItem is not null)
                            {
                                Task.Run(async () =>
                                {
                                    await _eventQueueReader(queueItem);
                                    await Task.Delay(TimeSpan.FromMilliseconds(_delayAfterReadInMs));
                                    _eventQueueRunEvent!.Set();
                                });
                                _eventQueueRunEvent!.WaitOne();
                            }
                        }
                        else
                        {
                            foreach (var queueItem in queueItems)
                            {
                                Task.Run(async () =>
                                {
                                    await _eventQueueReader(queueItem);
                                    _eventQueueRunEvent!.Set();
                                });
                                _eventQueueRunEvent!.WaitOne();
                            }
                        }
                    }
               } while (!IsEmpty);
            } while (_isStarted);
        }
        catch
        {
        }
    }
}
