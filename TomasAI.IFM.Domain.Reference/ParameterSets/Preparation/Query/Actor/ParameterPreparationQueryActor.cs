using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;

/// <summary>Prepares owner evidence without mutating another actor's state.</summary>
public sealed class ParameterPreparationQueryActor(IQueryActorContext<ParameterPreparationQueryActor> context)
    : BaseQueryActor<ParameterPreparationQueryActor>(context, ((IParameterPreparationQueryContext)context).Logger)
{
    public const string ActorName = "ParameterPreparationQuery";

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [PrepareParameterCommandQuery.Verb] = message => message.AsQuery<PrepareParameterCommandQuery, ParameterCommandPreparation>()!,
        [GetParameterCommandEvidenceQuery.Verb] = message => message.AsQuery<GetParameterCommandEvidenceQuery, ParameterCommandEvidenceReadModel>()!
    };

    static readonly IReadOnlyDictionary<Type, Func<IQuery, IParameterPreparationQueryContext, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, IParameterPreparationQueryContext, CancellationToken, ValueTask>>
    {
        [typeof(PrepareParameterCommandQuery)] = static async (query, services, token) =>
        {
            var result = await ((PrepareParameterCommandQuery)query).ExecuteAsync(services, services.Logger, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await services.ReplyAsync(query.Subject.ThreadId, PrepareParameterCommandQuery.Verb,
                new ServiceOk<ParameterCommandPreparation>(result)).ConfigureAwait(false);
        },
        [typeof(GetParameterCommandEvidenceQuery)] = static async (query, services, token) =>
        {
            var result = await ((GetParameterCommandEvidenceQuery)query).ExecuteAsync(services, services.Logger, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await services.ReplyAsync(query.Subject.ThreadId, GetParameterCommandEvidenceQuery.Verb,
                new ServiceOk<ParameterCommandEvidenceReadModel>(result)).ConfigureAwait(false);
        }
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);

    protected override IQuery ParseMessage(IQueryActorContext<ParameterPreparationQueryActor> services, IActorMessage message)
        => ParseMappedQuery(services, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterPreparationQueryActor> services, IQuery query)
        => ReceiveAsync(services, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(IQueryActorContext<ParameterPreparationQueryActor> services, IQuery query, CancellationToken token)
        => ResolveMappedQueryHandler(query, _receiveMap)(query, (IParameterPreparationQueryContext)services, token);

    protected override ValueTask OnExceptionAsync(IQueryActorContext<ParameterPreparationQueryActor> services, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => ExceptionMappedQueryAsync(services, threadId, query, verb, exception, _exceptionMap);
}
