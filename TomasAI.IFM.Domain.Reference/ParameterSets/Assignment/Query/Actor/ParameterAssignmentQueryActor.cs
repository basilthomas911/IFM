using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query.Actor;

public sealed class ParameterAssignmentQueryActor(IQueryActorContext<ParameterAssignmentQueryActor> context) : BaseQueryActor<ParameterAssignmentQueryActor>(context, ((IParameterAssignmentQueryContext)context).Logger)
{
    public const string ActorName = "ParameterAssignmentQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetParameterAssignmentQuery.Verb] = m => m.AsQuery<GetParameterAssignmentQuery, ParameterAssignmentSnapshot>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IParameterAssignmentQueryContext, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, IParameterAssignmentQueryContext, CancellationToken, ValueTask>>
    {
        [typeof(GetParameterAssignmentQuery)] = static async (q, ctx, ct) =>
        {
            var result = await ((GetParameterAssignmentQuery)q).ExecuteAsync(ctx, ctx.Logger, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetParameterAssignmentQuery.Verb,
                new ServiceOk<ParameterAssignmentSnapshot>(result)).ConfigureAwait(false);
        },
    };
    protected override IQuery ParseMessage(IQueryActorContext<ParameterAssignmentQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterAssignmentQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(IQueryActorContext<ParameterAssignmentQueryActor> context, IQuery query, CancellationToken cancellationToken) => await (ResolveMappedQueryHandler(query, _receiveMap)(query, (IParameterAssignmentQueryContext)context, cancellationToken)).ConfigureAwait(false);
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterAssignmentQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
