using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Versioned Fund-owned replacement for the published GetFund query.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundQuery : IQuery<FundMandateReadModel>
{
    public const string Actor = PortfolioQueryRoutes.Fund;
    public const string Verb = "GetFund";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public long? Version { get; init; }
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedOnUtc { get; init; }
    [Key(7)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Creates an empty query for serialization.</summary>
    public GetFundQuery() { }

    /// <summary>Creates a Fund projection query.</summary>
    /// <param name="portfolioId">The Portfolio identifier.</param>
    /// <param name="fundId">The Fund identifier.</param>
    /// <param name="version">The optional mandate version.</param>
    public GetFundQuery(int portfolioId, int fundId, long? version)
    {
        PortfolioId = portfolioId;
        FundId = fundId;
        Version = version;
    }

    /// <summary>Rehydrates every serialized field in permanent numeric-key order.</summary>
    /// <param name="subject">The actor subject.</param>
    /// <param name="entityId">The query entity key.</param>
    /// <param name="portfolioId">The Portfolio identifier.</param>
    /// <param name="fundId">The Fund identifier.</param>
    /// <param name="version">The optional mandate version.</param>
    /// <param name="correlationId">The request correlation identifier.</param>
    /// <param name="requestedOnUtc">The UTC request timestamp.</param>
    /// <param name="access">The requesting principal and roles.</param>
    [SerializationConstructor]
    public GetFundQuery(ActorSubject subject, ActorEntityId entityId, int portfolioId, int fundId,
        long? version, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        PortfolioId = portfolioId;
        FundId = fundId;
        Version = version;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
