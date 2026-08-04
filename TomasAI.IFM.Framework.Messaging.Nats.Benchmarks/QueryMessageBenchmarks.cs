using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>
/// Isolates the allocations removed from the query request/reply boundary.
/// Transport and HTTP allocations are intentionally excluded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class QueryMessageBenchmarks
{
    readonly NatsByteArrayMessageSerializer _natsBytes = new();
    readonly MessagePackBinarySerializer _messagePack = new();
    readonly NatsMessagePackSerializer<SerializationBenchmarks.BenchmarkEnvelope> _querySerializer =
        NatsMessagePackSerializer<SerializationBenchmarks.BenchmarkEnvelope>.Default;
    readonly NatsMessagePackSerializer<ServiceResult<SerializationBenchmarks.BenchmarkEnvelope>>
        _replySerializer =
            NatsMessagePackSerializer<ServiceResult<SerializationBenchmarks.BenchmarkEnvelope>>.Default;

    byte[] _serializedQuery = null!;
    ReadOnlySequence<byte> _querySequence;
    SerializationBenchmarks.BenchmarkEnvelope _result = null!;
    ServiceResult<SerializationBenchmarks.BenchmarkEnvelope> _reply = null!;
    FixedBufferWriter _writer = null!;

    [Params(256, 4096)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var payload = GC.AllocateUninitializedArray<byte>(PayloadSize);
        new Random(42).NextBytes(payload);
        _result = new SerializationBenchmarks.BenchmarkEnvelope
        {
            Id = Guid.Parse("1fae5726-9f66-45cc-904f-8dd953cf003d"),
            Sequence = 42,
            Subject = "Query.FundQuery.GetFundBalance.42",
            Payload = payload
        };
        _reply = new ServiceResult<SerializationBenchmarks.BenchmarkEnvelope>(_result);
        _serializedQuery = _messagePack.Serialize(_result)!;
        _querySequence = new ReadOnlySequence<byte>(_serializedQuery);
        _writer = new FixedBufferWriter(PayloadSize + 4096);
    }

    [Benchmark(Baseline = true, Description = "Query ingress legacy byte[] copy")]
    public SerializationBenchmarks.BenchmarkEnvelope? LegacyQueryIngress()
    {
        var bytes = _natsBytes.Deserialize(_querySequence)!;
        return _messagePack.Deserialize<SerializationBenchmarks.BenchmarkEnvelope>(bytes);
    }

    [Benchmark(Description = "Query ingress owned pooled memory")]
    public SerializationBenchmarks.BenchmarkEnvelope? OwnedQueryIngress()
        => _querySerializer.Deserialize(_querySequence);

    [Benchmark(Description = "Query reply legacy intermediate byte[]")]
    public int LegacyQueryReply()
    {
        var bytes = _messagePack.Serialize(_reply)!;
        _writer.Reset();
        _natsBytes.Serialize(_writer, bytes);
        return _writer.WrittenCount;
    }

    [Benchmark(Description = "Query reply typed direct-to-NATS")]
    public int DirectQueryReply()
    {
        _writer.Reset();
        _replySerializer.Serialize(_writer, _reply);
        return _writer.WrittenCount;
    }

    sealed class FixedBufferWriter(int capacity) : IBufferWriter<byte>
    {
        readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(capacity);
        int _written;

        public int WrittenCount => _written;
        public void Advance(int count) => _written += count;
        public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_written);
        public Span<byte> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_written);
        public void Reset() => _written = 0;
    }
}
