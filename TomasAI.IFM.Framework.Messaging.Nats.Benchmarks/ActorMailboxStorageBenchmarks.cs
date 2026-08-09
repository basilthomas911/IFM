using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Compares interchangeable Channel and bounded MPSC ring actor mailboxes.</summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMailboxStorageBenchmarks
{
    const int Capacity = 8192;
    const int ConcurrentOperationCount = 4096;
    IActorThreadQueue _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    BenchmarkActorMessage _message = null!;
    DedicatedProducerGroup _producers = null!;

    [Params(ActorMailboxImplementation.Channel, ActorMailboxImplementation.MpscRing)]
    public ActorMailboxImplementation Implementation { get; set; }

    [Params(1, 4, 8)]
    public int ProducerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _queue = CreateQueue(Implementation, Capacity);
        _queue.SetId(new ActorThreadId(ActorType.Command, "MailboxBenchmark", "1"));
        _queue.Start();
        _scheduled = (IScheduledActorThreadQueue)_queue;
        _message = new BenchmarkActorMessage();
        _producers = new DedicatedProducerGroup(
            _scheduled,
            _message,
            ProducerCount,
            ConcurrentOperationCount / ProducerCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producers.Dispose();
        _queue.Stop();
    }

    [Benchmark(OperationsPerInvoke = ConcurrentOperationCount)]
    public int ConcurrentProducerBatch()
    {
        _producers.RunBatch();
        var read = 0;
        while (_scheduled.TryRead(out _))
            read++;
        return read;
    }

    internal sealed class DedicatedProducerGroup : IDisposable
    {
        readonly IScheduledActorThreadQueue _queue;
        readonly IActorMessage _message;
        readonly int _operationsPerProducer;
        readonly AutoResetEvent[] _startSignals;
        readonly Thread[] _threads;
        readonly CountdownEvent _completed;
        Exception? _failure;
        int _stopping;

        internal DedicatedProducerGroup(
            IScheduledActorThreadQueue queue,
            IActorMessage message,
            int producerCount,
            int operationsPerProducer)
        {
            _queue = queue;
            _message = message;
            _operationsPerProducer = operationsPerProducer;
            _completed = new CountdownEvent(producerCount);
            _startSignals = new AutoResetEvent[producerCount];
            _threads = new Thread[producerCount];
            for (var index = 0; index < producerCount; index++)
            {
                var producer = index;
                _startSignals[index] = new AutoResetEvent(false);
                _threads[index] = new Thread(() => RunProducer(producer))
                {
                    IsBackground = true,
                    Name = $"ActorMailboxBenchmarkProducer-{index}"
                };
                _threads[index].Start();
            }
        }

        internal void RunBatch()
        {
            _completed.Reset(_threads.Length);
            foreach (var signal in _startSignals)
                signal.Set();
            _completed.Wait();
            if (_failure is not null)
                throw new InvalidOperationException("A benchmark producer failed.", _failure);
        }

        void RunProducer(int producer)
        {
            while (true)
            {
                _startSignals[producer].WaitOne();
                if (Volatile.Read(ref _stopping) != 0)
                    return;
                try
                {
                    for (var operation = 0; operation < _operationsPerProducer; operation++)
                    {
                        if (!_queue.TryWrite(_message, default))
                            throw new InvalidOperationException("Benchmark mailbox unexpectedly rejected a write.");
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(ref _failure, exception, null);
                }
                finally
                {
                    _completed.Signal();
                }
            }
        }

        public void Dispose()
        {
            Volatile.Write(ref _stopping, 1);
            foreach (var signal in _startSignals)
                signal.Set();
            foreach (var thread in _threads)
                thread.Join();
            foreach (var signal in _startSignals)
                signal.Dispose();
            _completed.Dispose();
        }
    }

    internal static IActorThreadQueue CreateQueue(
        ActorMailboxImplementation implementation,
        int capacity)
        => implementation switch
        {
            ActorMailboxImplementation.Channel => new ActorThreadQueueV2(capacity),
            ActorMailboxImplementation.MpscRing => new ActorThreadQueueMpscRing(capacity),
            ActorMailboxImplementation.SpscRing => new ActorThreadQueueSpscRing(capacity),
            _ => throw new ArgumentOutOfRangeException(nameof(implementation))
        };

    sealed class BenchmarkActorMessage : IActorMessage
    {
        public int AdmissionSizeBytes => 256;
        public ActorSubject Subject { get; } = new(ActorType.Command, "MailboxBenchmark", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMailboxRoundTripBenchmarks
{
    IActorThreadQueue _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    RoundTripMessage _message = null!;

    [Params(
        ActorMailboxImplementation.Channel,
        ActorMailboxImplementation.MpscRing,
        ActorMailboxImplementation.SpscRing)]
    public ActorMailboxImplementation Implementation { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _queue = ActorMailboxStorageBenchmarks.CreateQueue(Implementation, 8192);
        _queue.SetId(new ActorThreadId(ActorType.Command, "MailboxRoundTripBenchmark", "1"));
        _queue.Start();
        _scheduled = (IScheduledActorThreadQueue)_queue;
        _message = new RoundTripMessage();
    }

    [GlobalCleanup]
    public void Cleanup() => _queue.Stop();

    [Benchmark(OperationsPerInvoke = 256)]
    public int ScheduledRoundTrip()
    {
        var read = 0;
        for (var index = 0; index < 256; index++)
        {
            _ = _scheduled.TryWrite(_message, default);
            _ = _scheduled.TrySchedule();
            if (_scheduled.TryRead(out _))
                read++;
            _ = _scheduled.CompleteDrain();
        }
        return read;
    }

    sealed class RoundTripMessage : IActorMessage
    {
        public ActorSubject Subject { get; } = new(ActorType.Command, "MailboxRoundTripBenchmark", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMailboxSpscCapacityBenchmarks
{
    const int OperationCount = 4096;
    IActorThreadQueue _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    CapacityMessage _message = null!;
    ActorMailboxStorageBenchmarks.DedicatedProducerGroup _producer = null!;

    [Params(ActorMailboxImplementation.Channel, ActorMailboxImplementation.SpscRing)]
    public ActorMailboxImplementation Implementation { get; set; }

    [Params(8192, 65536)]
    public int Capacity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _queue = ActorMailboxStorageBenchmarks.CreateQueue(Implementation, Capacity);
        _queue.SetId(new ActorThreadId(ActorType.Command, "MailboxSpscCapacityBenchmark", "1"));
        _queue.Start();
        _scheduled = (IScheduledActorThreadQueue)_queue;
        _message = new CapacityMessage();
        _producer = new ActorMailboxStorageBenchmarks.DedicatedProducerGroup(
            _scheduled,
            _message,
            producerCount: 1,
            operationsPerProducer: OperationCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Dispose();
        _queue.Stop();
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public int SingleProducerBatch()
    {
        _producer.RunBatch();
        var read = 0;
        while (_scheduled.TryRead(out _))
            read++;
        return read;
    }

    sealed class CapacityMessage : IActorMessage
    {
        public int AdmissionSizeBytes => 256;
        public ActorSubject Subject { get; } = new(ActorType.Command, "MailboxSpscCapacityBenchmark", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMailboxScheduledBurstBenchmarks
{
    const int OperationCount = 4096;
    IActorThreadQueue _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    ScheduledBurstMessage _message = null!;

    [Params(
        ActorMailboxImplementation.Channel,
        ActorMailboxImplementation.MpscRing,
        ActorMailboxImplementation.SpscRing)]
    public ActorMailboxImplementation Implementation { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _queue = ActorMailboxStorageBenchmarks.CreateQueue(Implementation, 8192);
        _queue.SetId(new ActorThreadId(ActorType.Command, "MailboxScheduledBurstBenchmark", "1"));
        _queue.Start();
        _scheduled = (IScheduledActorThreadQueue)_queue;
        _message = new ScheduledBurstMessage();
    }

    [GlobalCleanup]
    public void Cleanup() => _queue.Stop();

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public int EnqueueScheduleAndDrainBurst()
    {
        var scheduleWins = 0;
        for (var index = 0; index < OperationCount; index++)
        {
            if (!_scheduled.TryWrite(_message, default))
                throw new InvalidOperationException("Benchmark mailbox unexpectedly rejected a write.");
            if (_scheduled.TrySchedule())
                scheduleWins++;
        }

        var read = 0;
        while (_scheduled.TryRead(out _))
            read++;
        if (_scheduled.CompleteDrain())
            throw new InvalidOperationException("Benchmark mailbox unexpectedly retained scheduled work.");
        return read + scheduleWins;
    }

    sealed class ScheduledBurstMessage : IActorMessage
    {
        public ActorSubject Subject { get; } = new(
            ActorType.Command,
            "MailboxScheduledBurstBenchmark",
            "Run",
            "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}

/// <summary>Measures the retained-allocation cost of constructing an empty mailbox at production capacity.</summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMailboxCreationBenchmarks
{
    [Params(
        ActorMailboxImplementation.Channel,
        ActorMailboxImplementation.MpscRing,
        ActorMailboxImplementation.SpscRing)]
    public ActorMailboxImplementation Implementation { get; set; }

    [Params(64, 256, 1024, 8192)]
    public int Capacity { get; set; }

    [Benchmark]
    public int CreateStartStop()
    {
        using var queue = (IDisposable)ActorMailboxStorageBenchmarks.CreateQueue(Implementation, Capacity);
        var actorQueue = (IActorThreadQueue)queue;
        actorQueue.SetId(new ActorThreadId(ActorType.Command, "MailboxCreationBenchmark", "1"));
        actorQueue.Start();
        return actorQueue.Count;
    }
}
