using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class SpscRingBufferBenchmarks
{
    NatsActorSpscRingBuffer _ring = null!;
    NatsActorMessage _message = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ring = new NatsActorSpscRingBuffer(
            capacityPowerOfTwo: 1024,
            spinCountEnqueue: 64,
            spinCountDequeue: 64);
        _ring.Start();
        _message = new NatsActorMessage(default);
    }

    [GlobalCleanup]
    public void Cleanup() => _ring.Stop();

    [Benchmark(OperationsPerInvoke = 256)]
    public int EnqueueDequeueBatch()
    {
        for (int i = 0; i < 256; i++)
        {
            _ring.Enqueue(_message);
            _ = _ring.Dequeue();
        }

        return _ring.Count;
    }
}
