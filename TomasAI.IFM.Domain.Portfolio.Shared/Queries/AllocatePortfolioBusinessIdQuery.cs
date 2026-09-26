using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Canonical replacement for the published AllocatePortfolioBusinessId query.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record AllocatePortfolioBusinessIdQuery : IQuery<PortfolioBusinessIdAllocation>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "AllocatePortfolioBusinessId";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public PortfolioBusinessIdentityKind Kind { get; init; } = default!;
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public DateTime RequestedOnUtc { get; init; }
    [Key(5)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public AllocatePortfolioBusinessIdQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="kind">The Kind query value.</param>
    public AllocatePortfolioBusinessIdQuery(PortfolioBusinessIdentityKind kind)
    {
        Kind = kind;
    }

    /// <summary>Rehydrates every serialized field in numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="kind">The Kind field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc field.</param>
    /// <param name="access">The Access field.</param>
    [SerializationConstructor]
    public AllocatePortfolioBusinessIdQuery(ActorSubject subject, ActorEntityId entityId, PortfolioBusinessIdentityKind kind, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        Kind = kind;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
