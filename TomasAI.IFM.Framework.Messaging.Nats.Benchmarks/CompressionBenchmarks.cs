using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>
/// Measures the current wire-compatible LZ4 policy against uncompressed MessagePack.
/// This is an evidence gate only; changing compression changes the wire representation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class CompressionBenchmarks
{
    static readonly MessagePackSerializerOptions UncompressedOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    readonly FixedBufferWriter _writer = new(131_072);
    SerializationBenchmarks.BenchmarkEnvelope _envelope = null!;

    [Params(256, 4096, 65536)]
    public int PayloadSize { get; set; }

    [Params(false, true)]
    public bool Compressible { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var payload = GC.AllocateUninitializedArray<byte>(PayloadSize);
        if (Compressible)
            Array.Fill(payload, (byte)7);
        else
            new Random(42).NextBytes(payload);
        _envelope = new SerializationBenchmarks.BenchmarkEnvelope
        {
            Id = Guid.Parse("b453936f-b125-42e6-945a-8c4c120ac323"),
            Sequence = 42,
            Subject = "Realtime.FuturesMarketPrice.Updated.ESZ26",
            Payload = payload
        };
    }

    [Benchmark(Baseline = true, Description = "MessagePack uncompressed direct writer")]
    public int Uncompressed()
    {
        _writer.Reset();
        MessagePackSerializer.Serialize(_writer, _envelope, UncompressedOptions);
        return _writer.WrittenCount;
    }

    [Benchmark(Description = "MessagePack LZ4 direct writer (current wire)")]
    public int Lz4()
    {
        _writer.Reset();
        MessagePackSerializer.Serialize(_writer, _envelope, MessagePackBinarySerializer.Options);
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
