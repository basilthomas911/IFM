using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Canonical replacement for the published GetPortfolios query.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPortfoliosQuery : IQuery<PortfolioPage<PortfolioReadModel>>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetPortfolios";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int? State { get; init; } = default!;
    [Key(3)] public int PageSize { get; init; } = default!;
    [Key(4)] public string? PageToken { get; init; } = default!;
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedOnUtc { get; init; }
    [Key(7)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetPortfoliosQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="state">The State query value.</param>
    /// <param name="pageSize">The PageSize query value.</param>
    /// <param name="pageToken">The PageToken query value.</param>
    public GetPortfoliosQuery(int? state, int pageSize, string? pageToken)
    {
        State = state;
        PageSize = pageSize;
        PageToken = pageToken;
    }

    /// <summary>Rehydrates every serialized field in numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="state">The State field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pageToken">The PageToken field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc field.</param>
    /// <param name="access">The Access field.</param>
    [SerializationConstructor]
    public GetPortfoliosQuery(ActorSubject subject, ActorEntityId entityId, int? state, int pageSize, string? pageToken, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        State = state;
        PageSize = pageSize;
        PageToken = pageToken;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
