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

/// <summary>Canonical replacement for the published GetPortfolio query.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPortfolioQuery : IQuery<PortfolioReadModel>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetPortfolio";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public long? Version { get; init; } = default!;
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedOnUtc { get; init; }
    [Key(6)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetPortfolioQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="version">The Version query value.</param>
    public GetPortfolioQuery(int portfolioId, long? version)
    {
        PortfolioId = portfolioId;
        Version = version;
    }

    /// <summary>Rehydrates every serialized field in numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="portfolioId">The PortfolioId field.</param>
    /// <param name="version">The Version field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc field.</param>
    /// <param name="access">The Access field.</param>
    [SerializationConstructor]
    public GetPortfolioQuery(ActorSubject subject, ActorEntityId entityId, int portfolioId, long? version, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        PortfolioId = portfolioId;
        Version = version;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
