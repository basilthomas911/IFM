using BenchmarkEnvelope = TomasAI.IFM.Framework.Messaging.Nats.Benchmarks.SerializationBenchmarks.BenchmarkEnvelope;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Measures additive provenance cost against the unchanged body-only send preparation.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class MessageOriginBenchmarks
{
    readonly ArrayBufferWriter<byte> writer = new(8192);
    BenchmarkEnvelope message = null!;
    [Params(256,4096)] public int PayloadSize { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        message = new BenchmarkEnvelope { Id = Guid.NewGuid(), Sequence = 1,
            Subject = "actors.fund.commands.create", Payload = new byte[PayloadSize] };
        MessageOrigins.Capture(message, TimeProvider.System);
    }
    [Benchmark(Baseline = true)]
    public int BeforeBodyOnly() => Serialize(message);
    [Benchmark]
    public int AfterForwardedMessage()
    {
        var headers = MessageOriginHeaders.Add(null, MessageOrigins.ForPublication(message, TimeProvider.System));
        return Serialize(message) + headers!.Count;
    }
    [Benchmark]
    public int AfterNewMessage()
    {
        var created = new BenchmarkEnvelope { Id = message.Id, Sequence = message.Sequence,
            Subject = message.Subject, Payload = message.Payload };
        var headers = MessageOriginHeaders.Add(null, MessageOrigins.ForPublication(created, TimeProvider.System));
        return Serialize(created) + headers!.Count;
    }
    int Serialize(BenchmarkEnvelope value)
    {
        writer.Clear();
        NatsMessagePackSerializer<BenchmarkEnvelope>.Default.Serialize(writer,value);
        return writer.WrittenCount;
    }
}
