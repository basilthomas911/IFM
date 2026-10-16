using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests.NatsJSDurableQueue;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]

public static class SampleData
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA5394",Justification="The fixed seed produces reproducible benchmark payload bytes, not security material.")]
    public static SampleEvent Event(string value = "event") => new()
    {
        Id = Guid.NewGuid(),
        EventId = Random.Shared.NextInt64(1, long.MaxValue),
        CommandId = Guid.NewGuid(),
        AggregateId = $"aggregate-{value}",
        EventSource = "unit-tests",
        ReceivedOn = DateTime.UtcNow,
        Value = value
    };
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]

public sealed class SampleEvent : IEvent
{
    public ActorSubject Subject { get; init; } = default!;
    public Guid Id { get; init; }
    public long EventId { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = string.Empty;
    public DateTime ReceivedOn { get; init; }
    public string UserName { get; init; } = "unit-test";
    public string EventName => nameof(SampleEvent);
    public EventType EventType => EventType.DomainEvent;
    public string Value { get; init; } = string.Empty;
}
