using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Commands;

[MessagePackObject(AllowPrivate = true)]
public sealed record ChangeTradeStrategyFamilyCommand : ICommand<ActorEntityId>
{
    [IgnoreMember] public const string Actor = "TradeStrategyFamilyCommand";
    [IgnoreMember] public const string Verb = "Change";
    [IgnoreMember] public const int ErrorId = 8062;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.TradeStrategyFamilyBoundedContext;
    [Key(6)] public ChangeTradeStrategyFamilyRequest Request { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(ChangeTradeStrategyFamilyCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => Environment.UserName;
}
