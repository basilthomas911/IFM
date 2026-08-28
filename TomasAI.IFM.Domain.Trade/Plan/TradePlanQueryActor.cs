using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.Trade.Plan;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Provides the TradePlanQueryActor implementation.</summary>
public sealed class TradePlanQueryActor(
    IQueryActorContext<TradePlanQueryActor> actorContext)
    : BaseQueryActor<TradePlanQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    private ITradePlanQueryContext ActorContext =>
        IsArgumentNull.Set(Context as ITradePlanQueryContext, nameof(Context))!;

    public const string ActorName = "TradePlanQuery";

    protected override IQuery ParseMessage(IQueryActorContext<TradePlanQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetStopLossLimitQuery.Verb] = message => message.AsQuery<GetStopLossLimitQuery, TradePlanStopLossLimitReadModel>()!,
        [GetTradePlanForwardLossRatiosQuery.Verb] = message => message.AsQuery<GetTradePlanForwardLossRatiosQuery, TradePlanForwardLossRatioReadModel[]>()!,
        [GetTradePlanForwardLossRatioQuery.Verb] = message => message.AsQuery<GetTradePlanForwardLossRatioQuery, TradePlanForwardLossRatioReadModel>()!,
        [GetTradePlansQuery.Verb] = message => message.AsQuery<GetTradePlansQuery, TradePlanReadModel[]>()!,
        [GetIronCondorForwardDeltaQuery.Verb] = message => message.AsQuery<GetIronCondorForwardDeltaQuery, IronCondorForwardDeltaDataModel>()!,
        [GetTradePlanForwardLossLimitQuery.Verb] = message => message.AsQuery<GetTradePlanForwardLossLimitQuery, TradePlanForwardLossLimitReadModel>()!
    };

    protected override ValueTask ReceiveAsync(IQueryActorContext<TradePlanQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<TradePlanQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(this, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<TradePlanQueryActor,
        IQueryActorContext<TradePlanQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetStopLossLimitQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetStopLossLimitQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<TradePlanStopLossLimitReadModel>(
                    await new GetStopLossLimitQueryHandler(actor.ActorContext.DbFactory.TradeDb).ExecuteAsync(value)));
        },
        [typeof(GetTradePlanForwardLossRatiosQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetTradePlanForwardLossRatiosQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<TradePlanForwardLossRatioReadModel[]>(
                    await new GetTradePlanForwardLossRatiosQueryHandler(actor.ActorContext.DbFactory.TradeDb).ExecuteAsync(value)));
        },
        [typeof(GetTradePlanForwardLossRatioQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetTradePlanForwardLossRatioQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<TradePlanForwardLossRatioReadModel>(
                    await new GetTradePlanForwardLossRatioQueryHandler(actor.ActorContext.DbFactory.TradeDb).ExecuteAsync(value)));
        },
        [typeof(GetTradePlansQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetTradePlansQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<TradePlanReadModel[]>(
                    await new GetTradePlansQueryHandler(actor.ActorContext.DbFactory.TradeDb).ExecuteAsync(value)));
        },
        [typeof(GetIronCondorForwardDeltaQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetIronCondorForwardDeltaQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<IronCondorForwardDeltaDataModel>(
                    await new GetIronCondorForwardDeltaQueryHandler(actor.ActorContext.DbFactory.MarketDataDb).ExecuteAsync(value)));
        },
        [typeof(GetTradePlanForwardLossLimitQuery)] = static async (actor, context, query, _) =>
        {
            var value = (GetTradePlanForwardLossLimitQuery)query;
            await context.ReplyAsync(value.Subject.ThreadId, value.Subject.Verb,
                new ServiceResult<TradePlanForwardLossLimitReadModel>(
                    await new GetTradePlanForwardLossLimitQueryHandler(actor.ActorContext.DbFactory.TradeDb).ExecuteAsync(value)));
        }
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<TradePlanQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
