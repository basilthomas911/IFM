using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ActorHealthJsonBenchmarks
{
    readonly JsonSerializerOptions reflectionOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    readonly SupervisorRuntimeSnapshot snapshot = CreateSnapshot();

    [Benchmark(Baseline = true)]
    public string ReflectionMetadata()
        => JsonSerializer.Serialize(snapshot, reflectionOptions);

    [Benchmark]
    public string SourceGeneratedMetadata()
        => JsonSerializer.Serialize(
            snapshot,
            ApiServerJsonContext.Default.SupervisorRuntimeSnapshot);

    internal static SupervisorRuntimeSnapshot CreateSnapshot()
    {
        var observedUtc = new DateTime(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc);
        var actors = Enumerable.Range(0, 64)
            .Select(index => CreateActor(index, observedUtc))
            .ToArray();
        return new SupervisorRuntimeSnapshot(
            observedUtc,
            SupervisorActorHealthStatus.Green,
            actors.Length,
            actors.Length,
            0,
            0,
            actors,
            [],
            [],
            []);
    }

    static SupervisorActorSnapshot CreateActor(int index, DateTime observedUtc)
    {
        var actorType = index % 2 == 0 ? ActorType.Command : ActorType.Query;
        var actorName = $"PerformanceActor{index}";
        var mailboxes = Enumerable.Range(0, 4)
            .Select(entity => CreateMailbox(actorType, actorName, entity, observedUtc))
            .ToArray();
        return new SupervisorActorSnapshot(
            new ActorMailboxId(actorType, actorName),
            "TomasAI.IFM.Performance",
            $"TomasAI.IFM.Performance.{actorName}",
            true,
            SupervisorActorLifecycleState.Running,
            1,
            SupervisorActorHealthStatus.Green,
            0,
            10_000,
            10_000,
            10_000,
            0,
            0,
            0,
            0,
            mailboxes);
    }

    static ActorMailboxMetricsSnapshot CreateMailbox(
        ActorType actorType,
        string actorName,
        int entity,
        DateTime observedUtc)
        => new(
            new ActorThreadId(actorType, actorName, entity.ToString()),
            0,
            2_500,
            2_500,
            2_500,
            0,
            0,
            0,
            0,
            true,
            ActorMailboxLifecycleState.Running,
            1,
            false,
            string.Empty,
            observedUtc,
            observedUtc,
            observedUtc,
            null,
            string.Empty,
            string.Empty,
            10_000);
}
