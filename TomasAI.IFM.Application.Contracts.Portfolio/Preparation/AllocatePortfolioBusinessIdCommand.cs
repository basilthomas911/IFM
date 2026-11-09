using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

/// <summary>Carries one AllocatePortfolioBusinessId request under a stable operation identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record AllocatePortfolioBusinessIdCommand : ICommand<ActorEntityId>
{
    public const string Actor = "PortfolioBusinessIdAllocationFunction";
    public const string Verb = "AllocatePortfolioBusinessId";
    public const int ErrorId = PortfolioErrorCodes.SequenceAllocationFailed;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public PortfolioBusinessIdentityKind Kind { get; init; }
    [Key(7)] public PortfolioAccessContext Access { get; init; } = default!;
    [IgnoreMember] public string CommandName => nameof(AllocatePortfolioBusinessIdCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => string.Empty;

    public static AllocatePortfolioBusinessIdCommand ForOperation(Guid operation)
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
