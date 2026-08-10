using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Application.Benchmarks;

/// <summary>
/// CPU/allocation lower bound for the pre-SWO-06 recovery shape. Fake storage and queue operations complete
/// synchronously, so the benchmark deliberately excludes PostgreSQL and NATS latency while retaining full-set
/// materialization, per-event deserialization, state reload, state write, and queue-call orchestration.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class EventProjectorRecoveryBaselineBenchmarks
{
    const string ProjectorName = "ApplicationEventProjector";
    EventLogReadModel[] _eventLogs = null!;
    Dictionary<long, EventProjectorStateReadModel> _states = null!;
    EventProjectorRecoveryItemReadModel[] _recoveryItems = null!;
    int _enqueued;

    [Params(1_000, 10_000, 100_000)]
    public int PendingEvents { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sourceEvent = new ApplicationStartupEvent
        {
            Id = Guid.Parse("2b528886-38bc-46c7-954d-3a3fcce35f36"),
            CommandId = Guid.Parse("ba9206c8-3504-4e48-965e-754e07f3a21b"),
            AggregateId = "benchmark",
            EventSource = "EventProjectorRecoveryBaseline",
            ReceivedOn = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
            CreatedOn = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
            CreatedBy = "benchmark"
        };
        var eventType = typeof(ApplicationStartupEvent).AssemblyQualifiedName!;
        var eventData = sourceEvent.ToEventData();
        _eventLogs = new EventLogReadModel[PendingEvents];
        _recoveryItems = new EventProjectorRecoveryItemReadModel[PendingEvents];
        _states = new Dictionary<long, EventProjectorStateReadModel>(PendingEvents);

        for (var index = 0; index < PendingEvents; index++)
        {
            var eventId = index + 1L;
            _eventLogs[index] = new EventLogReadModel(
                EventStreamId: (index % 256) + 1L,
                EventName: nameof(ApplicationStartupEvent),
                EventTypeName: eventType,
                EventVersion: eventId,
                EventData: eventData,
                CommandId: sourceEvent.CommandId,
                EventTimestamp: "2026-08-09T12:00:00.0000000Z");
            _states.Add(eventId, new EventProjectorStateReadModel(
                eventId,
                ApplicationStartupEvent.Actor,
                ProjectorName,
                isReplay: true,
                attemptNumber: 1,
                EventProjectorOutcomeType.Retrying,
                EventProjectorStageType.ApplyProjection));
            _recoveryItems[index] = new EventProjectorRecoveryItemReadModel(
                _eventLogs[index],
                new EventProjectorExecutionStateReadModel(
                    eventId,
                    ApplicationStartupEvent.Actor,
                    ProjectorName,
                    true,
                    1,
                    EventProjectorOutcomeType.Retrying,
                    EventProjectorStageType.ApplyProjection,
                    string.Empty,
                    sourceEvent.CreatedOn,
                    sourceEvent.CreatedOn,
                    _eventLogs[index].EventStreamId,
                    nameof(ApplicationStartupEvent),
                    0,
                    null,
                    null,
                    0,
                    null,
                    null,
                    string.Empty,
                    EventProjectorStageType.PublishProcessingEvent,
                    sourceEvent.CreatedOn));
        }
    }

    [IterationSetup]
    public void Reset() => _enqueued = 0;

    [Benchmark(Baseline = true)]
    public async Task<int> CurrentFullSetNPlusOneRecovery()
    {
        IReadOnlyCollection<EventLogReadModel> eventLogs = [.. _eventLogs];
        foreach (var eventLog in eventLogs)
        {
            var domainEvent = eventLog.ToDomainEvent();
            var currentState = await GetStateAsync(eventLog.EventVersion);
            if (currentState is null)
                continue;

            await PersistStateAsync(currentState);
            await EnqueueAsync(domainEvent);
        }

        return _enqueued;
    }

    [Benchmark]
    public async Task<int> BoundedJoinedKeysetRecovery()
    {
        const int batchSize = 256;
        for (var offset = 0; offset < _recoveryItems.Length; offset += batchSize)
        {
            var count = Math.Min(batchSize, _recoveryItems.Length - offset);
            var page = new EventProjectorRecoveryItemReadModel[count];
            Array.Copy(_recoveryItems, offset, page, 0, count);
            var streamGroups = page
                .GroupBy(item => item.State.EventStreamId)
                .Select(group => group.ToArray())
                .ToArray();
            await Parallel.ForEachAsync(
                streamGroups,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (stream, cancellationToken) =>
                {
                    foreach (var item in stream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var claimed = await ClaimJoinedStateAsync(item.State);
                        if (claimed is null)
                            continue;
                        var domainEvent = item.EventLog.ToDomainEvent();
                        _ = domainEvent;
                        Interlocked.Increment(ref _enqueued);
                    }
                });
        }

        return _enqueued;
    }

    ValueTask<EventProjectorStateReadModel?> GetStateAsync(long eventId)
        => ValueTask.FromResult(_states.GetValueOrDefault(eventId));

    static ValueTask PersistStateAsync(EventProjectorStateReadModel _) => ValueTask.CompletedTask;

    static ValueTask<EventProjectorExecutionStateReadModel?> ClaimJoinedStateAsync(
        EventProjectorExecutionStateReadModel state)
        => ValueTask.FromResult<EventProjectorExecutionStateReadModel?>(state);

    ValueTask EnqueueAsync(IEvent _)
    {
        _enqueued++;
        return ValueTask.CompletedTask;
    }
}
