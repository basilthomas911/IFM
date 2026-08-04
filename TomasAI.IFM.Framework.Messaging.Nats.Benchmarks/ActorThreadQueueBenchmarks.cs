using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ActorThreadQueueBenchmarks
{
    ActorThreadQueueV2 _queue = null!;
    IScheduledActorThreadQueue _scheduledQueue = null!;
    BenchmarkActorMessage _message = null!;

    [GlobalSetup]
    public void Setup()
    {
        _queue = new ActorThreadQueueV2(1024, 32, 32);
        _queue.Start();
        _scheduledQueue = _queue;
        _message = new BenchmarkActorMessage();
    }

    [GlobalCleanup]
    public void Cleanup() => _queue.Dispose();

    [Benchmark(OperationsPerInvoke = 256)]
    public int EnqueueDequeueBatch()
    {
        for (var i = 0; i < 256; i++)
        {
            _queue.Write(_message);
            using var enumerator = _queue.ReadAll().GetEnumerator();
            _ = enumerator.MoveNext();
        }

        return _queue.Count;
    }

    [Benchmark(OperationsPerInvoke = 256)]
    public int ScheduledHotPathBatch()
    {
        for (var i = 0; i < 256; i++)
        {
            _scheduledQueue.TryWrite(_message, default);
            _ = _scheduledQueue.TrySchedule();
        }

        var read = 0;
        while (_scheduledQueue.TryRead(out _))
            read++;
        _ = _scheduledQueue.CompleteDrain();
        return read;
    }

    sealed class BenchmarkActorMessage : IActorMessage
    {
        public ActorSubject Subject { get; } = new(ActorType.Command, "Benchmark", "Run", "1");
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
