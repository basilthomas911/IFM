using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
[MessagePackObject]
public sealed record SelectionQueryAccess([property:Key(0)] string Principal,[property:Key(1)] string[] Roles);
[MessagePackObject]
public sealed record TradeSelectionProjection([property:Key(0)] TradeSelectionFunctionCompletedEvent Completion,[property:Key(1)] bool WorkflowAccepted,[property:Key(2)] bool AcceptanceUnknown,[property:Key(3)] bool SuspectedOrphan);
[MessagePackObject]
public sealed record TradeSelectionHistoryRow([property:Key(0)] int PortfolioId,[property:Key(1)] int FundId,[property:Key(2)] DateOnly ValueDate,[property:Key(3)] DateTime OccurredAtUtc,[property:Key(4)] Guid WorkflowId,[property:Key(5)] Guid InvocationId,[property:Key(6)] Guid EventId,[property:Key(7)] short TargetHorizon,[property:Key(8)] byte Outcome,[property:Key(9)] string ReasonCode,[property:Key(10)] Guid ResultId,[property:Key(11)] string ResultSha256);
[MessagePackObject]
public sealed record TradeSelectionHistoryPage([property:Key(0)] TradeSelectionHistoryRow[] Items,[property:Key(1)] string? PagingState);
[MessagePackObject]
public sealed record GetTradeSelectionInvocationQuery:IQuery<TradeSelectionProjection>
{
    [IgnoreMember] public const string Actor="TradeSelectionPipelineQuery";
    [IgnoreMember] public const string Verb="GetTradeSelectionInvocation";
    [IgnoreMember] public const int ErrorId=23210;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public SelectionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record GetTradeSelectionResultQuery:IQuery<TradeSelectionResult>
{
    [IgnoreMember] public const string Actor="TradeSelectionPipelineQuery";
    [IgnoreMember] public const string Verb="GetTradeSelectionResult";
    [IgnoreMember] public const int ErrorId=23211;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public SelectionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [Key(5)] public Guid ResultId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record GetTradeSelectionHistoryPageQuery:IQuery<TradeSelectionHistoryPage>
{
    [IgnoreMember] public const string Actor="TradeSelectionPipelineQuery";
    [IgnoreMember] public const string Verb="GetTradeSelectionHistoryPage";
    [IgnoreMember] public const int ErrorId=23212;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public SelectionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public int PortfolioId {get;init;}
    [Key(4)] public int FundId {get;init;}
    [Key(5)] public DateOnly ValueDate {get;init;}
    [Key(6)] public int PageSize {get;init;}=50;
    [Key(7)] public string? PagingState {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
public interface ITradeSelectionQueryApi
{
    Task<ServiceResult<TradeSelectionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default);
    Task<ServiceResult<TradeSelectionResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default);
    Task<ServiceResult<TradeSelectionHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=50,string? pagingState=null,CancellationToken cancellationToken=default);
}
