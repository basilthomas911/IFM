using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Captures opening execution prices, accepted capital and ledger cash for one monitoring initialization.</summary>
[MessagePackObject]
public sealed record IronCondorMonitoringInitialization
{
    [Key(0)] public EstablishedTradeDefinition IronCondorTrade { get; init; } = new();
    [Key(1)] public decimal FundAvailableCash { get; init; }
    [Key(2)] public long FundFinancialRevision { get; init; }
    [Key(3)] public DateTime FundCashAsOfUtc { get; init; }
    [Key(4)] public decimal RequiredCapital { get; init; }
    [Key(5)] public int TradeOrderRevision { get; init; }
    [Key(6)] public DateOnly ValueDate { get; init; }
    [Key(7)] public DateTime InitializedAtUtc { get; init; }
}

/// <summary>Initializes legacy monitoring limits through the authoritative position command owner.</summary>
[MessagePackObject]
public sealed record InitializeIronCondorMonitoringCommand : ICommand<StrategyPositionId>
{
    public const string Verb = "InitializeIronCondorMonitoring";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [Key(4)] public IronCondorMonitoringInitialization MonitoringInitialization { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(InitializeIronCondorMonitoringCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25107;
}

/// <summary>Persists the accepted limit calculation and its financial provenance; projection populates the legacy tables.</summary>
[MessagePackObject]
public sealed record IronCondorMonitoringInitializedEvent : IEvent<StrategyPositionId>
{
    public const string Verb = "IronCondorMonitoringInitialized";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public StrategyPositionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public IronCondorMonitoringInitialization MonitoringInitialization { get; init; } = new();
    [Key(9)] public TradeLimitReadModel TradeLimits { get; init; } = new();
    [Key(10)] public TradeTypeLimitReadModel[] SpreadLimits { get; init; } = [];
    [IgnoreMember] public string UserName => "IronCondorMonitoring";
    [IgnoreMember] public string EventName => nameof(IronCondorMonitoringInitializedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
