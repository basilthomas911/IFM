using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Seeds one EMA aggregate with at most 64 ordered original observations.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SeedFuturesEmaSignalsCommand : ICommand<FuturesTradeSessionBarEntityId>
{
    public SeedFuturesEmaSignalsCommand() { }
    [SerializationConstructor]
    public SeedFuturesEmaSignalsCommand(Guid commandId, ActorSubject subject, bool postEvents,
        FuturesTradeSessionBarEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesTradeSessionBarReadModel[] observations)
    { CommandId = commandId; Subject = subject; PostEvents = postEvents; EntityId = entityId; ErrorCode = errorCode; RouteTo = routeTo; Observations = observations; }

    public const string Actor = GenerateFuturesEmaSignalCommand.Actor;
    public const string Verb = "SeedBatch";
    public const int ErrorId = GenerateFuturesEmaSignalCommand.ErrorId;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesEmaSignalBoundedContext;
    /// <summary>Immutable producer-prepared bars for exactly one series and timeframe, in increasing interval order.</summary>
    [Key(6)] public FuturesTradeSessionBarReadModel[] Observations { get; init; } = [];
    [IgnoreMember] public string CommandName => nameof(SeedFuturesEmaSignalsCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
