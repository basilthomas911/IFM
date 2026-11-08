using System.Buffers;
using BenchmarkDotNet.Attributes;
using NATS.Client.Core;
using TomasAI.IFM.Application.Api.Server.Benchmarks;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Qualification of the existing pooled default against the retained byte-array compatibility boundary; no new transport speedup is implied.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "BenchmarkDotNet invokes this public entry point from a generated assembly.")]
public class ApiServerPerformanceReceiveQualificationBenchmarks
{
    [Params(256, 4096, 65536, 950000)] public int Bytes { get; set; }
    ReadOnlySequence<byte> input;
    readonly NatsByteArrayMessageSerializer legacy = new();

    [GlobalSetup]
    public void Setup()
    {
        var bytes = new byte[Bytes];
        for (var index = 0; index < bytes.Length; index++) bytes[index] = (byte)(index % 251);
        var first = new Segment(bytes.AsMemory(0, bytes.Length / 2));
        var last = first.Append(bytes.AsMemory(bytes.Length / 2));
        input = new(first, 0, last, last.Memory.Length);
        using var owned = NatsDefaultSerializer<NatsMemoryOwner<byte>>.Default.Deserialize(input);
        if (!owned.Memory.Span.SequenceEqual(legacy.Deserialize(input))) throw new InvalidOperationException("Receive contents differ.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("05-ReceiveQualification")]
    public int LegacyCompatibilityArray()
    {
        var bytes = legacy.Deserialize(input)!;
        return bytes.Length;
    }

    [Benchmark, BenchmarkCategory("05-ReceiveQualification")]
    public int ExistingPooledDefault()
    {
        using var owner = NatsDefaultSerializer<NatsMemoryOwner<byte>>.Default.Deserialize(input);
        return owner.Memory.Length;
    }

    sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) { Memory = memory; }
        public Segment Append(ReadOnlyMemory<byte> value)
        {
            var next = new Segment(value) { RunningIndex = RunningIndex + Memory.Length };
            Next = next; return next;
        }
    }
}
