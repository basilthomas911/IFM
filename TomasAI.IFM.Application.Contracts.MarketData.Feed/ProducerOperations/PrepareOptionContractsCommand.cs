using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;

/// <summary>Prepares option contract inputs before dispatching a catalog command.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareOptionContractsCommand : ICommand<ActorEntityId>
{
    public const string Actor = "OptionContractPreparationFunction";
    public const string Verb = "PrepareOptionContracts";
    public const int ErrorId = 6011;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesOptionContractBoundedContext;
    [Key(6)] public FuturesOptionContractReadModel[] Contracts { get; init; } = [];
    [Key(7)] public string? OriginalContractId { get; init; }
    [IgnoreMember] public string CommandName => nameof(PrepareOptionContractsCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => string.Empty;

    public static PrepareOptionContractsCommand ForOperation(Guid operation)
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
