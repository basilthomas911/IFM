using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;

/// <summary>Processes latest and history VWAP read-model queries.</summary>
public sealed class FuturesVwapSignalQueryActor(
    IQueryActorContext<FuturesVwapSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesVwapSignalQueryActor>(actorContext,
        ((IFuturesVwapSignalQueryContext)actorContext).Logger)
{
    /// <summary>Identifies the VWAP Query mailbox.</summary>
    public const string ActorName = GetLatestFuturesVwapSignalQuery.Actor;
    IFuturesVwapSignalQueryContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVwapSignalQueryContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from {message.Subject}.");
        IQuery query = message.Subject.Verb switch
        {
            GetLatestFuturesVwapSignalQuery.Verb =>
                message.AsQuery<GetLatestFuturesVwapSignalQuery, FuturesVwapSignalReadModel?>()!,
            GetFuturesVwapSignalHistoryQuery.Verb =>
                message.AsQuery<GetFuturesVwapSignalHistoryQuery, FuturesVwapSignalReadModel[]>()!,
            _ => throw new InvalidOperationException($"Unsupported VWAP query verb {message.Subject.Verb}.")
        };
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IQuery query, CancellationToken cancellationToken)
    {
        switch (query)
        {
            case GetLatestFuturesVwapSignalQuery latest:
                var current = await latest.ExecuteAsync(
                    TypedContext.DbFactory, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, latest.Subject.Verb,
                    new ServiceResult<FuturesVwapSignalReadModel?>(current)).ConfigureAwait(false);
                break;
            case GetFuturesVwapSignalHistoryQuery history:
                var values = await history.ExecuteAsync(
                    TypedContext.DbFactory, cancellationToken).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, history.Subject.Verb,
                    new ServiceResult<FuturesVwapSignalReadModel[]>(values)).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported VWAP query {query.GetType().Name}.");
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        ActorThreadId threadId, IQuery query, string verb, Exception exception)
    {
        if (query is GetFuturesVwapSignalHistoryQuery)
            await context.ReplyAsync(threadId, verb,
                new ServiceResult<FuturesVwapSignalReadModel[]>(query.ErrorCode, exception.Message));
        else
            await context.ReplyAsync(threadId, verb,
                new ServiceResult<FuturesVwapSignalReadModel?>(query.ErrorCode, exception.Message));
    }
}
