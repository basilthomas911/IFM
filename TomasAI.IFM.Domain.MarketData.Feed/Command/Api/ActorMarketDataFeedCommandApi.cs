using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Api;

/// <summary>
/// Sends Market Data Feed commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// Operations construct strongly typed subjects and entity identifiers for feed lifecycle, streaming, and
/// persistence commands before dispatching them through the captured <see cref="IEventActorContext"/>.
/// Create one instance per actor context through <see cref="ActorMarketDataFeedCommandApiFactory"/>.
/// </remarks>
public sealed class ActorMarketDataFeedCommandApi(IEventActorContext context)
    : IActorMarketDataFeedCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    /// <summary>
    /// Sends the turn trade live feed off command and awaits its typed actor reply.
    /// </summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOffAsync(
        Guid commandId,
        int orderId,
        int tradeId,
        DateOnly valueDate)
    {
        var entityId = new TradeLiveFeedId(orderId, tradeId, valueDate);
        TurnTradeLiveFeedOffCommand command = new(orderId, tradeId, valueDate)
        {
            CommandId = commandId,
            Subject = Subject<TurnTradeLiveFeedOffCommand>(
                TurnTradeLiveFeedOffCommand.Actor,
                TurnTradeLiveFeedOffCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = TurnTradeLiveFeedOffCommand.ErrorId
        };
        return RequestAsync<TurnTradeLiveFeedOffCommand, TradeLiveFeedId>(command);
    }

    /// <summary>
    /// Sends the turn trade live feed on command and awaits its typed actor reply.
    /// </summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOnAsync(
        Guid commandId,
        int orderId,
        int tradeId,
        DateOnly valueDate)
    {
        var entityId = new TradeLiveFeedId(orderId, tradeId, valueDate);
        TurnTradeLiveFeedOnCommand command = new(orderId, tradeId, valueDate)
        {
            CommandId = commandId,
            Subject = Subject<TurnTradeLiveFeedOnCommand>(
                TurnTradeLiveFeedOnCommand.Actor,
                TurnTradeLiveFeedOnCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = TurnTradeLiveFeedOnCommand.ErrorId
        };
        return RequestAsync<TurnTradeLiveFeedOnCommand, TradeLiveFeedId>(command);
    }

    /// <summary>
    /// Sends the stop futures bar data streaming command and awaits its typed actor reply.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> StopFuturesBarDataStreamingAsync(DateOnly valueDate)
    {
        var entityId = new FuturesBarDataStreamingId(valueDate);
        StopFuturesBarDataStreamingCommand command = new(valueDate)
        {
            Subject = Subject<StopFuturesBarDataStreamingCommand>(
                StopFuturesBarDataStreamingCommand.Actor,
                StopFuturesBarDataStreamingCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = StopFuturesBarDataStreamingCommand.ErrorId
        };
        return RequestAsync<StopFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(command);
    }

    /// <summary>
    /// Sends the stop futures option tick data streaming command and awaits its typed actor reply.
    /// </summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <param name="contractId">The contract identifier.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> StopFuturesOptionTickDataStreamingAsync(
        Guid commandId,
        FuturesOptionTickEntityId entityId,
        string contractId)
    {
        StopFuturesOptionTickDataStreamingCommand command = new(entityId, contractId)
        {
            CommandId = commandId,
            Subject = Subject<StopFuturesOptionTickDataStreamingCommand>(
                StopFuturesOptionTickDataStreamingCommand.Actor,
                StopFuturesOptionTickDataStreamingCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = StopFuturesOptionTickDataStreamingCommand.ErrorId
        };
        return RequestAsync<StopFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(command);
    }

    /// <summary>
    /// Sends the start futures tick data streaming command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresContract">The futures contract associated with the operation.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="resetStream">Whether the existing stream should be reset.</param>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> StartFuturesTickDataStreamingAsync(
        FuturesContractV2ReadModel futuresContract,
        DateOnly valueDate,
        bool resetStream,
        FuturesDataId entityId)
    {
        StartFuturesTickDataStreamingCommand command = new(futuresContract, valueDate, resetStream)
        {
            Subject = Subject<StartFuturesTickDataStreamingCommand>(
                StartFuturesTickDataStreamingCommand.Actor,
                StartFuturesTickDataStreamingCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = StartFuturesTickDataStreamingCommand.ErrorId
        };
        return RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(command);
    }

    /// <summary>
    /// Sends the start futures bar data streaming command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresContracts">The futures contracts associated with the operation.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> StartFuturesBarDataStreamingAsync(
        FuturesContractV2ReadModel[] futuresContracts,
        DateOnly valueDate,
        FuturesBarDataStreamingId entityId)
    {
        StartFuturesBarDataStreamingCommand command = new(futuresContracts, valueDate)
        {
            Subject = Subject<StartFuturesBarDataStreamingCommand>(
                StartFuturesBarDataStreamingCommand.Actor,
                StartFuturesBarDataStreamingCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = StartFuturesBarDataStreamingCommand.ErrorId
        };
        return RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(command);
    }

    /// <summary>
    /// Sends the start futures option tick data streaming command and awaits its typed actor reply.
    /// </summary>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <param name="contract">The futures-option contract.</param>
    /// <param name="baseContract">The underlying futures contract.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="maturityDate">The option maturity date.</param>
    /// <param name="riskFreeRate">The annualized risk-free rate.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> StartFuturesOptionTickDataStreamingAsync(
        Guid commandId,
        FuturesOptionTickEntityId entityId,
        FuturesOptionContractReadModel contract,
        FuturesContractV2ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate)
    {
        StartFuturesOptionTickDataStreamingCommand command = new(
            entityId,
            contract,
            baseContract,
            valueDate,
            maturityDate,
            riskFreeRate)
        {
            CommandId = commandId,
            Subject = Subject<StartFuturesOptionTickDataStreamingCommand>(
                StartFuturesOptionTickDataStreamingCommand.Actor,
                StartFuturesOptionTickDataStreamingCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = StartFuturesOptionTickDataStreamingCommand.ErrorId
        };
        return RequestAsync<StartFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(command);
    }

    /// <summary>
    /// Sends the insert futures bar data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresBarData">The futures bar data to persist.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData)
    {
        InsertFuturesBarDataCommand command = new(futuresBarData)
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject<InsertFuturesBarDataCommand>(
                InsertFuturesBarDataCommand.Actor,
                InsertFuturesBarDataCommand.Verb,
                futuresBarData.Id),
            EntityId = futuresBarData.Id,
            ErrorCode = InsertFuturesBarDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesBarDataCommand, FuturesBarDataId>(command);
    }

    /// <summary>
    /// Sends the delete streaming request ID command and awaits its typed actor reply.
    /// </summary>
    /// <param name="feedId">The streaming feed identifier.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> DeleteStreamingRequestIdAsync(FeedId feedId)
    {
        DeleteStreamingRequestIdCommand command = new(feedId)
        {
            Subject = Subject<DeleteStreamingRequestIdCommand>(
                DeleteStreamingRequestIdCommand.Actor,
                DeleteStreamingRequestIdCommand.Verb,
                feedId),
            EntityId = feedId,
            ErrorCode = DeleteStreamingRequestIdCommand.ErrorId
        };
        return RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(command);
    }

    /// <summary>
    /// Sends the insert futures option quote data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="quoteId">The quote identifier.</param>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="quoteData">The quote data to persist.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionQuoteDataAsync(
        int quoteId,
        string contractId,
        QuoteData quoteData)
    {
        var entityId = new QuoteId(quoteId);
        InsertFuturesOptionQuoteDataCommand command = new(quoteId, contractId, quoteData)
        {
            Subject = Subject<InsertFuturesOptionQuoteDataCommand>(
                InsertFuturesOptionQuoteDataCommand.Actor,
                InsertFuturesOptionQuoteDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertFuturesOptionQuoteDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesOptionQuoteDataCommand, QuoteId>(command);
    }

    /// <summary>
    /// Sends the insert futures EOD data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="futuresTickData">The futures tick data used by the operation.</param>
    /// <param name="futuresContract">The futures contract associated with the operation.</param>
    /// <param name="eodDataToday">The current EOD data.</param>
    /// <param name="eodDataRange">The historical EOD data used by the calculation.</param>
    /// <param name="normalCurveData">The normal-curve lookup data.</param>
    /// <param name="windowSize">The calculation window size.</param>
    /// <param name="vixEodData">The VIX EOD data used by the calculation.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesEodDataAsync(
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel futuresContract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normalCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
    {
        var entityId = new FuturesEodDataId(futuresContract.ContractId, valueDate);
        InsertFuturesEodDataCommand command = new(
            valueDate,
            futuresTickData,
            futuresContract,
            eodDataToday,
            eodDataRange,
            normalCurveData,
            windowSize,
            vixEodData)
        {
            Subject = Subject<InsertFuturesEodDataCommand>(
                InsertFuturesEodDataCommand.Actor,
                InsertFuturesEodDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertFuturesEodDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesEodDataCommand, FuturesEodDataId>(command);
    }

    /// <summary>
    /// Sends the insert VIX futures EOD data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresTickData">The futures tick data used by the operation.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertVixFuturesEodDataAsync(
        FuturesTickDataV2ReadModel futuresTickData)
    {
        var entityId = new FuturesEodDataId(futuresTickData.ContractId, futuresTickData.ValueDate);
        InsertVixFuturesEodDataCommand command = new(futuresTickData)
        {
            Subject = Subject<InsertVixFuturesEodDataCommand>(
                InsertVixFuturesEodDataCommand.Actor,
                InsertVixFuturesEodDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertVixFuturesEodDataCommand.ErrorId
        };
        return RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(command);
    }

    /// <summary>
    /// Sends the insert futures tick data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresContract">The futures contract associated with the operation.</param>
    /// <param name="futuresTickData">The futures tick data used by the operation.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesTickDataAsync(
        FuturesContractV2ReadModel futuresContract,
        FuturesTickDataV2ReadModel futuresTickData)
    {
        var entityId = new FuturesDataId(futuresContract.ContractId, futuresTickData.ValueDate);
        InsertFuturesTickDataCommand command = new(futuresContract, futuresTickData)
        {
            Subject = Subject<InsertFuturesTickDataCommand>(
                InsertFuturesTickDataCommand.Actor,
                InsertFuturesTickDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertFuturesTickDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesTickDataCommand, FuturesDataId>(command);
    }

    /// <summary>
    /// Sends the insert futures option tick price data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="underlyingContract">The underlying futures contract.</param>
    /// <param name="optionContract">The futures-option tick data.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickPriceDataAsync(
        FuturesContractV2ReadModel underlyingContract,
        FuturesOptionTickDataV2ReadModel optionContract)
    {
        var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
        InsertFuturesOptionTickPriceDataCommand command = new(underlyingContract, optionContract)
        {
            Subject = Subject<InsertFuturesOptionTickPriceDataCommand>(
                InsertFuturesOptionTickPriceDataCommand.Actor,
                InsertFuturesOptionTickPriceDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertFuturesOptionTickPriceDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesOptionTickPriceDataCommand, FuturesOptionTickEntityId>(command);
    }

    /// <summary>
    /// Sends the insert futures option tick data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="underlyingContract">The underlying futures contract.</param>
    /// <param name="optionContract">The futures-option tick data.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickDataAsync(
        FuturesContractV2ReadModel underlyingContract,
        FuturesOptionTickDataV2ReadModel optionContract)
    {
        var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
        InsertFuturesOptionTickDataCommand command = new(underlyingContract, optionContract)
        {
            Subject = Subject<InsertFuturesOptionTickDataCommand>(
                InsertFuturesOptionTickDataCommand.Actor,
                InsertFuturesOptionTickDataCommand.Verb,
                entityId),
            EntityId = entityId,
            ErrorCode = InsertFuturesOptionTickDataCommand.ErrorId
        };
        return RequestAsync<InsertFuturesOptionTickDataCommand, FuturesOptionTickEntityId>(command);
    }

    async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await _context.RequestAsync<TCommand, TEntityId>(command);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage);
        return result;
    }

    static ActorSubject Subject<TCommand>(string actor, string verb, IActorEntityId entityId)
        => new(ActorType.Command, actor, verb, entityId.Format());
}

/// <summary>
/// Creates Market Data Feed command APIs bound to a running event actor.
/// </summary>
public sealed class ActorMarketDataFeedCommandApiFactory : IActorMarketDataFeedCommandApiFactory
{
    /// <summary>
    /// Creates a command API that dispatches through the supplied actor context.
    /// </summary>
    /// <param name="context">The actor context used for command request/reply messaging.</param>
    /// <returns>A context-bound Market Data Feed command API.</returns>
    public IActorMarketDataFeedCommandApi Create(IEventActorContext context)
        => new ActorMarketDataFeedCommandApi(context);
}
