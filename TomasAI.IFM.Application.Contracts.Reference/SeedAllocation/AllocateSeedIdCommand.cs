using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;

/// <summary>Allocates one seed identifier for a stable operation identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record AllocateSeedIdCommand : ICommand<ActorEntityId>
{
    public const string Actor = "SeedAllocationFunction";
    public const string Verb = "AllocateSeedId";
    public const int ErrorId = 1023;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.LookupTypeBoundedContext;
    [Key(6)] public string SeedType { get; init; } = string.Empty;
    [IgnoreMember] public string CommandName => nameof(AllocateSeedIdCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => string.Empty;
}
