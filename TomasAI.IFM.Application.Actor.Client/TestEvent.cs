using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Actor;

public record TestEvent(string MsgString)
    : IEvent<TestId>
{
    public ActorSubject Subject { get; init; } 
    public Guid Id { get; init; } = Guid.NewGuid();
    public TestId EntityId { get; init; } 
    public long EventId { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } 
    public string EventSource { get; init; } = string.Empty;
    public DateTime ReceivedOn { get; init; }
    public FuturesContractV2ReadModel Contract { get; init; } = default!;
    public DateTime CreatedOn { get; init; } = DateTime.Now;
    public string CreatedBy { get; init; } = string.Empty;

    public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    public string EventName => nameof(TestEvent);
    public EventType EventType => EventType.DomainEvent;
}
