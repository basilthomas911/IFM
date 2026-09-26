using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Commands;

[MessagePackObject(AllowPrivate = true)]
public sealed record CreateTradeStrategyFamilyCommand : ICommand<ActorEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public CreateTradeStrategyFamilyCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="request">The Request field.</param>
    [SerializationConstructor]
    public CreateTradeStrategyFamilyCommand(Guid commandId, ActorSubject subject, bool postEvents, ActorEntityId entityId, int errorCode, BoundedContextName routeTo, CreateTradeStrategyFamilyRequest request)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Request = request;
    }
    [IgnoreMember] public const string Actor = "TradeStrategyFamilyCommand";
    [IgnoreMember] public const string Verb = "Create";
    [IgnoreMember] public const int ErrorId = 8061;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.TradeStrategyFamilyBoundedContext;
    [Key(6)] public CreateTradeStrategyFamilyRequest Request { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(CreateTradeStrategyFamilyCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => Environment.UserName;
}
