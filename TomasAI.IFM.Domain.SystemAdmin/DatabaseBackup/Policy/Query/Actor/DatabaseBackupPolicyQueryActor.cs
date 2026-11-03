using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Query;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Query.Actor;

/// <summary>Provides the DatabaseBackupPolicyQueryActor implementation.</summary>
public class DatabaseBackupPolicyQueryActor(
    IQueryActorContext<DatabaseBackupPolicyQueryActor> actorContext)
    : BaseQueryActor<DatabaseBackupPolicyQueryActor>(actorContext, Require(actorContext).Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupPolicyQueryContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupPolicyQueryContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupPolicyQuery";
    readonly ISystemAdminDbContext _dbContext = Require(actorContext).DbContext;

    /// <summary>Gets the supported concrete query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes =>CachedSupportedQueryTypes.Value;
    /// <summary>Gets the supported query verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["GetBackupSetup"] = static message => message.AsQuery<GetDatabaseBackupSetupQuery, DatabaseBackupSetupReadModel>()!,
            ["GetPolicy"] = static message => message.AsQuery<GetDatabaseBackupPolicyQuery, DatabaseBackupPolicyReadModel>()!
        };

    protected override IQuery ParseMessage(IQueryActorContext<DatabaseBackupPolicyQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupPolicyQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<DatabaseBackupPolicyQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ((IDatabaseBackupQuery)query).Validate();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(_dbContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseBackupPolicyQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISystemAdminDbContext, IQueryActorContext<DatabaseBackupPolicyQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetDatabaseBackupSetupQuery)] = static (dbContext, context, query, cancellationToken) => ((GetDatabaseBackupSetupQuery)query).ExecuteAsync(dbContext, context, cancellationToken),
            [typeof(GetDatabaseBackupPolicyQuery)] = static (dbContext, context, query, cancellationToken) =>
                ((GetDatabaseBackupPolicyQuery)query).ExecuteAsync(dbContext, context, cancellationToken)
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<DatabaseBackupPolicyQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);

    /// <summary>Requires the typed Database Backup query context.</summary>
    static IDatabaseBackupPolicyQueryContext Require(IQueryActorContext<DatabaseBackupPolicyQueryActor> context)
        => context as IDatabaseBackupPolicyQueryContext ?? throw new ArgumentException("A typed Database Backup query context is required.", nameof(context));
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedQueryTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
