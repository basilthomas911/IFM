using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioOrderCompositionFailedEvent : IErrorEvent<FinancialExecutionId>
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public DateTime ErrorDate { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = 34130;
    [Key(6)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(7)] public ErrorType ErrorType { get; init; }
    [Key(8)] public string ErrorData { get; init; } = string.Empty;
    [Key(9)] public string CommandName { get; init; } = string.Empty;
    [Key(10)] public string CommandData { get; init; } = string.Empty;
    [Key(11)] public long EventId { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string EventSource { get; init; } = EvaluatePortfolioOrderCompositionCommand.Actor;
    [Key(14)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => "Portfolio";
    [IgnoreMember] public string EventName => nameof(PortfolioOrderCompositionFailedEvent);
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
