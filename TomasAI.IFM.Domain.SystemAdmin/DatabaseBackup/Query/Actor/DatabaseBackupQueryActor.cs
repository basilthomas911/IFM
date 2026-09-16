using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;

/// <summary>Provides the DatabaseBackupQueryActor implementation.</summary>
public class DatabaseBackupQueryActor(
    IQueryActorContext<DatabaseBackupQueryActor> actorContext)
    : BaseQueryActor<DatabaseBackupQueryActor>(actorContext, Require(actorContext).Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IDatabaseBackupQueryContext, nameof(Context))!;

    public const string Actor = DatabaseBackupQuery.Actor;
    readonly ISystemAdminDbContext _dbContext = Require(actorContext).DbContext;

    /// <summary>Gets the supported concrete query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes => _receiveMap.Keys.ToArray();
    /// <summary>Gets the supported query verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs => _parseMap.Keys.ToArray();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        ["GetProtectionSets"] = static message => message.AsQuery<GetDatabaseProtectionSetsQuery, DatabaseProtectionSetReadModel[]>()!,
        ["GetPolicy"] = static message => message.AsQuery<GetDatabaseBackupPolicyQuery, DatabaseBackupPolicyReadModel>()!,
        ["GetBackupOperation"] = static message => message.AsQuery<GetDatabaseBackupOperationQuery, DatabaseBackupOperationReadModel>()!,
        ["ListBackupOperations"] = static message => message.AsQuery<ListDatabaseBackupOperationsQuery, DatabaseBackupOperationReadModel[]>()!,
        ["GetBackupSet"] = static message => message.AsQuery<GetDatabaseBackupSetQuery, DatabaseBackupSetReadModel>()!,
        ["ListRestorePoints"] = static message => message.AsQuery<ListDatabaseRestorePointsQuery, DatabaseRestorePointReadModel[]>()!,
        ["GetRestorePoint"] = static message => message.AsQuery<GetDatabaseRestorePointQuery, DatabaseRestorePointReadModel>()!,
        ["GetLatestVerifiedBackup"] = static message => message.AsQuery<GetLatestVerifiedDatabaseBackupQuery, DatabaseRestorePointReadModel>()!,
        ["GetLatestRestoreTestedBackup"] = static message => message.AsQuery<GetLatestRestoreTestedDatabaseBackupQuery, DatabaseRestorePointReadModel>()!,
        ["GetRecoveryObjectiveCompliance"] = static message => message.AsQuery<GetDatabaseRecoveryObjectiveComplianceQuery, DatabaseProtectionSetReadModel[]>()!,
        ["GetRestoreOperation"] = static message => message.AsQuery<GetDatabaseRestoreOperationQuery, DatabaseRestoreOperationReadModel>()!,
        ["ListRestoreDrills"] = static message => message.AsQuery<ListDatabaseRestoreDrillsQuery, DatabaseRestoreOperationReadModel[]>()!,
        ["GetRetentionForecast"] = static message => message.AsQuery<GetDatabaseRetentionForecastQuery, DatabaseRetentionReadModel>()!,
        ["GetServiceHealth"] = static message => message.AsQuery<GetDatabaseBackupServiceHealthQuery, DatabaseBackupHealthReadModel[]>()!,
        ["GetRecoveryRunStats"] = static message => message.AsQuery<GetDatabaseRecoveryRunStatsQuery, DatabaseRecoveryRunStatsReadModel>()!
    };

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseBackupQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ((DatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(_dbContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseBackupQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseBackupQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetDatabaseProtectionSetsQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseProtectionSetsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseBackupPolicyQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseBackupPolicyQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseBackupOperationQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseBackupOperationQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(ListDatabaseBackupOperationsQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((ListDatabaseBackupOperationsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseBackupSetQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseBackupSetQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(ListDatabaseRestorePointsQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((ListDatabaseRestorePointsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseRestorePointQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseRestorePointQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetLatestVerifiedDatabaseBackupQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetLatestVerifiedDatabaseBackupQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetLatestRestoreTestedDatabaseBackupQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetLatestRestoreTestedDatabaseBackupQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseRecoveryObjectiveComplianceQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseRecoveryObjectiveComplianceQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseRestoreOperationQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseRestoreOperationQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(ListDatabaseRestoreDrillsQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((ListDatabaseRestoreDrillsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseRetentionForecastQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseRetentionForecastQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseBackupServiceHealthQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseBackupServiceHealthQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
        [typeof(GetDatabaseRecoveryRunStatsQuery)] = static (dbContext, context, query, cancellationToken) =>
            ((GetDatabaseRecoveryRunStatsQuery)query).ExecuteAsync(dbContext, context, cancellationToken)
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseBackupQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

    /// <summary>Requires the typed Database Backup query context.</summary>
    static IDatabaseBackupQueryContext Require(IQueryActorContext<DatabaseBackupQueryActor> context)
        => context as IDatabaseBackupQueryContext ?? throw new ArgumentException("A typed Database Backup query context is required.", nameof(context));
}
