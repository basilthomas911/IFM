using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

/// <summary>Carries one PrepareFinancialAuthority request under a stable operation identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareFinancialAuthorityCommand : ICommand<ActorEntityId>
{
    public const string Actor = "FinancialAuthorityPreparationFunction";
    public const string Verb = "PrepareFinancialAuthority";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public PrepareFinancialAuthorityRequest Parameters { get; init; } = default!;
    [Key(7)] public FinancialReadScope Scope { get; init; } = default!;
    [IgnoreMember] public string CommandName => nameof(PrepareFinancialAuthorityCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => string.Empty;

    public static PrepareFinancialAuthorityCommand ForOperation(Guid operation)
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
