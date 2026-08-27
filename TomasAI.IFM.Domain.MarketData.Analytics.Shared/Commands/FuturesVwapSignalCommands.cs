using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests one event-sourced live futures trade contribution.</summary>
[MessagePackObject]
public sealed record UpdateFuturesVwapSignalCommand : ICommand<FuturesVwapSignalEntityId>
{
    public const string Actor = "FuturesVwapSignalCommand";
    public const string Verb = "Update";
    public const int ErrorId = 26400;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesVwapSignalBoundedContext;
    [Key(6)] public FuturesVwapTradeObservation Observation { get; init; } = new();
    [Key(7)] public FuturesVwapConfiguration Configuration { get; init; } = FuturesVwapConfiguration.Standard;
    [IgnoreMember] public string CommandName => nameof(UpdateFuturesVwapSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}

/// <summary>Requests a bounded ordered exact-trade recovery batch.</summary>
[MessagePackObject]
public sealed record RecoverFuturesVwapSignalCommand : ICommand<FuturesVwapSignalEntityId>
{
    public const string Verb = "Recover";
    public const int ErrorId = 26401;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesVwapSignalBoundedContext;
    [Key(6)] public Guid RecoveryGenerationId { get; init; }
    [Key(7)] public long BatchOrdinal { get; init; }
    [Key(8)] public bool IsFirstBatch { get; init; }
    [Key(9)] public bool IsFinalBatch { get; init; }
    [Key(10)] public FuturesVwapTradeObservation[] Trades { get; init; } = [];
    [Key(11)] public FuturesVwapConfiguration Configuration { get; init; } = FuturesVwapConfiguration.Standard;
    [IgnoreMember] public string CommandName => nameof(RecoverFuturesVwapSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => UpdateFuturesVwapSignalCommand.Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
