using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;

/// <summary>Strict query actor for the latest durable Market Outlook snapshot.</summary>
public class MarketOutlookSnapshotQueryActor(
    IQueryActorContext<MarketOutlookSnapshotQueryActor> actorContext)
    : BaseQueryActor<MarketOutlookSnapshotQueryActor>(
        actorContext,
        ((IMarketOutlookSnapshotQueryContext)actorContext).Logger)
{
    public const string ActorName = GetMarketOutlookSnapshotQuery.Actor;

    IMarketOutlookSnapshotQueryContext DomainContext =>
        (IMarketOutlookSnapshotQueryContext)Context;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetMarketOutlookSnapshotQuery.Verb] = message =>
                message.AsQuery<GetMarketOutlookSnapshotQuery, MarketOutlookReadModel>()
                ?? throw new InvalidOperationException("Unable to deserialize the Market Outlook snapshot query.")
        };

    static readonly IReadOnlyDictionary<Type, Func<
        IQueryActorContext<MarketOutlookSnapshotQueryActor>,
        IDbContextFactory,
        IQuery,
        CancellationToken,
        ValueTask>> _receiveMap = new Dictionary<Type, Func<
            IQueryActorContext<MarketOutlookSnapshotQueryActor>,
            IDbContextFactory,
            IQuery,
            CancellationToken,
            ValueTask>>
        {
            [typeof(GetMarketOutlookSnapshotQuery)] = ReceiveSnapshotAsync
        };

    protected override IQuery ParseMessage(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
        => ResolveMappedQueryHandler(query, _receiveMap)(
            context, DomainContext.DbFactory, query, cancellationToken);

    static async ValueTask ReceiveSnapshotAsync(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        IDbContextFactory dbFactory,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var request = (GetMarketOutlookSnapshotQuery)query;
        var snapshot = await dbFactory.MarketDataDb.GetMarketOutlookSnapshotAsync(
            request.ContractId, request.ValueDate, cancellationToken).ConfigureAwait(false);
        ServiceResult<MarketOutlookReadModel> result = snapshot is null
            ? new ServiceFailed<MarketOutlookReadModel>(
                GetMarketOutlookSnapshotQuery.ErrorId,
                $"No Market Outlook snapshot is available for {request.ContractId} on or before {request.ValueDate:yyyy-MM-dd}.")
            : new ServiceOk<MarketOutlookReadModel>(snapshot);
        await context.ReplyAsync(
            query.Subject.ThreadId, GetMarketOutlookSnapshotQuery.Verb, result).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
