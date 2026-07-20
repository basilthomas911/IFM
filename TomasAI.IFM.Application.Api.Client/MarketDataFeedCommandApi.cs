using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// create market data feed command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class MarketDataFeedCommandApi(ICommandServiceApi commandSvc) : IMarketDataFeedCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start market data feed
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts, 
        DateOnly valueDate) 
        => await new StartMarketDataFeedParameter([.. IsArgumentNull.Set(futuresContracts)], valueDate, false, StartMarketDataFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StartMarketDataFeed, e));

    /// <summary>
    /// stop market data feed
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopMarketDataFeedAsync(DateOnly valueDate) 
        => await new StopMarketDataFeedParameter(valueDate, StopMarketDataFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StopMarketDataFeed, e));

    /// <summary>
    /// reset market data feed
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ResetMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts, 
        DateOnly valueDate) 
        => await new ResetMarketDataFeedParameter([.. IsArgumentNull.Set(futuresContracts)], valueDate, ResetMarketDataFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.ResetMarketDataFeed, e));

    /// <summary>
    /// insert futures tick data
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="futuresTickData"></param>
    public async Task<ServiceResult<Guid>> InsertFuturesTickDataAsync(
        FuturesContractV2ReadModel futuresContract, 
        FuturesTickDataV2ReadModel futuresTickData)
        => await new InsertFuturesTickDataParameter(IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresTickData), InsertFuturesTickDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesTickData, e));

    /// <summary>
    /// insert futures option tick data
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="futuresOptionTickData"></param>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(
        FuturesContractV2ReadModel futuresContract, 
        FuturesOptionTickDataV2ReadModel futuresOptionTickData)
        => await new InsertFuturesOptionTickDataParameter(IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresOptionTickData), InsertFuturesOptionTickDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesOptionTickData, e));

    /// <summary>
    /// start futures option tick data streaming
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="optionContract"></param>
    /// <param name="baseContract"></param>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="riskFreeRate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesOptionTickDataStreamingAsync(
            FuturesOptionTickEntityId entityId,
            FuturesOptionContractReadModel optionContract, 
            FuturesContractV2ReadModel baseContract, 
            DateOnly valueDate, 
            DateOnly maturityDate, 
            double riskFreeRate)
        => await new StartFuturesOptionTickDataStreamingParameter(entityId, IsArgumentNull.Set(optionContract), IsArgumentNull.Set(baseContract),
                valueDate, maturityDate, riskFreeRate, StartFuturesOptionTickDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StartFuturesOptionTickDataStreaming, e));

    /// <summary>
    /// stop futures option tick data streaming
    /// </summary>
    /// <param name="feedId"></param>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionTickDataStreamingAsync(FuturesOptionTickEntityId entityId, string contractId)
        => await new StopFuturesOptionTickDataStreamingParameter(entityId, IsArgumentNull.Set(contractId), StopFuturesOptionTickDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StopFuturesOptionTickDataStreaming, e));

    /// <summary>
    /// delete streaming request id
    /// </summary>
    /// <param name="feedId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteStreamingRequestIdAsync(FeedId feedId)
        => await new DeleteStreamingRequestIdParameter(feedId, DeleteStreamingRequestIdCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.DeleteStreamingRequestId, e));

    /// <summary>
    /// start futures tick data streaming
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesTickDataStreamingAsync(
        FuturesContractV2ReadModel futuresContract, 
        DateOnly valueDate, 
        bool resetStream)
        => await new StartFuturesTickDataStreamingParameter(IsArgumentNull.Set(futuresContract), valueDate, resetStream, StartFuturesTickDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StartFuturesTickDataStreaming, e));

    /// <summary>
    /// stop futures tick data streaming
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesTickDataStreamingAsync(string contractId, DateOnly valueDate)
        => await new StopFuturesTickDataStreamingParameter(IsArgumentNull.Set(contractId), valueDate, StopFuturesTickDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StopFuturesTickDataStreaming, e));

    /// <summary>
    /// start futures bar data streaming
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesBarDataStreamingAsync(
        FuturesContractV2ReadModel[] futuresContracts, 
        DateOnly valueDate)
        => await new StartFuturesBarDataStreamingParameter(IsArgumentNull.Set(futuresContracts), valueDate, StartFuturesBarDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StartFuturesBarDataStreaming, e));

    /// <summary>
    /// stop futures bar data streaming
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesBarDataStreamingAsync(DateOnly valueDate)
        => await new StopFuturesBarDataStreamingParameter(valueDate, StopFuturesBarDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StopFuturesBarDataStreaming, e));

    /// <summary>
    /// enable trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> EnableTradeLiveFeedAsync(int orderId, int tradeId)
        => await new EnableTradeLiveFeedParameter(orderId, tradeId, TurnTradeLiveFeedOnCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.EnableTradeLiveFeed, e));

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DisableTradeLiveFeedAsync(
        int orderId, 
        int tradeId)
        => await new DisableTradeLiveFeedParameter(orderId, tradeId, TurnTradeLiveFeedOffCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.DisableTradeLiveFeed, e));

    /// <summary>
    /// add trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate)
        => await new AddTradeLiveFeedParameter(orderId, tradeId, valueDate, AddTradeLiveFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.AddTradeLiveFeed, e));

    /// <summary>
    /// remove trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate)
        => await new RemoveTradeLiveFeedParameter(orderId, tradeId, valueDate, RemoveTradeLiveFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.RemoveTradeLiveFeed, e));

    /// <summary>
    /// remove trade live feeds
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedsAsync(int orderId)
        => await new RemoveTradeLiveFeedsParameter(orderId, RemoveTradeLiveFeedsCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.RemoveTradeLiveFeeds, e));

    /// <summary>
    /// halt trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> HaltTradeLiveFeedAsync(int orderId, int tradeId)
        => await new HaltTradeLiveFeedParameter(orderId, tradeId, HaltTradeLiveFeedCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.HaltTradeLiveFeed, e));

    /// <summary>
    /// insert futures eod data
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="futuresTickData"></param>
    /// <param name="contract"></param>
    /// <param name="eodDataToday"></param>
    /// <param name="eodDataRange"></param>
    /// <param name="normCurveData"></param>
    /// <param name="windowSize"></param>
    /// <param name="vixEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesEodDataAsync(
        DateOnly valueDate, 
        FuturesTickDataV2ReadModel futuresTickData, 
        FuturesContractV2ReadModel contract, 
        FuturesEodDataV2ReadModel eodDataToday, 
        ICollection<FuturesEodDataV2ReadModel> eodDataRange, 
        NormalCurveTableReadModel normCurveData, 
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
        => await new InsertFuturesEodDataParameter(valueDate, futuresTickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData, InsertFuturesEodDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesEodData, e));

    /// <summary>
    /// insert vix futures eod data
    /// </summary>
    /// <param name="vixFuturesTickData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel vixFuturesTickData)
        => await new InsertVixFuturesEodDataParameter(IsArgumentNull.Set(vixFuturesTickData), InsertVixFuturesEodDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertVixFuturesEodData, e));

    /// <summary>
    /// insert futures bar data
    /// </summary>
    /// <param name="futuresBarData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData)
        => await new InsertFuturesBarDataParameter(IsArgumentNull.Set(futuresBarData), InsertFuturesBarDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesBarData, e));

    /// <summary>
    /// insert futures closing price
    /// </summary>
    /// <param name="id"></param>
    /// <param name="closingPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesClosingPriceAsync(
        FuturesDataId id,
        decimal closingPrice)
        => await new InsertFuturesClosingPriceParameter(IsArgumentNull.Set(id), closingPrice, InsertFuturesClosingPriceCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesClosingPrice, e));

    /// <summary>
    /// delete futures bar data
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteFuturesBarDataAsync(FuturesBarDataId id)
        => await new DeleteFuturesBarDataParameter(id, DeleteFuturesBarDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.DeleteFuturesBarData, e));

    /// <summary>
    /// insert futures option quote data
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="contractId"></param>
    /// <param name="quoteData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionQuoteDataAsync(int quoteId, string contractId, QuoteData quoteData)
        => await new InsertFuturesOptionQuoteDataParameter(quoteId, contractId, quoteData, InsertFuturesOptionQuoteDataCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.InsertFuturesOptionQuoteData, e));

    /// <summary>
    /// start futures option quote data streaming
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="futuresOptionQuotes"></param>
    /// <param name="futuresOptionContracts" ></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesOptionQuoteDataStreamingAsync(
        int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts)
        => await new StartFuturesOptionQuoteDataStreamingParameter(quoteId, futuresOptionQuotes, futuresOptionContracts, StartFuturesOptionQuoteDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StartFuturesOptionQuoteDataStreaming, e));

    /// <summary>
    /// stop futures option quote data streaming
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionQuoteDataStreamingAsync(int quoteId)
        => await new StopFuturesOptionQuoteDataStreamingParameter(quoteId, StopFuturesOptionQuoteDataStreamingCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataFeedUriPath.StopFuturesOptionQuoteDataStreaming, e));
}
