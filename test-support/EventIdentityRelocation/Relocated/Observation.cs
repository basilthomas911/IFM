using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
namespace IdentityFixture.Relocated;
// Intentionally identical payload schema with different CLR namespace and assembly.
public sealed record Observation : IEvent
{
 public ActorSubject Subject { get; init; }
 public Guid Id { get; init; }
 public long EventId { get; init; }
 public Guid CommandId { get; init; }
 public string AggregateId { get; init; } = "fixture";
 public string EventSource { get; init; } = "compatibility";
 public DateTime ReceivedOn { get; init; }
 public string UserName => "fixture";
 public string EventName => "Observation";
 public EventType EventType => EventType.DomainEvent;
 public decimal Value { get; init; }
}
