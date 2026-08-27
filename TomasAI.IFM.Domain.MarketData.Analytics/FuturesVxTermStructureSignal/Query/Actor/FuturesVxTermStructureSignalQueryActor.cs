using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;

/// <summary>Processes latest VX term-structure read-model queries.</summary>
public sealed class FuturesVxTermStructureSignalQueryActor(
    IQueryActorContext<FuturesVxTermStructureSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesVxTermStructureSignalQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = GetLatestFuturesVxTermStructureSignalQuery.Actor;
    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName,
            Verb: GetLatestFuturesVxTermStructureSignalQuery.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from {message.Subject}.");
        var query = message.AsQuery<GetLatestFuturesVxTermStructureSignalQuery,
            FuturesVxTermStructureSignalReadModel?>();
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        if (query is not GetLatestFuturesVxTermStructureSignalQuery latest)
            throw new InvalidOperationException($"Unsupported {ActorName} query {query.GetType().Name}.");
        var result = await latest.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId,
            GetLatestFuturesVxTermStructureSignalQuery.Verb,
            new ServiceResult<FuturesVxTermStructureSignalReadModel?>(result)).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        ActorThreadId threadId, IQuery query, string verb, Exception exception) =>
        await context.ReplyAsync(threadId, verb,
            new ServiceResult<FuturesVxTermStructureSignalReadModel?>(query.ErrorCode, exception.Message));
}
