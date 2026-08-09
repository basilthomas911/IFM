using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Measures the actor mailbox hot path with instruments dormant and with an active listener.</summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorMetricsBenchmarks
{
    ActorThreadQueueV2 _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    BenchmarkActorMessage _message = null!;
    MeterListener? _listener;

    [Params(false, true)]
    public bool MetricsEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (MetricsEnabled)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = static (instrument, listener) =>
                {
                    if (instrument.Meter.Name == ActorRuntimeMetrics.MeterName)
                        listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>(static (_, _, _, _) => { });
            _listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            _listener.Start();
        }

        _queue = new ActorThreadQueueV2(1024);
        _queue.SetId(new ActorThreadId(ActorType.Command, "MetricsBenchmark", "1"));
        _queue.Start();
        _scheduled = _queue;
        _message = new BenchmarkActorMessage();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _queue.Dispose();
        _listener?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = 256)]
    public int ScheduledEnqueueDequeueBatch()
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

    sealed class BenchmarkActorMessage : IActorMessage
    {
        public ActorSubject Subject { get; } = new(ActorType.Command, "MetricsBenchmark", "Run", "1");
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
