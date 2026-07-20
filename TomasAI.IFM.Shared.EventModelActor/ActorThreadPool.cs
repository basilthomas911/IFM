using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor
{
    /// <summary>
    /// Lightweight, thread-safe implementation of <see cref="IActorThreadPool"/> that
    /// waits for messages from actor mailbox queues and uses ConcurrentAsyncEventChannel in each actor thread.
    /// </summary>
    public class ActorThreadPool : IActorThreadPool
    {
        private readonly ConcurrentDictionary<ActorMailboxId, IActorThread> _threads = new();
        private readonly ILogger<EventChannel>? _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ActorThreadPool"/>.
        /// </summary>
        /// <param name="logger">Optional logger forwarded to underlying event channels.</param>
        public ActorThreadPool(ILogger<EventChannel>? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool AddActor(IActor actor)
        {
            if (actor is null) throw new ArgumentNullException(nameof(actor));
            var id = actor.Mailbox.Id;
            var thread = new PoolActorThread(actor, _logger);

            if (_threads.TryAdd(id, thread))
            {
                thread.Start();
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool RemoveActor(ActorMailboxId mailboxId)
        {
            if (_threads.TryRemove(mailboxId, out var thread))
            {
                try
                {
                    thread.Stop();
                }
                catch
                {
                    // best-effort stop
                }

                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public IActorThread GetActorThread(ActorSubject subject)
        {
            if (subject is null) throw new ArgumentNullException(nameof(subject));
            var id = subject.ToMailboxId();
            if (_threads.TryGetValue(id, out var thread))
                return thread;

            throw new KeyNotFoundException($"Actor thread for mailbox '{id}' not found.");
        }

        /// <inheritdoc />
        public bool Exists(ActorMailboxId mailboxId) => _threads.ContainsKey(mailboxId);

        /// <inheritdoc />
        public bool IsRunning(ActorMailboxId mailboxId)
            => _threads.TryGetValue(mailboxId, out var t) && t.IsRunning;

        /// <inheritdoc />
        public bool IsStarted(ActorMailboxId mailboxId)
            => _threads.TryGetValue(mailboxId, out var s) && s.IsStarted;

        /// <inheritdoc />
        public bool IsStopped(ActorMailboxId mailboxId)
            => _threads.TryGetValue(mailboxId, out var st) && st.IsStopped;

        /// <inheritdoc />
        public int Count => _threads.Count;

        /// <summary>
        /// Actor thread implementation that waits on the actor mailbox queue for messages,
        /// forwards messages into a ConcurrentAsyncEventChannel and manages timer/exception behavior.
        /// </summary>
        private sealed class PoolActorThread : IActorThread
        {
            private readonly IActor _actor;
            private readonly ConcurrentAsyncEventChannel<IActorMessage> _channel;
            private readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
            private Timer? _timer;
            private volatile ActorThreadState _state;
            private Exception? _exception;

            /// <summary>
            /// Creates a new instance of <see cref="PoolActorThread"/> for the provided actor.
            /// </summary>
            /// <param name="actor">Actor to wrap.</param>
            /// <param name="logger">Optional logger forwarded to the event channel.</param>
            public PoolActorThread(IActor actor, ILogger<EventChannel>? logger)
            {
                _actor = actor ?? throw new ArgumentNullException(nameof(actor));
                _state = ActorThreadState.Ready;
                var channelName = $"ActorChannel-{_actor.Mailbox.Id}";
                _channel = new ConcurrentAsyncEventChannel<IActorMessage>(channelName, OnMessageAsync, logger);
            }

            /// <summary>
            /// Posts a message by writing it into the actor mailbox queue.
            /// </summary>
            public bool Post(IActorMessage message)
            {
                if (message is null) throw new ArgumentNullException(nameof(message));
                if (IsStopped || IsFaulted) return false;

                try
                {
                    _actor.Mailbox.Queue.Write(message);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// Starts reading from the mailbox queue in a background loop and starts the internal channel.
            /// </summary>
            public bool Start()
            {
                if (IsStarted) return true;

                _channel.Start();
                _state = ActorThreadState.Started;

                // Start mailbox reader loop
                Task.Run(() => MailboxReaderLoopAsync());
                return true;
            }

            /// <summary>
            /// Stops mailbox reading and the internal channel.
            /// </summary>
            public bool Stop()
            {
                if (IsStopped) return true;

                try
                {
                    _channel.Stop();
                    _timer?.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _state = ActorThreadState.Stopped;
                }

                return true;
            }

            public bool IsRunning => _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage;
            public bool IsStarted => _state == ActorThreadState.Started || _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage;
            public bool IsStopped => _state == ActorThreadState.Stopped;
            public bool IsFaulted => _state == ActorThreadState.Faulted;
            public ActorThreadState State => _state;
            public Exception? Exception => _exception;

            private async Task MailboxReaderLoopAsync()
            {
                var queue = _actor.Mailbox.Queue;
                try
                {
                    while (true)
                    {
                        _state = ActorThreadState.WaitingForMessage;
                        // wait for a message to arrive
                        await queue.WaitForMessageAsync().ConfigureAwait(false);

                        // drain available messages
                        while (!queue.IsEmpty)
                        {
                            var message = queue.Read();

                            // push into channel for processing
                            var written = _channel.WriteData(message);
                            if (!written)
                            {
                                // channel refused the message; mark fault
                                _exception = new InvalidOperationException("Channel refused message");
                                SetFaulted(_exception);
                                return;
                            }

                            // reset timer when processing starts
                            ResetTimer();
                        }

                        if (IsStopped || IsFaulted)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    SetFaulted(ex);
                }
            }

            private ValueTask OnMessageAsync(IActorMessage message)
            {
                // called by the event channel's reader loop
                _state = ActorThreadState.ProcessingMessage;
                ResetTimer();

                try
                {
                    var task = _actor.ReceiveAsync(message).AsTask();
                    return new ValueTask(task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            var ex = t.Exception?.GetBaseException() ?? new Exception("Unknown processing error");
                            SetFaulted(ex);
                        }
                        else
                        {
                            _state = ActorThreadState.WaitingForMessage;
                            // reset timer back to full timeout ready for next message
                            ResetTimer();
                        }
                    }));
                }
                catch (Exception ex)
                {
                    SetFaulted(ex);
                    return ValueTask.CompletedTask;
                }
            }

            private void SetFaulted(Exception ex)
            {
                _exception = ex;
                _state = ActorThreadState.Faulted;
                try
                {
                    _channel.Stop();
                }
                catch { }
            }

            private void ResetTimer()
            {
                try
                {
                    _timer?.Dispose();
                    _timer = new Timer(OnTimeout, null, _timeout, Timeout.InfiniteTimeSpan);
                }
                catch { }
            }

            private void OnTimeout(object? state)
            {
                try
                {
                    _state = ActorThreadState.TimedOut;
                    _channel.Stop();
                    // stop mailbox by disposing timer and leaving loop to end when reader exits
                    _timer?.Dispose();
                }
                catch { }
            }
        }
    }
}
