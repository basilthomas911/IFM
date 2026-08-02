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

public sealed class ActorMarketDataFeedCommandApi(IEventActorContext context)
    : IActorMarketDataFeedCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

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

public sealed class ActorMarketDataFeedCommandApiFactory : IActorMarketDataFeedCommandApiFactory
{
    public IActorMarketDataFeedCommandApi Create(IEventActorContext context)
        => new ActorMarketDataFeedCommandApi(context);
}
