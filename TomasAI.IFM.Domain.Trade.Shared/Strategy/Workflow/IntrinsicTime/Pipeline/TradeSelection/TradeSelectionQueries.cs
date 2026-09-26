using TomasAI.IFM.Domain.Trade.Shared;
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
public interface ITradeSelectionQueryApi
{
    Task<ServiceResult<TradeSelectionProjection>> GetInvocationAsync(StrategyWorkflowId workflowId,Guid invocationId,CancellationToken cancellationToken=default);
    Task<ServiceResult<TradeSelectionResult>> GetResultAsync(StrategyWorkflowId workflowId,Guid invocationId,Guid resultId,CancellationToken cancellationToken=default);
    Task<ServiceResult<TradeSelectionHistoryPage>> GetHistoryAsync(int portfolioId,int fundId,DateOnly valueDate,int pageSize=50,string? pagingState=null,CancellationToken cancellationToken=default);
}
