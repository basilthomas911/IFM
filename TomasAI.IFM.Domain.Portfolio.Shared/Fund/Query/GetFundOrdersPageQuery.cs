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

/// <summary>Represents the GetFundOrdersPageQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundOrdersPageQuery : IQuery<PortfolioPage<FundOrderProjectionReadModel>>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetFundOrdersPage";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public int FundId { get; init; } = default!;
    [Key(4)] public DateOnly OrderMonth { get; init; } = default!;
    [Key(5)] public int PageSize { get; init; } = default!;
    [Key(6)] public string? PageToken { get; init; } = default!;
    [Key(7)] public Guid CorrelationId { get; init; }
    [Key(8)] public DateTime RequestedOnUtc { get; init; }
    [Key(9)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundOrdersPageQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="fundId">The FundId query value.</param>
    /// <param name="orderMonth">The OrderMonth query value.</param>
    /// <param name="pageSize">The PageSize query value.</param>
    /// <param name="pageToken">The PageToken query value.</param>
    public GetFundOrdersPageQuery(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken)
    {
        PortfolioId = portfolioId;
        FundId = fundId;
        OrderMonth = orderMonth;
        PageSize = pageSize;
        PageToken = pageToken;
    }
}
