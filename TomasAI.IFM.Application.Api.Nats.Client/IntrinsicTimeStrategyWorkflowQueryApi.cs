using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Implements the workflow query API over the standard NATS actor request transport.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IIntrinsicTimeStrategyWorkflowQueryApi
{
    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>> GetByIdAsync(
        StrategyWorkflowId workflowId,
        long minimumRevision = 0)
        => RequestAsync<GetIntrinsicTimeStrategyWorkflowByIdQuery, IntrinsicTimeStrategyWorkflowReadModel>(
            QuerySubject(GetIntrinsicTimeStrategyWorkflowByIdQuery.Verb, workflowId.ToString()),
            new GetIntrinsicTimeStrategyWorkflowByIdQuery
            {
                Subject = QuerySubject(GetIntrinsicTimeStrategyWorkflowByIdQuery.Verb, workflowId.ToString()),
                EntityId = new ActorEntityId(workflowId.ToString()),
                WorkflowId = workflowId,
                MinimumWorkflowRevision = minimumRevision
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>> GetActiveAsync(
        string workflowEntityId,
        long minimumRevision = 0)
        => RequestAsync<GetActiveIntrinsicTimeStrategyWorkflowQuery, ActiveIntrinsicTimeStrategyWorkflowReadModel>(
            QuerySubject(GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb, workflowEntityId),
            new GetActiveIntrinsicTimeStrategyWorkflowQuery
            {
                Subject = QuerySubject(GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb, workflowEntityId),
                EntityId = new ActorEntityId(workflowEntityId),
                WorkflowEntityId = workflowEntityId,
                MinimumWorkflowRevision = minimumRevision
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>> GetStartAttemptsAsync(
        string workflowEntityId, DateTime beforeUtc, int pageSize)
        => RequestAsync<GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery, IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>(
            QuerySubject(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery.Verb, workflowEntityId),
            new GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery
            {
                Subject = QuerySubject(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery.Verb, workflowEntityId),
                EntityId = new ActorEntityId(workflowEntityId), WorkflowEntityId = workflowEntityId,
                BeforeUtc = beforeUtc, PageSize = pageSize
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<StrategyWorkflowStageState>> GetStageStateAsync(
        StrategyWorkflowId workflowId, StrategyWorkflowStage stage, long minimumRevision = 0)
        => RequestAsync<GetIntrinsicTimeStrategyWorkflowStageStateQuery, StrategyWorkflowStageState>(
            QuerySubject(GetIntrinsicTimeStrategyWorkflowStageStateQuery.Verb, workflowId.ToString()),
            new GetIntrinsicTimeStrategyWorkflowStageStateQuery
            {
                Subject = QuerySubject(GetIntrinsicTimeStrategyWorkflowStageStateQuery.Verb, workflowId.ToString()),
                EntityId = new ActorEntityId(workflowId.ToString()), WorkflowId = workflowId,
                Stage = stage, MinimumWorkflowRevision = minimumRevision
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>> GetTimelineAsync(
        StrategyWorkflowId workflowId, long afterEventId, int pageSize)
        => RequestAsync<GetIntrinsicTimeStrategyWorkflowTimelineQuery, IntrinsicTimeStrategyWorkflowTimelineReadModel[]>(
            QuerySubject(GetIntrinsicTimeStrategyWorkflowTimelineQuery.Verb, workflowId.ToString()),
            new GetIntrinsicTimeStrategyWorkflowTimelineQuery
            {
                Subject = QuerySubject(GetIntrinsicTimeStrategyWorkflowTimelineQuery.Verb, workflowId.ToString()),
                EntityId = new ActorEntityId(workflowId.ToString()), WorkflowId = workflowId,
                AfterEventId = afterEventId, PageSize = pageSize
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetRecentAsync(
        string workflowEntityId, DateTime beforeUtc, int pageSize)
        => RequestAsync<GetRecentIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(
            QuerySubject(GetRecentIntrinsicTimeStrategyWorkflowsQuery.Verb, workflowEntityId),
            new GetRecentIntrinsicTimeStrategyWorkflowsQuery
            {
                Subject = QuerySubject(GetRecentIntrinsicTimeStrategyWorkflowsQuery.Verb, workflowEntityId),
                EntityId = new ActorEntityId(workflowEntityId), WorkflowEntityId = workflowEntityId,
                BeforeUtc = beforeUtc, PageSize = pageSize
            }).AsTask();

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetCompletedAsync(
        DateOnly startDate, DateOnly endDate, int pageSize)
        => StatusAsync<GetCompletedIntrinsicTimeStrategyWorkflowsQuery>(
            GetCompletedIntrinsicTimeStrategyWorkflowsQuery.Verb, startDate, endDate, pageSize);

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> GetStoppedAsync(
        DateOnly startDate, DateOnly endDate, int pageSize)
        => StatusAsync<GetStoppedIntrinsicTimeStrategyWorkflowsQuery>(
            GetStoppedIntrinsicTimeStrategyWorkflowsQuery.Verb, startDate, endDate, pageSize);

    /// <inheritdoc />
    public Task<ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>> GetObservationAsync(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntity)
    {
        var entityText = workflowEntity.Format();
        var subject = QuerySubject(GetIntrinsicTimeStrategyWorkflowObservationQuery.Verb, entityText);
        return RequestAsync<GetIntrinsicTimeStrategyWorkflowObservationQuery,
            IntrinsicTimeStrategyWorkflowObservationReadModel>(subject,
            new GetIntrinsicTimeStrategyWorkflowObservationQuery
            {
                Subject = subject,
                EntityId = new ActorEntityId(entityText),
                WorkflowEntity = workflowEntity
            }).AsTask();
    }

    Task<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> StatusAsync<TQuery>(
        string verb, DateOnly startDate, DateOnly endDate, int pageSize)
        where TQuery : class, IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>, new()
    {
        var entity = $"{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
        var query = new TQuery();
        EventInitHelper.SetProperty(query, nameof(IQuery.Subject), QuerySubject(verb, entity));
        EventInitHelper.SetProperty(query, nameof(IQuery.EntityId), new ActorEntityId(entity));
        EventInitHelper.SetProperty(query, "StartDate", startDate);
        EventInitHelper.SetProperty(query, "EndDate", endDate);
        EventInitHelper.SetProperty(query, "PageSize", pageSize);
        return RequestAsync<TQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(query.Subject, query).AsTask();
    }

    static ActorSubject QuerySubject(string verb, string entityId)
        => new(ActorType.Query, GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor, verb, entityId);
}
