using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

public abstract record StrategyCatalogLifecycleCommand : ICommand<ActorEntityId>
{
    public const string Actor = "StrategyCatalogCommand";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = StrategyCatalogCommand.ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.TradeStrategyFamilyBoundedContext;
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => Environment.UserName;

    public static ActorEntityId Address(CatalogKey key) => new($"{(int)key.Kind}-{key.Id:N}-{key.Version}");
}
