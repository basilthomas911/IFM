using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query.Actor;

/// <summary>Provides the DatabaseBackupQueryActor implementation.</summary>
public class DatabaseBackupQueryActor(
    IQueryActorContext<DatabaseBackupQueryActor> actorContext)
    : BaseQueryActor<DatabaseBackupQueryActor>(actorContext, Require(actorContext).Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupQueryContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupQueryContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupQuery";
    readonly ISystemAdminDbReadContext _dbContext = Require(actorContext).DbContext;

    /// <summary>Gets the supported concrete query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes =>CachedSupportedQueryTypes.Value;
    /// <summary>Gets the supported query verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["GetBackupOperation"] = static message => message.AsQuery<GetDatabaseBackupOperationQuery, DatabaseBackupOperationReadModel>()!,
            ["ListBackupOperations"] = static message => message.AsQuery<ListDatabaseBackupOperationsQuery, DatabaseBackupOperationReadModel[]>()!,
            ["GetLatestVerifiedBackup"] = static message => message.AsQuery<GetLatestVerifiedDatabaseBackupQuery, DatabaseRestorePointReadModel>()!
        };

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseBackupQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ((IDatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(_dbContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<ISystemAdminDbReadContext, IQueryActorContext<DatabaseBackupQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISystemAdminDbReadContext, IQueryActorContext<DatabaseBackupQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetDatabaseBackupOperationQuery)] = static async (dbContext, context, query, cancellationToken) =>
            {
                var result = await ((GetDatabaseBackupOperationQuery)query).ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
            },
            [typeof(ListDatabaseBackupOperationsQuery)] = static async (dbContext, context, query, cancellationToken) =>
            {
                var result = await ((ListDatabaseBackupOperationsQuery)query).ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetLatestVerifiedDatabaseBackupQuery)] = static async (dbContext, context, query, cancellationToken) =>
            {
                var result = await ((GetLatestVerifiedDatabaseBackupQuery)query).ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
            }
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseBackupQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);

    /// <summary>Requires the typed Database Backup query context.</summary>
    static IDatabaseBackupQueryContext Require(IQueryActorContext<DatabaseBackupQueryActor> context)
        => context as IDatabaseBackupQueryContext ?? throw new ArgumentException("A typed Database Backup query context is required.", nameof(context));
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedQueryTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
