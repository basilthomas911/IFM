using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query.Actor;

/// <summary>Provides the DatabaseBackupServiceQueryActor implementation.</summary>
public class DatabaseBackupServiceQueryActor(
    IQueryActorContext<DatabaseBackupServiceQueryActor> actorContext)
    : BaseQueryActor<DatabaseBackupServiceQueryActor>(actorContext, Require(actorContext).Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupServiceQueryContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupServiceQueryContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupServiceQuery";
    readonly ISystemAdminDbReadContext _dbContext = Require(actorContext).DbContext;

    /// <summary>Gets the supported concrete query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes =>CachedSupportedQueryTypes.Value;
    /// <summary>Gets the supported query verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["GetProtectionSets"] = static message => message.AsQuery<GetDatabaseProtectionSetsQuery, DatabaseProtectionSetReadModel[]>()!,
            ["GetServiceHealth"] = static message => message.AsQuery<GetDatabaseBackupServiceHealthQuery, DatabaseBackupHealthReadModel[]>()!
        };

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseBackupServiceQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupServiceQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupServiceQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ((IDatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(_dbContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<ISystemAdminDbReadContext, IQueryActorContext<DatabaseBackupServiceQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISystemAdminDbReadContext, IQueryActorContext<DatabaseBackupServiceQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetDatabaseProtectionSetsQuery)] = static async (dbContext, context, query, cancellationToken) =>
            {
                var result = await ((GetDatabaseProtectionSetsQuery)query).ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetDatabaseBackupServiceHealthQuery)] = static async (dbContext, context, query, cancellationToken) =>
            {
                var result = await ((GetDatabaseBackupServiceHealthQuery)query).ExecuteAsync(dbContext, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
            }
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseBackupServiceQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);

    /// <summary>Requires the typed Database Backup query context.</summary>
    static IDatabaseBackupServiceQueryContext Require(IQueryActorContext<DatabaseBackupServiceQueryActor> context)
        => context as IDatabaseBackupServiceQueryContext ?? throw new ArgumentException("A typed Database Backup query context is required.", nameof(context));
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedQueryTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
