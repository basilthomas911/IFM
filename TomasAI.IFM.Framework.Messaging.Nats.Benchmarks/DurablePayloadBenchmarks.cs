using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>
/// Measures the two stable arrays required by the durable replay schema: the polymorphic
/// event payload and the independently retained outer envelope.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class DurablePayloadBenchmarks
{
    SerializationBenchmarks.BenchmarkEnvelope _event = null!;

    [Params(256, 4096)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var payload = GC.AllocateUninitializedArray<byte>(PayloadSize);
        new Random(42).NextBytes(payload);
        _event = new SerializationBenchmarks.BenchmarkEnvelope
        {
            Id = Guid.Parse("cfb9092d-042e-48c1-a5ff-cf8bb6dd83e3"),
            Sequence = 42,
            Subject = "Event.FuturesBarData.Projected.ESZ26",
            Payload = payload
        };
    }

    [Benchmark(Description = "Durable polymorphic payload plus retained envelope")]
    public byte[] SerializeDurableEnvelope()
    {
        var eventPayload = MessagePackSerializer.Serialize(
            typeof(SerializationBenchmarks.BenchmarkEnvelope),
            _event,
            MessagePackBinarySerializer.Options);
        var envelope = new DurableBenchmarkEnvelope(
            2,
            typeof(SerializationBenchmarks.BenchmarkEnvelope).AssemblyQualifiedName!,
            eventPayload);
        return MessagePackSerializer.Serialize(envelope, MessagePackBinarySerializer.Options);
    }

    [MessagePackObject]
    public sealed record DurableBenchmarkEnvelope(
        [property: Key(0)] int Version,
        [property: Key(1)] string EventType,
        [property: Key(2)] byte[] EventPayload);
}
