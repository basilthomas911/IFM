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

/// <summary>Represents the GetFundQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundQuery : IQuery<FundMandateReadModel>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetFund";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public int FundId { get; init; } = default!;
    [Key(4)] public long? Version { get; init; } = default!;
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedOnUtc { get; init; }
    [Key(7)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="fundId">The FundId query value.</param>
    /// <param name="version">The Version query value.</param>
    public GetFundQuery(int portfolioId, int fundId, long? version)
    {
        PortfolioId = portfolioId;
        FundId = fundId;
        Version = version;
    }
}
