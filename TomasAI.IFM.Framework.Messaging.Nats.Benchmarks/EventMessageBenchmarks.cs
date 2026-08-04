using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>
/// Compares the legacy event byte-array path with one shared pooled payload and
/// one lightweight branch per primary/routed mailbox.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class EventMessageBenchmarks
{
    readonly NatsByteArrayMessageSerializer _natsBytes = new();
    readonly NatsMessagePackSerializer<BenchmarkEvent> _eventSerializer =
        NatsMessagePackSerializer<BenchmarkEvent>.Default;

    byte[] _serializedEvent = null!;
    ReadOnlySequence<byte> _sequence;
    ActorSubject[] _destinations = null!;

    [Params(256, 4096)]
    public int PayloadSize { get; set; }

    [Params(1, 2, 5, 17)]
    public int DestinationCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var payload = GC.AllocateUninitializedArray<byte>(PayloadSize);
        new Random(42).NextBytes(payload);
        var source = new ActorSubject(
            ActorType.Event,
            "SourceEventActor",
            "Completed",
            "42");
        var @event = new BenchmarkEvent
        {
            Subject = source,
            Id = Guid.Parse("52bf878a-365d-4097-a3af-d50d562db2f7"),
            EventId = 42,
            CommandId = Guid.Parse("7e74466c-a4eb-4dac-8cb4-47af3e65f403"),
            AggregateId = "42",
            EventSource = "Benchmark",
            ReceivedOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Payload = payload
        };

        var writer = new ArrayBufferWriter<byte>();
        _eventSerializer.Serialize(writer, @event);
        _serializedEvent = writer.WrittenSpan.ToArray();
        _sequence = new ReadOnlySequence<byte>(_serializedEvent);
        _destinations = Enumerable.Range(0, DestinationCount)
            .Select(index => index == 0
                ? source
                : new ActorSubject(
                    ActorType.Event,
                    $"RoutedEventActor{index}",
                    source.Verb,
                    source.EntityId))
            .ToArray();
    }

    [Benchmark(Baseline = true, Description = "Event fan-out legacy byte[]")]
    public int LegacyByteArrayFanout()
    {
        var bytes = _natsBytes.Deserialize(_sequence)!;
        var checksum = 0;
        foreach (var destination in _destinations)
        {
            var natsMessage = new NatsMsg<byte[]>(
                destination.ToString(),
                null,
                default,
                default,
                bytes,
                default!,
                default);
            using var branch = new NatsActorMessage(natsMessage);
            checksum += branch.AsEvent<BenchmarkEvent>()!.Payload.Length;
        }
        return checksum;
    }

    [Benchmark(Description = "Event fan-out shared owned payload")]
    public int SharedOwnedFanout()
    {
        var owner = NatsMemoryOwner<byte>.Allocate(_serializedEvent.Length);
        _sequence.CopyTo(owner.Span);
        using var payload = new NatsSharedEventPayload(owner);
        var checksum = 0;
        foreach (var destination in _destinations)
        {
            using var branch = payload.CreateBranch(destination);
            checksum += branch.AsEvent<BenchmarkEvent>()!.Payload.Length;
        }
        return checksum;
    }

    [MessagePackObject]
    public sealed record BenchmarkEvent : IEvent
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public long EventId { get; init; }
        [Key(3)] public Guid CommandId { get; init; }
        [Key(4)] public string AggregateId { get; init; } = string.Empty;
        [Key(5)] public string EventSource { get; init; } = string.Empty;
        [Key(6)] public DateTime ReceivedOn { get; init; }
        [Key(7)] public byte[] Payload { get; init; } = [];
        [IgnoreMember] public string UserName => "benchmark";
        [IgnoreMember] public string EventName => nameof(BenchmarkEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    }
}
