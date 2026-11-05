using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;

/// <summary>Prepares the current market session for a stable start operation.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareMarketDataFeedSessionCommand : ICommand<ActorEntityId>
{
    public const string Actor = "FeedSessionPreparationFunction";
    public const string Verb = "PrepareMarketDataFeedSession";
    public const int ErrorId = 5005;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketDataFeedBoundedContext;
    [IgnoreMember] public string CommandName => nameof(PrepareMarketDataFeedSessionCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => string.Empty;

    public static PrepareMarketDataFeedSessionCommand ForOperation(Guid operation)
    {
        var entityId = new ActorEntityId(operation.ToString("N"));
        return new()
        {
            CommandId = operation,
            EntityId = entityId,
            Subject = new(ActorType.Function, Actor, Verb, entityId.Format())
        };
    }
}
