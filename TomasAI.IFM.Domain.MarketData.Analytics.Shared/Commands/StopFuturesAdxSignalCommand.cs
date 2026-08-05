using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

[MessagePackObject(AllowPrivate = true)]
public record StopFuturesAdxSignalCommand : ICommand<FuturesAdxSignalEntityId>
{
    public const string Actor = "FuturesAdxSignalCommand";
    public const string Verb = "Stop";
    public const int ErrorId = 20004;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesAdxSignalEntityId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
    public StopFuturesAdxSignalCommand() { }
    public StopFuturesAdxSignalCommand(FuturesAdxSignalEntityId entityId)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesAdxSignalBoundedContext;
    }
}
