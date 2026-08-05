using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class EventPublicationBenchmarks
{
    DatabaseBackupEvent _event = default!;

    [GlobalSetup]
    public void Setup()
    {
        var entityId = new DatabaseBackupId(DatabaseBackupNames.EventDb);
        _event = new DatabaseBackupEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                DatabaseBackupEvent.Actor,
                DatabaseBackupEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            CommandId = Guid.NewGuid()
        };
    }

    [Benchmark(Baseline = true)]
    public ActorSubject BeforeRebuildRoute()
        => new(
            ActorType.Event,
            _event.Subject.Name.Replace("Denormalizer", "Event", StringComparison.Ordinal),
            _event.Subject.Verb,
            _event.EntityId.Format());

    [Benchmark]
    public ActorSubject AfterUseExistingRoute() => _event.Subject;
}
