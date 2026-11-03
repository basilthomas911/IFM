using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query.Actor;

/// <summary>Provides the DatabaseRecoveryQueryActor implementation.</summary>
public class DatabaseRecoveryQueryActor(
    IQueryActorContext<DatabaseRecoveryQueryActor> actorContext)
    : BaseQueryActor<DatabaseRecoveryQueryActor>(actorContext, Require(actorContext).Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseRecoveryQueryContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseRecoveryQueryContext, nameof(Context))!;

    public const string Actor = "DatabaseRecoveryQuery";
    readonly ISystemAdminDbContext _dbContext = Require(actorContext).DbContext;

    /// <summary>Gets the supported concrete query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes =>CachedSupportedQueryTypes.Value;
    /// <summary>Gets the supported query verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["ListRestorePoints"] = static message => message.AsQuery<ListDatabaseRestorePointsQuery, DatabaseRestorePointReadModel[]>()!,
            ["GetRestorePoint"] = static message => message.AsQuery<GetDatabaseRestorePointQuery, DatabaseRestorePointReadModel>()!,
            ["GetLatestRestoreTestedBackup"] = static message => message.AsQuery<GetLatestRestoreTestedDatabaseBackupQuery, DatabaseRestorePointReadModel>()!,
            ["GetRecoveryObjectiveCompliance"] = static message => message.AsQuery<GetDatabaseRecoveryObjectiveComplianceQuery, DatabaseProtectionSetReadModel[]>()!,
            ["GetRestoreOperation"] = static message => message.AsQuery<GetDatabaseRestoreOperationQuery, DatabaseRestoreOperationReadModel>()!,
            ["ListRestoreDrills"] = static message => message.AsQuery<ListDatabaseRestoreDrillsQuery, DatabaseRestoreOperationReadModel[]>()!,
            ["GetRecoveryRunStats"] = static message => message.AsQuery<GetDatabaseRecoveryRunStatsQuery, DatabaseRecoveryRunStatsReadModel>()!
        };

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseRecoveryQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseRecoveryQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseRecoveryQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ((IDatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(_dbContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseRecoveryQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseRecoveryQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(ListDatabaseRestorePointsQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((ListDatabaseRestorePointsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetDatabaseRestorePointQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetDatabaseRestorePointQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetLatestRestoreTestedDatabaseBackupQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetLatestRestoreTestedDatabaseBackupQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetDatabaseRecoveryObjectiveComplianceQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetDatabaseRecoveryObjectiveComplianceQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetDatabaseRestoreOperationQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetDatabaseRestoreOperationQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(ListDatabaseRestoreDrillsQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((ListDatabaseRestoreDrillsQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetDatabaseRecoveryRunStatsQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetDatabaseRecoveryRunStatsQuery)query).ExecuteAsync(dbContext, context, cancellationToken)
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseRecoveryQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);

    /// <summary>Requires the typed Database Backup query context.</summary>
    static IDatabaseRecoveryQueryContext Require(IQueryActorContext<DatabaseRecoveryQueryActor> context)
        => context as IDatabaseRecoveryQueryContext ?? throw new ArgumentException("A typed Database Backup query context is required.", nameof(context));
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedQueryTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
