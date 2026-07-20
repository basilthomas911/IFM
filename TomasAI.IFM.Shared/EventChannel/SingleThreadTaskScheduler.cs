using System.Collections.Concurrent;
using System.Diagnostics;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Provides a task scheduler that executes tasks on a single thread in a sequential order.
/// </summary>
/// <remarks>The <see cref="SingleThreadTaskScheduler"/> is designed to execute tasks in a controlled, sequential
/// manner, ensuring that only one task is executed at a time. This is useful for scenarios where tasks need to be
/// processed in a specific order or when tasks should not run concurrently.</remarks>
public class SingleThreadTaskScheduler
    : TaskScheduler, IDisposable
{
    readonly BlockingCollection<Task> _queue = new();
    int _pendingCount = 1; // Represents the completion of masterTask

    /// <summary>
    /// Executes the specified asynchronous action using a custom task scheduler.
    /// </summary>
    /// <remarks>The method ensures that the provided action is executed in a controlled environment where
    /// child tasks are not attached to the parent. It waits for the completion of the action and propagates any
    /// exceptions that occur during its execution.</remarks>
    /// <param name="action">The asynchronous action to execute. This parameter cannot be <see langword="null"/>.</param>
    public static void Run(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using SingleThreadTaskScheduler scheduler = new();

        Task<Task> masterTaskTask = new(action, TaskCreationOptions.DenyChildAttach);
        Task masterTask = masterTaskTask.Unwrap();
        masterTaskTask.Start(scheduler);

        // The masterTask cannot be completed at this point.
        scheduler.HandleTaskCompletion(masterTask);

        foreach (Task task in scheduler._queue.GetConsumingEnumerable())
            scheduler.TryExecuteTask(task);

        // exit masterTask thread, so propagate all exceptions if still running
        try
        {
            if (!(masterTask.IsCanceled || masterTask.IsFaulted))
                masterTask?.Wait(); 
        }
        catch { }
    }

    SingleThreadTaskScheduler() { } // Prevent public instantiation

    void HandleTaskCompletion(Task task)
    {
        _ = task.ContinueWith(t => ErrorOnThreadPool(() =>
        {
            int pending = Interlocked.Decrement(ref _pendingCount);
            Debug.Assert(pending >= 0);
            if (pending == 0) 
                _queue.CompleteAdding();
        }), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    static void ErrorOnThreadPool(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            // Preserve previous behavior: rethrow on threadpool by wrapping exception in TaskSchedulerException.
            ThreadPool.QueueUserWorkItem(_ => throw new TaskSchedulerException(ex));
        }
    }

    protected override void QueueTask(Task task)
    {
        ErrorOnThreadPool(() =>
        {
            int pending = Interlocked.Increment(ref _pendingCount);
            Debug.Assert(pending > 0);
            HandleTaskCompletion(task);
            _queue.Add(task);
        });
    }

    protected override bool TryExecuteTaskInline(Task task,
        bool taskWasPreviouslyQueued) => false;

    public override int MaximumConcurrencyLevel => 1;
    protected override IEnumerable<Task> GetScheduledTasks() => _queue;

    public void Dispose() => _queue.Dispose();
}
