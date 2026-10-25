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

/// <summary>Represents the GetFundOrderTradesPageQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundOrderTradesPageQuery : IQuery<PortfolioPage<FundOrderTradeProjectionReadModel>>
{
    public const string Actor = PortfolioQueryRoutes.Fund;
    public const string Verb = "GetFundOrderTradesPage";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int OrderId { get; init; } = default!;
    [Key(3)] public int PageSize { get; init; } = default!;
    [Key(4)] public string? PageToken { get; init; } = default!;
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedOnUtc { get; init; }
    [Key(7)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundOrderTradesPageQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="orderId">The OrderId query value.</param>
    /// <param name="pageSize">The PageSize query value.</param>
    /// <param name="pageToken">The PageToken query value.</param>
    public GetFundOrderTradesPageQuery(int orderId, int pageSize, string? pageToken)
    {
        OrderId = orderId;
        PageSize = pageSize;
        PageToken = pageToken;
    }

    /// <summary>Rehydrates every serialized field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject wire value.</param>
    /// <param name="entityId">The EntityId wire value.</param>
    /// <param name="orderId">The OrderId wire value.</param>
    /// <param name="pageSize">The PageSize wire value.</param>
    /// <param name="pageToken">The PageToken wire value.</param>
    /// <param name="correlationId">The CorrelationId wire value.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc wire value.</param>
    /// <param name="access">The Access wire value.</param>
    [SerializationConstructor]
    public GetFundOrderTradesPageQuery(ActorSubject subject, ActorEntityId entityId, int orderId, int pageSize, string? pageToken, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        OrderId = orderId;
        PageSize = pageSize;
        PageToken = pageToken;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
