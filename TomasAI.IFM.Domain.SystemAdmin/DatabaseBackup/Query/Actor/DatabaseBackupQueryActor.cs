using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Extensions;

using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;

/// <summary>Provides the DatabaseBackupQueryActor implementation.</summary>
public class DatabaseBackupQueryActor(
    IQueryActorContext<DatabaseBackupQueryActor> actorContext)
    : BaseQueryActor<DatabaseBackupQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IDatabaseBackupQueryContext, nameof(Context))!;

    public const string Actor = DatabaseBackupQuery.Actor;
    readonly ISystemAdminDbContext _dbContext = actorContext.DbContext;

    /// <summary>Gets the SupportedQueryTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes => QueryRoutes.Select(route => route.QueryType).ToArray();
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs => _parseMap.Keys;

    static readonly (Type QueryType, Type ResultType)[] QueryRoutes =
    [
        (typeof(GetDatabaseProtectionSetsQuery), typeof(DatabaseProtectionSetReadModel[])),
        (typeof(GetDatabaseBackupPolicyQuery), typeof(DatabaseBackupPolicyReadModel)),
        (typeof(GetDatabaseBackupOperationQuery), typeof(DatabaseBackupOperationReadModel)),
        (typeof(ListDatabaseBackupOperationsQuery), typeof(DatabaseBackupOperationReadModel[])),
        (typeof(GetDatabaseBackupSetQuery), typeof(DatabaseBackupSetReadModel)),
        (typeof(ListDatabaseRestorePointsQuery), typeof(DatabaseRestorePointReadModel[])),
        (typeof(GetDatabaseRestorePointQuery), typeof(DatabaseRestorePointReadModel)),
        (typeof(GetLatestVerifiedDatabaseBackupQuery), typeof(DatabaseRestorePointReadModel)),
        (typeof(GetLatestRestoreTestedDatabaseBackupQuery), typeof(DatabaseRestorePointReadModel)),
        (typeof(GetDatabaseRecoveryObjectiveComplianceQuery), typeof(DatabaseProtectionSetReadModel[])),
        (typeof(GetDatabaseRestoreOperationQuery), typeof(DatabaseRestoreOperationReadModel)),
        (typeof(ListDatabaseRestoreDrillsQuery), typeof(DatabaseRestoreOperationReadModel[])),
        (typeof(GetDatabaseRetentionForecastQuery), typeof(DatabaseRetentionReadModel)),
        (typeof(GetDatabaseBackupServiceHealthQuery), typeof(DatabaseBackupHealthReadModel[])),
        (typeof(GetDatabaseRecoveryRunStatsQuery), typeof(DatabaseRecoveryRunStatsReadModel))
    ];
    static readonly MethodInfo ParseMethod = typeof(DatabaseBackupQueryActor).GetMethod(nameof(ParseTyped), BindingFlags.Static | BindingFlags.NonPublic)!;
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = QueryRoutes.ToDictionary(
        route => ((DatabaseBackupQuery)Activator.CreateInstance(route.QueryType)!).Verb,
        route => (Func<IActorMessage, IQuery>)ParseMethod.MakeGenericMethod(route.QueryType, route.ResultType).CreateDelegate(typeof(Func<IActorMessage, IQuery>)),
        StringComparer.Ordinal);

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseBackupQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    static IQuery ParseTyped<TQuery, TResult>(IActorMessage message)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => message.AsQuery<TQuery, TResult>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TQuery).Name}.");

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ((DatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(this, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<DatabaseBackupQueryActor,
        IQueryActorContext<DatabaseBackupQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetDatabaseProtectionSetsQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (GetDatabaseProtectionSetsQuery)query,
                await actor._dbContext.GetProtectionSetsAsync((GetDatabaseProtectionSetsQuery)query, cancellationToken)),
        [typeof(GetDatabaseBackupPolicyQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseBackupPolicyQuery)query,
                await actor._dbContext.GetPolicyAsync((GetDatabaseBackupPolicyQuery)query, cancellationToken)),
        [typeof(GetDatabaseBackupOperationQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseBackupOperationQuery)query,
                await actor._dbContext.GetBackupOperationAsync((GetDatabaseBackupOperationQuery)query, cancellationToken)),
        [typeof(ListDatabaseBackupOperationsQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (ListDatabaseBackupOperationsQuery)query,
                await actor._dbContext.ListBackupOperationsAsync((ListDatabaseBackupOperationsQuery)query, cancellationToken)),
        [typeof(GetDatabaseBackupSetQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseBackupSetQuery)query,
                await actor._dbContext.GetBackupSetAsync((GetDatabaseBackupSetQuery)query, cancellationToken)),
        [typeof(ListDatabaseRestorePointsQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (ListDatabaseRestorePointsQuery)query,
                await actor._dbContext.ListRestorePointsAsync((ListDatabaseRestorePointsQuery)query, cancellationToken)),
        [typeof(GetDatabaseRestorePointQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseRestorePointQuery)query,
                await actor._dbContext.GetRestorePointAsync((GetDatabaseRestorePointQuery)query, cancellationToken)),
        [typeof(GetLatestVerifiedDatabaseBackupQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetLatestVerifiedDatabaseBackupQuery)query,
                await actor._dbContext.GetLatestVerifiedBackupAsync((GetLatestVerifiedDatabaseBackupQuery)query, cancellationToken)),
        [typeof(GetLatestRestoreTestedDatabaseBackupQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetLatestRestoreTestedDatabaseBackupQuery)query,
                await actor._dbContext.GetLatestRestoreTestedBackupAsync((GetLatestRestoreTestedDatabaseBackupQuery)query, cancellationToken)),
        [typeof(GetDatabaseRecoveryObjectiveComplianceQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (GetDatabaseRecoveryObjectiveComplianceQuery)query,
                await actor._dbContext.GetRecoveryObjectiveComplianceAsync((GetDatabaseRecoveryObjectiveComplianceQuery)query, cancellationToken)),
        [typeof(GetDatabaseRestoreOperationQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseRestoreOperationQuery)query,
                await actor._dbContext.GetRestoreOperationAsync((GetDatabaseRestoreOperationQuery)query, cancellationToken)),
        [typeof(ListDatabaseRestoreDrillsQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (ListDatabaseRestoreDrillsQuery)query,
                await actor._dbContext.ListRestoreDrillsAsync((ListDatabaseRestoreDrillsQuery)query, cancellationToken)),
        [typeof(GetDatabaseRetentionForecastQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseRetentionForecastQuery)query,
                await actor._dbContext.GetRetentionForecastAsync((GetDatabaseRetentionForecastQuery)query, cancellationToken)),
        [typeof(GetDatabaseBackupServiceHealthQuery)] = static async (actor, context, query, cancellationToken) =>
            await Reply(context, (GetDatabaseBackupServiceHealthQuery)query,
                await actor._dbContext.GetServiceHealthAsync((GetDatabaseBackupServiceHealthQuery)query, cancellationToken)),
        [typeof(GetDatabaseRecoveryRunStatsQuery)] = static async (actor, context, query, cancellationToken) =>
            await ReplyOne(context, (GetDatabaseRecoveryRunStatsQuery)query,
                await actor._dbContext.GetRecoveryRunStatsAsync((GetDatabaseRecoveryRunStatsQuery)query, cancellationToken))
    };

    static ValueTask Reply<TQuery, TResult>(IQueryActorContext<DatabaseBackupQueryActor> context, TQuery query, TResult result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync(query.Subject.ThreadId, query.Verb, new ServiceOk<TResult>(result));

    static ValueTask ReplyOne<TQuery, TResult>(IQueryActorContext<DatabaseBackupQueryActor> context, TQuery query, TResult? result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync<TResult>(query.Subject.ThreadId, query.Verb,
            result is null
                ? new ServiceFailed<TResult>(404, "DatabaseBackup projection was not found.")
                : new ServiceOk<TResult>(result));

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseBackupQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
