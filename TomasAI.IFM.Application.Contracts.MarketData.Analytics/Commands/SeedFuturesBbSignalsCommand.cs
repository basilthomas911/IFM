using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Repairs a cold Bollinger accumulator from a complete, producer-prepared original history window.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SeedFuturesBbSignalsCommand : ICommand<FuturesTradeSessionBarEntityId>
{
    public SeedFuturesBbSignalsCommand() { }
    [SerializationConstructor]
    public SeedFuturesBbSignalsCommand(Guid commandId, ActorSubject subject, bool postEvents,
        FuturesTradeSessionBarEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesTradeSessionBarReadModel[] observations)
    { CommandId = commandId; Subject = subject; PostEvents = postEvents; EntityId = entityId; ErrorCode = errorCode; RouteTo = routeTo; Observations = observations; }

    public const string Actor = GenerateFuturesBbSignalCommand.Actor;
    public const string Verb = "SeedBatch";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = GenerateFuturesBbSignalCommand.ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesBbSignalBoundedContext;
    /// <summary>At least forty ordered bars are required for price variance and the prior-width baseline.</summary>
    [Key(6)] public FuturesTradeSessionBarReadModel[] Observations { get; init; } = [];
    [IgnoreMember] public string CommandName => nameof(SeedFuturesBbSignalsCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
