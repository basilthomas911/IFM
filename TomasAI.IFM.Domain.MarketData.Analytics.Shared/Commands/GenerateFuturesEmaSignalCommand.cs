using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests event-sourced EMA10/20/50/200 calculation for one closed bar.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GenerateFuturesEmaSignalCommand : ICommand<FuturesTradeSessionBarEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public GenerateFuturesEmaSignalCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="observation">The Observation field.</param>
    [SerializationConstructor]
    public GenerateFuturesEmaSignalCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesTradeSessionBarEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesTradeSessionBarReadModel observation)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Observation = observation;
    }
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
