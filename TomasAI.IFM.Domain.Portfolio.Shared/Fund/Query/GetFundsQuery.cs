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

/// <summary>Represents the GetFundsQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundsQuery : IQuery<PortfolioPage<FundMandateReadModel>>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetFunds";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public int? State { get; init; } = default!;
    [Key(4)] public int PageSize { get; init; } = default!;
    [Key(5)] public string? PageToken { get; init; } = default!;
    [Key(6)] public Guid CorrelationId { get; init; }
    [Key(7)] public DateTime RequestedOnUtc { get; init; }
    [Key(8)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundsQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="state">The State query value.</param>
    /// <param name="pageSize">The PageSize query value.</param>
    /// <param name="pageToken">The PageToken query value.</param>
    public GetFundsQuery(int portfolioId, int? state, int pageSize, string? pageToken)
    {
        PortfolioId = portfolioId;
        State = state;
        PageSize = pageSize;
        PageToken = pageToken;
    }
}
