using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using StrategyIronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>NATS client for strategy-specific Trade Plan current and history queries.</summary>
public sealed class StrategyTradePlanQueryApi(IActorProducer producer)
    : NatsClientApi(producer), IStrategyTradePlanQueryApi
{
    public Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentIronCondorAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        var id = new StrategyIronCondorTradePlanId(positionId, valueDate);
        var query = new GetCurrentIronCondorTradePlanQuery
        {
            PlanId = id,
            Subject = Subject(GetCurrentIronCondorTradePlanQuery.Actor,
                GetCurrentIronCondorTradePlanQuery.Verb, id.Format())
        };
        return RequestAsync<GetCurrentIronCondorTradePlanQuery, StrategyTradePlanSnapshot?>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanHistoryPage>> GetIronCondorHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        var id = new StrategyIronCondorTradePlanId(positionId, valueDate);
        var query = new GetIronCondorTradePlanHistoryQuery
        {
            PlanId = id, PageSize = pageSize, PagingState = pagingState,
            Subject = Subject(GetIronCondorTradePlanHistoryQuery.Actor,
                GetIronCondorTradePlanHistoryQuery.Verb, id.Format())
        };
        return RequestAsync<GetIronCondorTradePlanHistoryQuery, StrategyTradePlanHistoryPage>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentVerticalSpreadAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        var id = new VerticalSpreadTradePlanId(positionId, valueDate);
        var query = new GetCurrentVerticalSpreadTradePlanQuery
        {
            PlanId = id,
            Subject = Subject(GetCurrentVerticalSpreadTradePlanQuery.Actor,
                GetCurrentVerticalSpreadTradePlanQuery.Verb, id.Format())
        };
        return RequestAsync<GetCurrentVerticalSpreadTradePlanQuery, StrategyTradePlanSnapshot?>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanHistoryPage>> GetVerticalSpreadHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        var id = new VerticalSpreadTradePlanId(positionId, valueDate);
        var query = new GetVerticalSpreadTradePlanHistoryQuery
        {
            PlanId = id, PageSize = pageSize, PagingState = pagingState,
            Subject = Subject(GetVerticalSpreadTradePlanHistoryQuery.Actor,
                GetVerticalSpreadTradePlanHistoryQuery.Verb, id.Format())
        };
        return RequestAsync<GetVerticalSpreadTradePlanHistoryQuery, StrategyTradePlanHistoryPage>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentFuturesAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        var id = new FuturesTradePlanId(positionId, valueDate);
        var query = new GetCurrentFuturesTradePlanQuery
        {
            PlanId = id,
            Subject = Subject(GetCurrentFuturesTradePlanQuery.Actor,
                GetCurrentFuturesTradePlanQuery.Verb, id.Format())
        };
        return RequestAsync<GetCurrentFuturesTradePlanQuery, StrategyTradePlanSnapshot?>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanHistoryPage>> GetFuturesHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        var id = new FuturesTradePlanId(positionId, valueDate);
        var query = new GetFuturesTradePlanHistoryQuery
        {
            PlanId = id, PageSize = pageSize, PagingState = pagingState,
            Subject = Subject(GetFuturesTradePlanHistoryQuery.Actor,
                GetFuturesTradePlanHistoryQuery.Verb, id.Format())
        };
        return RequestAsync<GetFuturesTradePlanHistoryQuery, StrategyTradePlanHistoryPage>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<StrategyTradePlanActivityPage>> GetActivityAsync(
        DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        var streamId = valueDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var query = new GetStrategyTradePlanActivityQuery
        {
            ValueDate = valueDate,
            PageSize = pageSize,
            PagingState = pagingState,
            Subject = Subject(GetStrategyTradePlanActivityQuery.Actor,
                GetStrategyTradePlanActivityQuery.Verb, streamId)
        };
        return RequestAsync<GetStrategyTradePlanActivityQuery, StrategyTradePlanActivityPage>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<ExitPositionWorkflowProjection?>> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId, DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        var streamId = $"{positionId.Format()}.{valueDate:yyyyMMdd}";
        var query = new GetPositionExitWorkflowQuery
        {
            PositionId = positionId,
            ValueDate = valueDate,
            Subject = Subject(GetPositionExitWorkflowQuery.Actor,
                GetPositionExitWorkflowQuery.Verb, streamId)
        };
        return RequestAsync<GetPositionExitWorkflowQuery, ExitPositionWorkflowProjection?>(
            query.Subject, query, cancellationToken).AsTask();
    }

    public Task<ServiceResult<PositionExitWorkflowHistoryPage>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100,
        byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        var streamId = $"{positionId.Format()}.{valueDate:yyyyMMdd}";
        var query = new GetPositionExitWorkflowTimelineQuery
        {
            PositionId = positionId,
            ValueDate = valueDate,
            PageSize = pageSize,
            PagingState = pagingState,
            Subject = Subject(GetPositionExitWorkflowTimelineQuery.Actor,
                GetPositionExitWorkflowTimelineQuery.Verb, streamId)
        };
        return RequestAsync<GetPositionExitWorkflowTimelineQuery,
            PositionExitWorkflowHistoryPage>(query.Subject, query, cancellationToken).AsTask();
    }

    static ActorSubject Subject(string actor, string verb, string entityId) =>
        new(ActorType.Query, actor, verb, entityId);
}
