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

/// <summary>Represents the GetPortfolioFundStrategyReferenceCombinationsQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPortfolioFundStrategyReferenceCombinationsQuery : IQuery<PortfolioFundStrategyReferenceCombination[]>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetPortfolioFundStrategyReferenceCombinations";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public DateTime AsOfUtc { get; init; } = default!;
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedOnUtc { get; init; }
    [Key(6)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetPortfolioFundStrategyReferenceCombinationsQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="asOfUtc">The AsOfUtc query value.</param>
    public GetPortfolioFundStrategyReferenceCombinationsQuery(int portfolioId, DateTime asOfUtc)
    {
        PortfolioId = portfolioId;
        AsOfUtc = asOfUtc;
    }
}
