using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.Api.Nats.Client;
/// <summary>Sends concrete persisted-model queries using the established actor transport.</summary>
public sealed class ScheduledTaskQueryApi(IActorProducer producer) : IScheduledTaskQueryApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskOutputPage>> GetScheduledTaskOutputAsync(GetScheduledTaskOutputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskOutputQuery.Actor, GetScheduledTaskOutputQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskOutputPage, GetScheduledTaskOutputQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskRunPage>> GetScheduledTaskRunHistoryAsync(GetScheduledTaskRunHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskRunHistoryQuery.Actor, GetScheduledTaskRunHistoryQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskRunPage, GetScheduledTaskRunHistoryQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskCatalog>> GetScheduledTaskCatalogAsync(GetScheduledTaskCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskCatalogQuery.Actor, GetScheduledTaskCatalogQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskCatalog, GetScheduledTaskCatalogQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTasksDashboard>> GetScheduledTasksDashboardAsync(GetScheduledTasksDashboardQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTasksDashboardQuery.Actor, GetScheduledTasksDashboardQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTasksDashboard, GetScheduledTasksDashboardQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskDefinition>> GetScheduledTaskAsync(GetScheduledTaskQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskQuery.Actor, GetScheduledTaskQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskDefinition, GetScheduledTaskQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskSchedulePreview>> PreviewScheduledTaskScheduleAsync(PreviewScheduledTaskScheduleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, PreviewScheduledTaskScheduleQuery.Actor, PreviewScheduledTaskScheduleQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskSchedulePreview, PreviewScheduledTaskScheduleQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskRun>> GetScheduledTaskRunAsync(GetScheduledTaskRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskRunQuery.Actor, GetScheduledTaskRunQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskRun, GetScheduledTaskRunQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskRun[]>> ListScheduledTaskRunsAsync(ListScheduledTaskRunsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, ListScheduledTaskRunsQuery.Actor, ListScheduledTaskRunsQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskRun[], ListScheduledTaskRunsQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledTaskHostCapability>> GetScheduledTaskHostHealthAsync(GetScheduledTaskHostHealthQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledTaskHostHealthQuery.Actor, GetScheduledTaskHostHealthQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledTaskHostCapability, GetScheduledTaskHostHealthQuery>(normalized.Subject, normalized, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<ScheduledMarketPositionPage>> GetScheduledMarketPositionsAsync(GetScheduledMarketPositionsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var identity = query.EntityId.IsValid ? query.EntityId : new ScheduledTaskId(Guid.NewGuid());
        var normalized = query with { EntityId = identity, Subject = new(ActorType.Query, GetScheduledMarketPositionsQuery.Actor, GetScheduledMarketPositionsQuery.Verb, identity.Format()) };
        return producer.RequestAsync<ScheduledMarketPositionPage, GetScheduledMarketPositionsQuery>(normalized.Subject, normalized, cancellationToken);
    }
}
