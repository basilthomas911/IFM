using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
[MessagePackObject]
public sealed record GetRiskInvocationQuery:IQuery<RiskObservation>
{
    [IgnoreMember] public const string Actor="RiskManagementQuery";
    [IgnoreMember] public const string Verb="GetRiskInvocation";
    [IgnoreMember] public const int ErrorId=23340;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
[MessagePackObject]
public sealed record GetRiskResultQuery:IQuery<RiskAssessmentResult>
{
    [IgnoreMember] public const string Actor="RiskManagementQuery";
    [IgnoreMember] public const string Verb="GetRiskResult";
    [IgnoreMember] public const int ErrorId=23341;
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
public sealed record GetRiskHistoryPageQuery:IQuery<RiskHistoryPage>
{
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
public interface IRiskQueryApi
{
    Task<ServiceResult<RiskObservation>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default);
    Task<ServiceResult<RiskAssessmentResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default);
    Task<ServiceResult<RiskHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=25,string? pagingState=null,CancellationToken cancellationToken=default);
}
