using System.Collections.Concurrent;

namespace TomasAI.IFM.Shared.EventQueue;

public class ConcurrentEventQueue<TData>
{
    readonly Action<TData>? _eventQueueReader;
    readonly Func<TData,Task>? _eventAsyncQueueReader;
    readonly EventQueueReaderMode _readerMode;
    ConcurrentQueue<TData>? _eventQueue;
    AutoResetEvent? _eventQueueResetEvent;
    Thread? _eventQueueThread;
    readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentEventQueue{TData}"/> class with the specified event queue
    /// reader.
    /// </summary>
    /// <remarks>The <paramref name="eventQueueReader"/> parameter is required to handle the processing of
    /// events. The queue operates in synchronous reader mode by default.</remarks>
    /// <param name="eventQueueReader">A delegate that processes events from the queue. This action is invoked for each item in the queue.</param>
    public ConcurrentEventQueue(Action<TData> eventQueueReader )
    {
        _eventQueueReader = eventQueueReader;
        _readerMode = EventQueueReaderMode.Sync;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentEventQueue{TData}"/> class with an asynchronous event
    /// queue reader.
    /// </summary>
    /// <remarks>This constructor sets the queue to operate in asynchronous reader mode. The provided
    /// <paramref name="eventQueueReader"/> is used to handle events as they are dequeued.</remarks>
    /// <param name="eventQueueReader">A delegate that processes events asynchronously. The delegate is invoked for each event in the queue and is
    /// expected to return a <see cref="Task"/> representing the asynchronous operation.</param>
    public ConcurrentEventQueue(Func<TData,Task> eventQueueReader)
    {
        _eventAsyncQueueReader = eventQueueReader;
        _readerMode = EventQueueReaderMode.Async;
    }

    public bool IsEmpty => _eventQueue == null || _eventQueue.IsEmpty;

    /// <summary>
    /// Initializes and starts the event queue processing thread.
    /// </summary>
    /// <remarks>This method sets up the internal event queue and starts a background thread with the highest
    /// priority  to process events. The thread will run until the associated cancellation token is triggered.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentEventQueue{TData}"/>, allowing for method chaining.</returns>
    public ConcurrentEventQueue<TData> Start()
    {
        try
        {
            _eventQueue = new ConcurrentQueue<TData>();
            _eventQueueResetEvent = new AutoResetEvent(false);
            _eventQueueThread = new Thread(() => ReadEventQueue(_cancellationTokenSource.Token)) { Priority = ThreadPriority.Highest, IsBackground = true };
            _eventQueueThread.Start();
        }
        catch { }
        return this;
    }

    /// <summary>
    /// Stops the event queue, cancels any ongoing operations, and releases associated resources.
    /// </summary>
    /// <remarks>This method cancels the internal event processing thread, signals any waiting operations to
    /// complete,  and disposes of the resources used by the event queue. After calling this method, the event queue 
    /// cannot be restarted, and further operations on it may result in undefined behavior.</remarks>
    /// <returns>The current instance of <see cref="ConcurrentEventQueue{TData}"/> for method chaining.</returns>
    public ConcurrentEventQueue<TData> Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _eventQueueResetEvent?.Set();
        }
        catch { }
        finally
        {
            _eventQueueThread = null;
            _eventQueue = null;
            _eventQueueResetEvent = null;
            _cancellationTokenSource.Dispose();
        }
        return this;
    }

    /// <summary>
    /// Adds the specified item to the event queue for processing.
    /// </summary>
    /// <remarks>This method enqueues the provided item into the internal event queue, if the queue is
    /// initialized. Ensure that the queue is properly configured before calling this method.</remarks>
    /// <param name="queueItem">The item to be added to the queue. Cannot be null.</param>
    public void EnqueueForSignal(TData queueItem) 
        => _eventQueue?.Enqueue(queueItem);

    /// <summary>
    /// Signals the event to allow waiting threads to proceed.
    /// </summary>
    /// <remarks>This method sets the underlying event, if it is not null, to release any threads waiting on
    /// it. Ensure that the event has been properly initialized before calling this method.</remarks>
    public void Signal() 
        => _eventQueueResetEvent?.Set();

    /// <summary>
    /// Adds an item to the queue and signals that the queue has been updated.
    /// </summary>
    /// <remarks>This method ensures that the item is enqueued and a signal is sent to notify any waiting
    /// processes or threads that the queue has been updated.</remarks>
    /// <param name="queueItem">The item to add to the queue. Cannot be null.</param>
    public void EnqueueAndSignal(TData queueItem)
    {
        EnqueueForSignal(queueItem);
        Signal();
    }

    /// <summary>
    /// Clears all events from the queue.
    /// </summary>
    /// <remarks>This method removes all items from the internal event queue.  After calling this method, the
    /// queue will be empty.</remarks>
    public void Clear()
    {
        while (!IsEmpty)
            _eventQueue?.TryDequeue(out _);
    }

    /// <summary>
    /// Processes items from the event queue until cancellation is requested.
    /// </summary>
    /// <remarks>This method continuously monitors and processes items from the event queue. It waits for the
    /// queue to be populated,  processes all available items, and then repeats the process until the provided <paramref
    /// name="cancelToken"/> signals cancellation.  The processing mode is determined by the <see
    /// cref="EventQueueReaderMode"/> configuration: <list type="bullet"> <item><description><see
    /// cref="EventQueueReaderMode.Sync"/> processes items synchronously.</description></item> <item><description><see
    /// cref="EventQueueReaderMode.Async"/> processes items asynchronously.</description></item> </list>  If the queue
    /// is empty, the method waits for a signal before attempting to process items again. Exceptions occurring during 
    /// processing are caught and suppressed.</remarks>
    /// <param name="cancelToken">A <see cref="CancellationToken"/> used to signal the cancellation of the queue processing.</param>
    void ReadEventQueue(CancellationToken cancelToken)
    {
        try
        {
            do
            {
                _eventQueueResetEvent?.WaitOne();
                if (cancelToken.IsCancellationRequested)
                    break;
                do
                {
                    List<TData> queueItems = [];
                    while (!IsEmpty)
                        if (_eventQueue!.TryDequeue(out var queueItem))
                            queueItems.Add(queueItem);
                    if (queueItems.Count > 0)
                    {
                        _ = _readerMode switch {  
                            EventQueueReaderMode.Sync => ForEach(queueItems), 
                            EventQueueReaderMode.Async => ForEachAsync(queueItems), 
                            _ => false 
                        };
                    }
                } while (!IsEmpty);

            } while (true);
        }
        catch
        {
        }

        bool ForEach(List<TData> queueItems)
        {
            queueItems.ForEach(queueItem => _eventQueueReader?.Invoke(queueItem));
            return true;
        }

        bool ForEachAsync(List<TData> queueItems)
        {
            queueItems.ForEach(queueItem => _eventAsyncQueueReader?.Invoke(queueItem)?.Wait());
            return true;
        }
    }
}
