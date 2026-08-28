using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests event-sourced EMA10/20/50/200 calculation for one closed bar.</summary>
[MessagePackObject]
public sealed record GenerateFuturesEmaSignalCommand : ICommand<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the command actor name.</summary>
    public const string Actor = "FuturesEmaSignalCommand";
    /// <summary>Gets the command verb.</summary>
    public const string Verb = "Generate";
    /// <summary>Gets the stable error code.</summary>
    public const int ErrorId = 26100;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesEmaSignalBoundedContext;
    /// <summary>Gets the immutable source bar.</summary>
    [Key(6)] public FuturesTradeSessionBarReadModel Observation { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(GenerateFuturesEmaSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
