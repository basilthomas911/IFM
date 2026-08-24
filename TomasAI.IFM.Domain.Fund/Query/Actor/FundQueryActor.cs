using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Fund.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures contract commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FundQueryActor"/> is a specialized command actor designed to handle operations
/// related to futures contracts. It processes commands such as adding, changing, and removing futures contracts,
/// validates the commands, and manages the actor's state. This actor relies on an event-sourced repository for state
/// persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="actorContext">The typed Fund query context.</param>
public class FundQueryActor(IQueryActorContext<FundQueryActor> actorContext)
    : BaseQueryActor<FundQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FundQuery";

    readonly ILogger<FundQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>
    /// Gets the Fund-specific query context supplied when this actor was constructed.
    /// </summary>
    protected IFundQueryContext FundQueryContext { get; } =
        IsArgumentNull.Set(actorContext as IFundQueryContext, nameof(actorContext))!;

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <remarks>This method validates the provided message and resolves it to a specific query based on the
    /// message subject. The resolved query is then stored in the provided context along with additional message
    /// information.</remarks>
    /// <param name="message">The actor message to parse. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="context">The query actor context used to store message-related information. This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <returns>The thread identifier extracted from the message subject.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FundQueryActor> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = messageParser.Invoke(message);
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            verb: msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    /// <remarks>This dictionary enables efficient dispatching and parsing of incoming NATS messages based on
    /// their verb. Each entry associates a specific query verb with a function that converts a NATS message payload
    /// into a strongly typed query object implementing the IQuery interface. The mapping is intended for internal
    /// use in query deserialization and routing scenarios.</remarks>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetClosingFundBalanceQuery.Verb] = msg => msg.AsQuery<GetClosingFundBalanceQuery, FundBalanceReadModel>()!,
        [GetFundBalanceQuery.Verb] = msg => msg.AsQuery<GetFundBalanceQuery, FundBalanceReadModel>()!,
        [GetFundDrawdownBalancesQuery.Verb] = msg => msg.AsQuery<GetFundDrawdownBalancesQuery, FundDrawdownBalancesReadModel>()!,
        [GetFundIdFromOrderIdQuery.Verb] = msg => msg.AsQuery<GetFundIdFromOrderIdQuery, ScalarReadModel<int>>()!,
        [GetFundOrdersQuery.Verb] = msg => msg.AsQuery<GetFundOrdersQuery, FundOrderReadModel[]>()!,
        [GetFundOrderTradesQuery.Verb] = msg => msg.AsQuery<GetFundOrderTradesQuery, FundOrderTradeReadModel[]>()!,
        [GetFundPnlReportQuery.Verb] = msg => msg.AsQuery<GetFundPnlReportQuery, FundPnlReportReadModel>()!,
        [GetFundsQuery.Verb] = msg => msg.AsQuery<GetFundsQuery, FundReadModel[]>()!,
        [GetFundWinLossRatioQuery.Verb] = msg => msg.AsQuery<GetFundWinLossRatioQuery, FundWinLossRatioReadModel>()!,
        [GetOpeningFundBalanceQuery.Verb] = msg => msg.AsQuery<GetOpeningFundBalanceQuery, FundBalanceReadModel>()!,
        [GetFundMaxProfitGeneratedQuery.Verb] = msg => msg.AsQuery<GetFundMaxProfitGeneratedQuery, FundMaxProfitGeneratedReadModel>()!,
    };

    /// <summary>
    /// Asynchronously processes the specified query within the context of the actor, utilizing the provided state and
    /// </summary>
    /// <param name="context"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<FundQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FundQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var queryType = query.GetType();
        if (!_receiveMap.TryGetValue(queryType, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {queryType.Name}");
        await receiveFunc(query, FundQueryContext, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<IQuery, IFundQueryContext, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetClosingFundBalanceQuery)] =  async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetClosingFundBalanceQuery)!;
            var result = await query.GetClosingFundBalanceAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundBalanceReadModel>(new FundBalanceReadModel(result));
            await ctx.ReplyAsync(q.Subject.ThreadId, GetClosingFundBalanceQuery.Verb, serviceResult);
        },
        [typeof(GetFundBalanceQuery)] =  async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundBalanceQuery)!;
            var result = await query.GetFundBalanceAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundBalanceReadModel>(new FundBalanceReadModel(result));
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundBalanceQuery.Verb, serviceResult);
        },
        [typeof(GetFundDrawdownBalancesQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundDrawdownBalancesQuery)!;
            var result = await query.GetFundDrawdownBalancesAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundDrawdownBalancesReadModel>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundDrawdownBalancesQuery.Verb, serviceResult);

        },
        [typeof(GetFundIdFromOrderIdQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundIdFromOrderIdQuery)!;
            var result = await query.GetFundIdFromOrderIdAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResut = new ServiceResult<ScalarReadModel<int>>(new ScalarReadModel<int>(result));
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundIdFromOrderIdQuery.Verb, serviceResut);
        },
        [typeof(GetFundOrdersQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundOrdersQuery)!;
            var result = await query.GetFundOrdersAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundOrderReadModel[]>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundOrdersQuery.Verb, serviceResult);
        },
        [typeof(GetFundOrderTradesQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundOrderTradesQuery)!;
            var result = await query.GetFundOrderTradesAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundOrderTradeReadModel[]>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundOrderTradesQuery.Verb, serviceResult);
        },
        [typeof(GetFundPnlReportQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundPnlReportQuery)!;
            var fundPnlReport = await query.GetFundPnlReportAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundPnlReportReadModel>(fundPnlReport);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundPnlReportQuery.Verb, serviceResult);
        },
        [typeof(GetFundsQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundsQuery)!;
            var result = await query.GetFundsAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundReadModel[]>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundsQuery.Verb, serviceResult);
        },
        [typeof(GetFundWinLossRatioQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundWinLossRatioQuery)!;
            var result = await query.GetFundWinLossRatioAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundWinLossRatioReadModel>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundWinLossRatioQuery.Verb, serviceResult);
        },
        [typeof(GetOpeningFundBalanceQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetOpeningFundBalanceQuery)!;
            var result = await query.GetOpeningFundBalanceAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundBalanceReadModel>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOpeningFundBalanceQuery.Verb, serviceResult);
        },
        [typeof(GetFundMaxProfitGeneratedQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundMaxProfitGeneratedQuery)!;
            var fundMaxProfitGenerated = await query.GetFundMaxProfitGeneratedAsync(ctx, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FundMaxProfitGeneratedReadModel>(fundMaxProfitGenerated);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundMaxProfitGeneratedQuery.Verb, serviceResult);
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <remarks>This method attempts to handle the exception by determining the type of query that caused it
    /// and sending an appropriate error response back to the caller. If the query type is not recognized, an <see
    /// cref="InvalidOperationException"/> is thrown.</remarks>
    /// <param name="context">The context in which the query is being processed. Provides access to actor-specific operations and state.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported or cannot be processed by the actor.</exception>
    protected override async ValueTask OnExceptionAsync(IQueryActorContext<FundQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(ex.Message);
        try
        {
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetClosingFundBalanceQuery 
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundBalanceReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFundBalanceQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundBalanceReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFundDrawdownBalancesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundDrawdownBalancesReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFundIdFromOrderIdQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarReadModel<int>>(query.ErrorCode, ex.Message)),
                _ when query is GetFundOrdersQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundOrderReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetFundOrderTradesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundOrderTradeReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetFundPnlReportQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundPnlReportReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFundsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetFundWinLossRatioQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundWinLossRatioReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetOpeningFundBalanceQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundBalanceReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFundMaxProfitGeneratedQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FundMaxProfitGeneratedReadModel>(query.ErrorCode, ex.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }

}
