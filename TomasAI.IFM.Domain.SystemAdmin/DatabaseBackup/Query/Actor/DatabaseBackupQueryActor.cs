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
    : BaseQueryActor<DatabaseBackupQueryActor>(actorContext.Logger, actorContext.ActorId)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupQueryContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IDatabaseBackupQueryContext, nameof(actorContext))!;

    public const string Actor = DatabaseBackupQuery.Actor;
    readonly ISystemAdminDbContext _dbContext = actorContext.DbContext;

    /// <summary>Gets the SupportedQueryTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes => QueryRoutes.Select(route => route.QueryType).ToArray();
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs => ParseMap.Keys;

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
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> ParseMap = QueryRoutes.ToDictionary(
        route => ((DatabaseBackupQuery)Activator.CreateInstance(route.QueryType)!).Verb,
        route => (Func<IActorMessage, IQuery>)ParseMethod.MakeGenericMethod(route.QueryType, route.ResultType).CreateDelegate(typeof(Func<IActorMessage, IQuery>)),
        StringComparer.Ordinal);

    protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: Actor }
            || !ParseMap.TryGetValue(message.Subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {Actor} query from message: {message.Subject}");
        var query = parser(message);
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb, new ActorMessageInfo(message, query));
        return query;
    }

    static IQuery ParseTyped<TQuery, TResult>(IActorMessage message)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => message.AsQuery<TQuery, TResult>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TQuery).Name}.");

    protected override ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IQuery query, CancellationToken cancellationToken)
    {
        var dispatchContext = actorContext.RouteTo(context);
        ((DatabaseBackupQuery)query).Validate();
        switch (query)
        {
            case GetDatabaseProtectionSetsQuery value: await Reply(context, value, await _dbContext.GetProtectionSetsAsync(value, cancellationToken)); break;
            case GetDatabaseBackupPolicyQuery value: await ReplyOne(context, value, await _dbContext.GetPolicyAsync(value, cancellationToken)); break;
            case GetDatabaseBackupOperationQuery value: await ReplyOne(context, value, await _dbContext.GetBackupOperationAsync(value, cancellationToken)); break;
            case ListDatabaseBackupOperationsQuery value: await Reply(context, value, await _dbContext.ListBackupOperationsAsync(value, cancellationToken)); break;
            case GetDatabaseBackupSetQuery value: await ReplyOne(context, value, await _dbContext.GetBackupSetAsync(value, cancellationToken)); break;
            case ListDatabaseRestorePointsQuery value: await Reply(context, value, await _dbContext.ListRestorePointsAsync(value, cancellationToken)); break;
            case GetDatabaseRestorePointQuery value: await ReplyOne(context, value, await _dbContext.GetRestorePointAsync(value, cancellationToken)); break;
            case GetLatestVerifiedDatabaseBackupQuery value: await ReplyOne(context, value, await _dbContext.GetLatestVerifiedBackupAsync(value, cancellationToken)); break;
            case GetLatestRestoreTestedDatabaseBackupQuery value: await ReplyOne(context, value, await _dbContext.GetLatestRestoreTestedBackupAsync(value, cancellationToken)); break;
            case GetDatabaseRecoveryObjectiveComplianceQuery value: await Reply(context, value, await _dbContext.GetRecoveryObjectiveComplianceAsync(value, cancellationToken)); break;
            case GetDatabaseRestoreOperationQuery value: await ReplyOne(context, value, await _dbContext.GetRestoreOperationAsync(value, cancellationToken)); break;
            case ListDatabaseRestoreDrillsQuery value: await Reply(context, value, await _dbContext.ListRestoreDrillsAsync(value, cancellationToken)); break;
            case GetDatabaseRetentionForecastQuery value: await ReplyOne(context, value, await _dbContext.GetRetentionForecastAsync(value, cancellationToken)); break;
            case GetDatabaseBackupServiceHealthQuery value: await Reply(context, value, await _dbContext.GetServiceHealthAsync(value, cancellationToken)); break;
            case GetDatabaseRecoveryRunStatsQuery value: await ReplyOne(context, value, await _dbContext.GetRecoveryRunStatsAsync(value, cancellationToken)); break;
            default: throw new InvalidOperationException($"Unsupported DatabaseBackup query '{query.GetType().Name}'.");
        }
    }

    static ValueTask Reply<TQuery, TResult>(IQueryActorContext context, TQuery query, TResult result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync(query.Subject.ThreadId, query.Verb, new ServiceOk<TResult>(result));

    static ValueTask ReplyOne<TQuery, TResult>(IQueryActorContext context, TQuery query, TResult? result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync<TResult>(query.Subject.ThreadId, query.Verb,
            result is null
                ? new ServiceFailed<TResult>(404, "DatabaseBackup projection was not found.")
                : new ServiceOk<TResult>(result));

    protected override ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
    {
        return query switch
        {
            GetDatabaseProtectionSetsQuery or GetDatabaseRecoveryObjectiveComplianceQuery => Failure<DatabaseProtectionSetReadModel[]>(),
            GetDatabaseBackupPolicyQuery => Failure<DatabaseBackupPolicyReadModel>(),
            GetDatabaseBackupOperationQuery => Failure<DatabaseBackupOperationReadModel>(),
            ListDatabaseBackupOperationsQuery => Failure<DatabaseBackupOperationReadModel[]>(),
            GetDatabaseBackupSetQuery => Failure<DatabaseBackupSetReadModel>(),
            ListDatabaseRestorePointsQuery => Failure<DatabaseRestorePointReadModel[]>(),
            GetDatabaseRestorePointQuery or GetLatestVerifiedDatabaseBackupQuery or GetLatestRestoreTestedDatabaseBackupQuery => Failure<DatabaseRestorePointReadModel>(),
            GetDatabaseRestoreOperationQuery => Failure<DatabaseRestoreOperationReadModel>(),
            ListDatabaseRestoreDrillsQuery => Failure<DatabaseRestoreOperationReadModel[]>(),
            GetDatabaseRetentionForecastQuery => Failure<DatabaseRetentionReadModel>(),
            GetDatabaseBackupServiceHealthQuery => Failure<DatabaseBackupHealthReadModel[]>(),
            GetDatabaseRecoveryRunStatsQuery => Failure<DatabaseRecoveryRunStatsReadModel>(),
            _ => throw new InvalidOperationException($"Unsupported DatabaseBackup query '{query.GetType().Name}'.")
        };

        ValueTask Failure<TResult>() where TResult : class
            => context.ReplyAsync(threadId, verb, new ServiceFailed<TResult>(query.ErrorCode, exception.Message));
    }
}
