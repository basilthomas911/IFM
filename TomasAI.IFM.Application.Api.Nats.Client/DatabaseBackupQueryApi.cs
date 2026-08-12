using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class DatabaseBackupQueryApi(IActorProducer actorProducer) : IDatabaseBackupQueryApi
{
    readonly IActorProducer _actorProducer = actorProducer ?? throw new ArgumentNullException(nameof(actorProducer));

    public ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseProtectionSetsQuery, DatabaseProtectionSetReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseBackupPolicyReadModel>> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseBackupPolicyQuery, DatabaseBackupPolicyReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseBackupOperationReadModel>> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseBackupOperationQuery, DatabaseBackupOperationReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseBackupOperationReadModel[]>> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken = default) => SendAsync<ListDatabaseBackupOperationsQuery, DatabaseBackupOperationReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseBackupSetReadModel>> GetBackupSetAsync(GetDatabaseBackupSetQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseBackupSetQuery, DatabaseBackupSetReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestorePointReadModel[]>> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken = default) => SendAsync<ListDatabaseRestorePointsQuery, DatabaseRestorePointReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseRestorePointQuery, DatabaseRestorePointReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken = default) => SendAsync<GetLatestVerifiedDatabaseBackupQuery, DatabaseRestorePointReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken = default) => SendAsync<GetLatestRestoreTestedDatabaseBackupQuery, DatabaseRestorePointReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>> GetRecoveryObjectiveComplianceAsync(GetDatabaseRecoveryObjectiveComplianceQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseRecoveryObjectiveComplianceQuery, DatabaseProtectionSetReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestoreOperationReadModel>> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseRestoreOperationQuery, DatabaseRestoreOperationReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRestoreOperationReadModel[]>> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken = default) => SendAsync<ListDatabaseRestoreDrillsQuery, DatabaseRestoreOperationReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRetentionReadModel>> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseRetentionForecastQuery, DatabaseRetentionReadModel>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseBackupHealthReadModel[]>> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseBackupServiceHealthQuery, DatabaseBackupHealthReadModel[]>(query, cancellationToken);
    public ValueTask<ServiceResult<DatabaseRecoveryRunStatsReadModel>> GetRecoveryRunStatsAsync(GetDatabaseRecoveryRunStatsQuery query, CancellationToken cancellationToken = default) => SendAsync<GetDatabaseRecoveryRunStatsQuery, DatabaseRecoveryRunStatsReadModel>(query, cancellationToken);

    async ValueTask<ServiceResult<TResult>> SendAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken)
        where TQuery : DatabaseBackupQuery, IQuery<TResult>
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var entityId = query.EntityId.Value == Guid.Empty
                ? new DatabaseRecoveryOperationId(query.Request.RequestId)
                : query.EntityId;
            var normalized = (TQuery)(query with
            {
                EntityId = entityId,
                Subject = new ActorSubject(ActorType.Query, DatabaseBackupQuery.Actor, query.Verb, entityId.Format())
            });
            normalized.Validate();
            return await _actorProducer.RequestAsync<TResult, TQuery>(normalized.Subject, normalized, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return new ServiceFailed<TResult>(query.ErrorCode, exception.Message);
        }
    }
}
