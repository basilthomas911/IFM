using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a thread pool for managing actor threads in an actor-based system.
/// </summary>
/// <remarks>The <see cref="ActorThreadPool"/> class provides functionality to initialize, retrieve, and manage
/// actor threads within a thread pool. It ensures efficient allocation of threads to actors and allows threads to be
/// returned to the pool after use. The thread pool is initialized with a specified number of threads and supports
/// operations such as retrieving threads for specific actors and returning them to the pool. <para> This class is
/// designed to work in conjunction with an <see cref="IActorSupervisor"/> to manage actor lifecycles and a logger for
/// diagnostic purposes. The default timeout for thread operations can be configured during initialization.
/// </para></remarks>
/// <param name="context"></param>
/// <param name="logger"></param>
public class ActorThreadPool(IActorSupervisor context, ILogger logger) 
    : IActorThreadPool
{
    readonly IActorSupervisor _context = IsArgumentNull.Set(context);
    readonly ConcurrentDictionary<ActorThreadId, IActorThread> _runningThreads = new();
    readonly BlockingThreadPoolQueue _threadPool = new(logger);
    readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(2);
    readonly ILogger _logger = IsArgumentNull.Set(logger);

    /// <summary>
    /// Initializes the thread pool with the specified number of threads.
    /// </summary>
    /// <param name="initialThreadCount">The number of threads to create and add to the thread pool. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="initialThreadCount"/> is less than or equal to zero.</exception>
    public IActorThreadPool Initialize(int initialThreadCount)
    {
        if (initialThreadCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialThreadCount), "Initial thread count must be greater than zero.");
        for (int i = 0; i < initialThreadCount; i++)
        {
            var thread = new ActorThread(this, _logger, _defaultTimeout);
            _threadPool.Enqueue(thread);
        }
        return this;
    }
    /// <summary>
    /// Retrieves the actor thread associated with the specified mailbox identifier.
    /// </summary>
    /// <param name="mailboxId">The unique identifier of the mailbox whose actor thread is to be retrieved.</param>
    /// <returns>The <see cref="IActorThread"/> instance associated with the specified mailbox identifier.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no actor thread is found for the specified <paramref name="mailboxId"/>.</exception>
    public IActorThread GetThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);

        if (_runningThreads.TryGetValue(threadId, out var actorThread))
            return actorThread!;

        var mailboxId = threadId.MailboxId;
        if (!_context.Children.ContainsKey(mailboxId))
            throw new KeyNotFoundException($"Actor with mailbox id '{mailboxId}' not found in context.");

        var actor = _context.Children[mailboxId];
        actorThread = _threadPool.Dequeue();
        actorThread.Start(actor, threadId);

        // Track the running thread so it can be retrieved/released later
        _runningThreads.TryAdd(threadId, actorThread);

        return actorThread!;
    }

    /// <summary>
    /// Returns an actor thread to the thread pool after stopping it.
    /// </summary>
    /// <param name="actorThread">The actor thread to be returned to the thread pool. Cannot be <see langword="null"/>.</param>
    public void ReleaseThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);

        if (!_runningThreads.TryRemove(threadId, out var actorThread))
            return;

        // Stop the thread and return it to the pool
        actorThread.Stop();
        _threadPool.Enqueue(actorThread);
    }

    /// <summary>
    /// Gets the number of threads currently in the thread pool.
    /// </summary>
    public int Count => _threadPool.Count;

    /// <inheritdoc />
    public ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct)
        => ValueTask.FromResult(GetThread(threadId));

}
