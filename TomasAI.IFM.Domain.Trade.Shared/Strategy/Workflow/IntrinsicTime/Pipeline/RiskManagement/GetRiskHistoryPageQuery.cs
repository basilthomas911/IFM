using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
[MessagePackObject(AllowPrivate = true)]
public sealed record GetRiskHistoryPageQuery:IQuery<RiskHistoryPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetRiskHistoryPageQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="access">The Access field.</param>
    /// <param name="portfolioId">The PortfolioId field.</param>
    /// <param name="fundId">The FundId field.</param>
    /// <param name="valueDate">The ValueDate field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pagingState">The PagingState field.</param>
    [SerializationConstructor]
    public GetRiskHistoryPageQuery(ActorSubject subject, IActorEntityId entityId, CompositionQueryAccess access, int portfolioId, int fundId, DateOnly valueDate, int pageSize, string? pagingState)
    {
        Subject = subject;
        EntityId = entityId;
        Access = access;
        PortfolioId = portfolioId;
        FundId = fundId;
        ValueDate = valueDate;
        PageSize = pageSize;
        PagingState = pagingState;
    }
    [IgnoreMember] public const string Actor="RiskManagementQuery";
    [IgnoreMember] public const string Verb="GetRiskHistoryPage";
    [IgnoreMember] public const int ErrorId=23342;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public int PortfolioId {get;init;}
    [Key(4)] public int FundId {get;init;}
    [Key(5)] public DateOnly ValueDate {get;init;}
    [Key(6)] public int PageSize {get;init;}=25;
    [Key(7)] public string? PagingState {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
