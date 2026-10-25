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

/// <summary>Represents the GetPortfolioFinancialPolicyQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPortfolioFinancialPolicyQuery : IQuery<PortfolioFinancialPolicyReadModel>
{
    public const string Actor = PortfolioQueryRoutes.FinancialPolicy;
    public const string Verb = "GetPortfolioFinancialPolicy";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PolicyId { get; init; } = default!;
    [Key(3)] public long? PolicyVersion { get; init; } = default!;
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedOnUtc { get; init; }
    [Key(6)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetPortfolioFinancialPolicyQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="policyId">The PolicyId query value.</param>
    /// <param name="policyVersion">The PolicyVersion query value.</param>
    public GetPortfolioFinancialPolicyQuery(int policyId, long? policyVersion)
    {
        PolicyId = policyId;
        PolicyVersion = policyVersion;
    }

    /// <summary>Rehydrates every serialized field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject wire value.</param>
    /// <param name="entityId">The EntityId wire value.</param>
    /// <param name="policyId">The PolicyId wire value.</param>
    /// <param name="policyVersion">The PolicyVersion wire value.</param>
    /// <param name="correlationId">The CorrelationId wire value.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc wire value.</param>
    /// <param name="access">The Access wire value.</param>
    [SerializationConstructor]
    public GetPortfolioFinancialPolicyQuery(ActorSubject subject, ActorEntityId entityId, int policyId, long? policyVersion, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
