using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

[MessagePackObject(AllowPrivate = true)]
public record StopFuturesAtrSignalCommand : ICommand<FuturesAtrSignalEntityId>
{

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    [SerializationConstructor]
    public StopFuturesAtrSignalCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesAtrSignalEntityId entityId, int errorCode, BoundedContextName routeTo)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
    }
    public const string Actor = "FuturesAtrSignalCommand";
    public const string Verb = "Stop";
    public const int ErrorId = 20004;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesAtrSignalEntityId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
    public StopFuturesAtrSignalCommand() { }
    public StopFuturesAtrSignalCommand(FuturesAtrSignalEntityId entityId)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesAtrSignalBoundedContext;
    }
}
