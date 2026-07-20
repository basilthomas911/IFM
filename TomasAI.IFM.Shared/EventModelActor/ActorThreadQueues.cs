using NATS.Client.Core;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a mechanism for managing and routing messages to thread-specific queues in an actor-based system.
/// </summary>
/// <remarks>This class maintains a collection of thread queues, each identified by a unique thread ID, and
/// facilitates message routing based on the subject of the message. It ensures that messages are processed by the
/// appropriate thread queue, creating new queues as needed. The class is designed to work in conjunction with an actor
/// supervisor to manage thread-specific processing.</remarks>
/// <param name="supervisor"></param>
public sealed class ActorThreadQueues(IActorSupervisor supervisor)
    : IActorThreadQueues
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ConcurrentDictionary<ActorThreadId, IActorThreadQueue> _threadQueues = new();

    /// <summary>
    /// Gets the number of thread queues currently managed.
    /// </summary>
    public int Count => _threadQueues.Count;

    /// <summary>
    /// Sends the specified message to the appropriate thread queue for processing.
    /// </summary>
    /// <remarks>The method routes the message to a thread queue based on the thread ID derived from the
    /// message's subject. Ensure that the <see cref="IActorMessage.Subject"/> property is properly set to avoid
    /// unexpected behavior.</remarks>
    /// <param name="message">The message to be sent. The <see cref="IActorMessage.Subject"/> property is used to determine the target thread
    /// queue.</param>
    public void Write(in NatsMsg<byte[]> message)
    {
        var msgSubject = message.Subject.ToSubject();
        var threadId = msgSubject.ThreadId;
        var thread = _supervisor.GetThread(threadId) ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{msgSubject.ActorId}'.");
        var threadQueue = GetThreadQueue(threadId);
        threadQueue.Start();
        if (threadQueue.Write(message))
            thread.SignalMessageAvailable(threadId);
    }

    public void Write(in NatsMsg<byte[]> message, ActorSubject msgSubject, CancellationToken cancellationToken = default)
    {
        var threadId = msgSubject.ThreadId;
        var thread = _supervisor.GetThread(threadId) ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{msgSubject.ActorId}'.");
        var threadQueue = GetThreadQueue(threadId);
        threadQueue.Start();
        if (threadQueue.Write(message, cancellationToken))
            thread.SignalMessageAvailable(threadId);
    }

    /// <summary>
    /// Asynchronously writes the specified message to the queue of the actor thread identified by the message's
    /// subject.
    /// </summary>
    /// <remarks>Uses a sync fast-path to avoid async state machine allocation when both
    /// <see cref="IActorSupervisor.GetThreadAsync"/> and <see cref="IActorThreadQueue.EnqueueAsync"/>
    /// complete synchronously (common steady-state case).</remarks>
    /// <param name="refNatsMsg">The message to enqueue for processing by the target actor thread. The message must contain a valid subject that
    /// identifies the intended thread.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation of writing the message to the actor thread
    /// queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the actor thread corresponding to the message's subject cannot be found.</exception>
    public ValueTask WriteAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    {
        var msgSubject = message.Subject.ToSubject();
        var threadId = msgSubject.ThreadId;
        var getThreadTask = _supervisor.GetThreadAsync(threadId, cancellationToken);
        if (getThreadTask.IsCompletedSuccessfully)
        {
            var thread = getThreadTask.Result ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{msgSubject.ActorId}'.");
            var threadQueue = GetThreadQueue(threadId);
            var enqueueTask = threadQueue.EnqueueAsync(message, cancellationToken);
            if (enqueueTask.IsCompletedSuccessfully)
            {
                thread.SignalMessageAvailable(threadId);
                return ValueTask.CompletedTask;
            }
            return AwaitEnqueueAndSignalAsync(enqueueTask, thread, threadId);
        }
        return WriteAsyncSlow(getThreadTask, message, msgSubject, cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes the specified message using a pre-parsed subject to avoid redundant parsing.
    /// </summary>
    /// <remarks>Uses a sync fast-path to avoid async state machine allocation when both
    /// <see cref="IActorSupervisor.GetThreadAsync"/> and <see cref="IActorThreadQueue.EnqueueAsync"/>
    /// complete synchronously.</remarks>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="subject">The pre-parsed actor subject from the consumer.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async operation.</returns>
    public ValueTask WriteAsync(NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default)
    {
        var threadId = subject.ThreadId;
        var getThreadTask = _supervisor.GetThreadAsync(threadId, cancellationToken);
        if (getThreadTask.IsCompletedSuccessfully)
        {
            var thread = getThreadTask.Result ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{subject.ActorId}'.");
            var threadQueue = GetThreadQueue(threadId);
            var enqueueTask = threadQueue.EnqueueAsync(message, cancellationToken);
            if (enqueueTask.IsCompletedSuccessfully)
            {
                thread.SignalMessageAvailable(threadId);
                return ValueTask.CompletedTask;
            }
            return AwaitEnqueueAndSignalAsync(enqueueTask, thread, threadId);
        }
        return WriteAsyncSlowWithSubject(getThreadTask, message, subject, cancellationToken);
    }

    /// <summary>
    /// Async fallback: awaits a pending enqueue, then signals the thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static async ValueTask AwaitEnqueueAndSignalAsync(ValueTask enqueueTask, IActorThread thread, ActorThreadId threadId)
    {
        await enqueueTask.ConfigureAwait(false);
        thread.SignalMessageAvailable(threadId);
    }

    /// <summary>
    /// Async slow path for <see cref="WriteAsync(NatsMsg{byte[]}, CancellationToken)"/> when GetThreadAsync
    /// does not complete synchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask WriteAsyncSlow(ValueTask<IActorThread> getThreadTask, NatsMsg<byte[]> message, ActorSubject msgSubject, CancellationToken cancellationToken)
    {
        var threadId = msgSubject.ThreadId;
        var thread = await getThreadTask.ConfigureAwait(false) ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{msgSubject.ActorId}'.");
        var threadQueue = GetThreadQueue(threadId);
        await threadQueue.EnqueueAsync(message, cancellationToken).ConfigureAwait(false);
        thread.SignalMessageAvailable(threadId);
    }

    /// <summary>
    /// Async slow path for <see cref="WriteAsync(NatsMsg{byte[]}, ActorSubject, CancellationToken)"/> when
    /// GetThreadAsync does not complete synchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask WriteAsyncSlowWithSubject(ValueTask<IActorThread> getThreadTask, NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken)
    {
        var threadId = subject.ThreadId;
        var thread = await getThreadTask.ConfigureAwait(false) ?? throw new InvalidOperationException($"Actor thread for id '{threadId}' not found when writing to mailbox '{subject.ActorId}'.");
        var threadQueue = GetThreadQueue(threadId);
        await threadQueue.EnqueueAsync(message, cancellationToken).ConfigureAwait(false);
        thread.SignalMessageAvailable(threadId);
    }

    /// <summary>
    /// Retrieves the thread queue associated with the specified thread ID, creating a new one if it does not already
    /// exist.
    /// </summary>
    /// <param name="threadId">The unique identifier of the thread for which to retrieve the queue. Cannot be <see langword="null"/>.</param>
    /// <returns>The <see cref="IActorThreadQueue"/> instance associated with the specified thread ID. If no queue exists for the
    /// given ID, a new one is created and returned.</returns>
    public IActorThreadQueue GetThreadQueue(ActorThreadId threadId)
    {
        return _threadQueues.GetOrAdd(threadId, static (id, supervisor) =>
        {
            var queue = supervisor.Container.Resolve<IActorThreadQueue>();
            queue.SetId(id);
            queue.Start();
            return queue;
        }, _supervisor);
    }

    /// <summary>
    /// Releases the thread queue associated with the specified thread identifier if it is empty.
    /// </summary>
    /// <remarks>Uses atomic compare-and-remove to avoid a TOCTOU race where a concurrent writer could
    /// enqueue a message between the Count == 0 check and the removal, which would silently lose that message.</remarks>
    /// <param name="threadId">The identifier of the actor thread whose queue is to be released. This parameter cannot be null.</param>
    public void ReleaseThreadQueue(ActorThreadId threadId)
    {
        if (_threadQueues.TryGetValue(threadId, out var threadQueue) && threadQueue.Count == 0)
        {
            threadQueue.Stop();
            ((ICollection<KeyValuePair<ActorThreadId, IActorThreadQueue>>)_threadQueues)
                .Remove(new KeyValuePair<ActorThreadId, IActorThreadQueue>(threadId, threadQueue));
        }
    }
   
}
