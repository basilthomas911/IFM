using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Measures provenance preservation separately from transport and payload serialization.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class MessageOriginCopyBenchmarks
{
    CopyCommand source = null!;

    [GlobalSetup]
    public void Setup()
    {
        source = new CopyCommand { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, "OriginBenchmark", "Run", "one") };
        MessageOrigins.Capture(source, TimeProvider.System);
    }

    [Benchmark(Baseline = true)]
    public object BeforeRecordCopy() => source with { PostEvents = false };

    [Benchmark]
    public object AfterForwardedCopy() => MessageOrigins.Copy(source, source with { PostEvents = false });

    [Benchmark]
    public object AfterProducerPreparation() => MessageOrigins.CopyCommandIfSameIdentity(source,
        source with { PostEvents = false });

    sealed record CopyCommand : ICommand
    {
        public ActorSubject Subject { get; init; }
        public Guid CommandId { get; init; }
        public bool PostEvents { get; init; } = true;
        public string CommandName => nameof(CopyCommand);
        public BoundedContextName RouteTo => default;
        public string StreamId => Subject.StreamId;
        public string EventSource => "Benchmark";
        public int ErrorCode => 0;
    }
}
