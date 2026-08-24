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
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parsers.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {subject}");

        var query = IsArgumentNull.Set(parser(message));
        context.SetMessageInfo(subject.ThreadId, subject.Verb, new ActorMessageInfo(message, query));
        return query;
    }

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parsers = new()
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
        var dispatchContext = context;
        cancellationToken.ThrowIfCancellationRequested();
        switch (query)
        {
            case GetStopLossLimitQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<TradePlanStopLossLimitReadModel>(
                        await new GetStopLossLimitQueryHandler(ActorContext.DbFactory.TradeDb).ExecuteAsync(q)));
                break;
            case GetTradePlanForwardLossRatiosQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<TradePlanForwardLossRatioReadModel[]>(
                        await new GetTradePlanForwardLossRatiosQueryHandler(ActorContext.DbFactory.TradeDb).ExecuteAsync(q)));
                break;
            case GetTradePlanForwardLossRatioQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<TradePlanForwardLossRatioReadModel>(
                        await new GetTradePlanForwardLossRatioQueryHandler(ActorContext.DbFactory.TradeDb).ExecuteAsync(q)));
                break;
            case GetTradePlansQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<TradePlanReadModel[]>(
                        await new GetTradePlansQueryHandler(ActorContext.DbFactory.TradeDb).ExecuteAsync(q)));
                break;
            case GetIronCondorForwardDeltaQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<IronCondorForwardDeltaDataModel>(
                        await new GetIronCondorForwardDeltaQueryHandler(ActorContext.DbFactory.MarketDataDb).ExecuteAsync(q)));
                break;
            case GetTradePlanForwardLossLimitQuery q:
                await context.ReplyAsync(q.Subject.ThreadId, q.Subject.Verb,
                    new ServiceResult<TradePlanForwardLossLimitReadModel>(
                        await new GetTradePlanForwardLossLimitQueryHandler(ActorContext.DbFactory.TradeDb).ExecuteAsync(q)));
                break;
            default:
                throw new InvalidOperationException($"Unable to process {ActorName} query: {query.GetType().Name}");
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<TradePlanQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
    {
        switch (query)
        {
            case GetStopLossLimitQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanStopLossLimitReadModel>(query.ErrorCode, ex.Message));
                break;
            case GetTradePlanForwardLossRatiosQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanForwardLossRatioReadModel[]>(query.ErrorCode, ex.Message));
                break;
            case GetTradePlanForwardLossRatioQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanForwardLossRatioReadModel>(query.ErrorCode, ex.Message));
                break;
            case GetTradePlansQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanReadModel[]>(query.ErrorCode, ex.Message));
                break;
            case GetIronCondorForwardDeltaQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<IronCondorForwardDeltaDataModel>(query.ErrorCode, ex.Message));
                break;
            case GetTradePlanForwardLossLimitQuery:
                await context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanForwardLossLimitReadModel>(query.ErrorCode, ex.Message));
                break;
            default:
                await context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message));
                break;
        }
    }
}
