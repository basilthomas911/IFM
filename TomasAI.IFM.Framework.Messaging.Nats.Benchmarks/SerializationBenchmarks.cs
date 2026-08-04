using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class SerializationBenchmarks
{
    readonly NatsByteArrayMessageSerializer _natsBytes = new();
    readonly MessagePackBinarySerializer _messagePack = new();
    readonly NatsMessagePackSerializer<BenchmarkEnvelope> _directMessagePack =
        NatsMessagePackSerializer<BenchmarkEnvelope>.Default;

    byte[] _payload = null!;
    ReadOnlySequence<byte> _singleSegment;
    ReadOnlySequence<byte> _multiSegment;
    FixedBufferWriter _writer = null!;
    BenchmarkEnvelope _envelope = null!;
    byte[] _serializedEnvelope = null!;

    [Params(256, 4096)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = GC.AllocateUninitializedArray<byte>(PayloadSize);
        new Random(42).NextBytes(_payload);
        _singleSegment = new ReadOnlySequence<byte>(_payload);
        _multiSegment = SequenceFactory.Create(_payload);
        _writer = new FixedBufferWriter(PayloadSize + 2048);
        _envelope = new BenchmarkEnvelope
        {
            Id = Guid.Parse("1fae5726-9f66-45cc-904f-8dd953cf003d"),
            Sequence = 42,
            Subject = "actors.fund.commands.create",
            Payload = _payload
        };
        _serializedEnvelope = _messagePack.Serialize(_envelope)!;
    }

    [Benchmark(Description = "NATS byte[] deserialize / single segment")]
    public byte[]? DeserializeSingleSegment()
        => _natsBytes.Deserialize(_singleSegment);

    [Benchmark(Description = "NATS byte[] deserialize / multi segment")]
    public byte[]? DeserializeMultiSegment()
        => _natsBytes.Deserialize(_multiSegment);

    [Benchmark(Description = "NATS byte[] serialize")]
    public int SerializeBytes()
    {
        _writer.Reset();
        _natsBytes.Serialize(_writer, _payload);
        return _writer.WrittenCount;
    }

    [Benchmark(Description = "MessagePack envelope serialize")]
    public byte[]? SerializeEnvelope()
        => _messagePack.Serialize(_envelope);

    [Benchmark(Description = "MessagePack direct-to-NATS serialize")]
    public int SerializeEnvelopeDirectToNats()
    {
        _writer.Reset();
        _directMessagePack.Serialize(_writer, _envelope);
        return _writer.WrittenCount;
    }

    [Benchmark(Description = "MessagePack envelope deserialize")]
    public BenchmarkEnvelope? DeserializeEnvelope()
        => _messagePack.Deserialize<BenchmarkEnvelope>(_serializedEnvelope);

    public sealed class BenchmarkEnvelope
    {
        public Guid Id { get; init; }
        public long Sequence { get; init; }
        public string Subject { get; init; } = string.Empty;
        public byte[] Payload { get; init; } = [];
    }

    sealed class FixedBufferWriter(int capacity) : IBufferWriter<byte>
    {
        readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(capacity);
        int _written;

        public int WrittenCount => _written;

        public void Advance(int count) => _written += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
            => _buffer.AsMemory(_written);

        public Span<byte> GetSpan(int sizeHint = 0)
            => _buffer.AsSpan(_written);

        public void Reset() => _written = 0;
    }

    static class SequenceFactory
    {
        public static ReadOnlySequence<byte> Create(byte[] data)
        {
            int midpoint = data.Length / 2;
            var first = new Segment(data.AsMemory(0, midpoint));
            var last = first.Append(data.AsMemory(midpoint));
            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public Segment Append(ReadOnlyMemory<byte> nextMemory)
            {
                var next = new Segment(nextMemory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = next;
                return next;
            }
        }
    }
}
