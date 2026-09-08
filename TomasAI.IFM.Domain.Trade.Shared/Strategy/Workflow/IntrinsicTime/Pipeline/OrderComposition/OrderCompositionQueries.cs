using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
[MessagePackObject]
public sealed record CompositionQueryAccess([property:Key(0)] string Principal,[property:Key(1)] string[] Roles);
[MessagePackObject]
public sealed record OrderCompositionProjection([property:Key(0)] OrderCompositionFunctionCompletedEvent Completion,[property:Key(1)] bool WorkflowAccepted,[property:Key(2)] bool AcceptanceUnknown,[property:Key(3)] bool SuspectedOrphan);
[MessagePackObject]
public sealed record OrderCompositionHistoryRow([property:Key(0)] int PortfolioId,[property:Key(1)] int FundId,[property:Key(2)] DateOnly ValueDate,[property:Key(3)] DateTime OccurredAtUtc,[property:Key(4)] Guid WorkflowId,[property:Key(5)] Guid InvocationId,[property:Key(6)] Guid EventId,[property:Key(7)] short TargetHorizon,[property:Key(8)] byte Outcome,[property:Key(9)] string ReasonCode,[property:Key(10)] Guid ResultId,[property:Key(11)] string ResultSha256);
[MessagePackObject]
public sealed record OrderCompositionHistoryPage([property:Key(0)] OrderCompositionHistoryRow[] Items,[property:Key(1)] string? PagingState);
[MessagePackObject]
public sealed record GetOrderCompositionInvocationQuery:IQuery<OrderCompositionProjection>
{
    [IgnoreMember] public const string Actor="OrderCompositionPipelineQuery";
    [IgnoreMember] public const string Verb="GetOrderCompositionInvocation";
    [IgnoreMember] public const int ErrorId=23213;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record GetOrderCompositionResultQuery:IQuery<OrderCompositionResult>
{
    [IgnoreMember] public const string Actor="OrderCompositionPipelineQuery";
    [IgnoreMember] public const string Verb="GetOrderCompositionResult";
    [IgnoreMember] public const int ErrorId=23214;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [Key(5)] public Guid ResultId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record GetOrderCompositionHistoryPageQuery:IQuery<OrderCompositionHistoryPage>
{
    [IgnoreMember] public const string Actor="OrderCompositionPipelineQuery";
    [IgnoreMember] public const string Verb="GetOrderCompositionHistoryPage";
    [IgnoreMember] public const int ErrorId=23215;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public int PortfolioId {get;init;}
    [Key(4)] public int FundId {get;init;}
    [Key(5)] public DateOnly ValueDate {get;init;}
    [Key(6)] public int PageSize {get;init;}=50;
    [Key(7)] public string? PagingState {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
public interface IOrderCompositionQueryApi
{
    Task<ServiceResult<OrderCompositionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default);
    Task<ServiceResult<OrderCompositionResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default);
    Task<ServiceResult<OrderCompositionHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=50,string? pagingState=null,CancellationToken cancellationToken=default);
}
