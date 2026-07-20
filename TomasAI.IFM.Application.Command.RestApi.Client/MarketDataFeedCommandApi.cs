using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// create market data feed command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class MarketDataFeedCommandApi(ICommandService commandSvc) : IMarketDataFeedCommandApi
{
    const string FuturesTickDataController = "FuturesTickData";
    const string FuturesOptionTickDataController = "FuturesOptionTickData";
    const string FuturesOptionQuoteDataController = "FuturesOptionQuoteData";
    const string MarketDataFeedController = "MarketDataFeed";
    const string FuturesBarDataController = "FuturesBarData";

    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start market data feed
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts, 
        DateOnly valueDate) 
        => await new StartMarketDataFeedCommand([.. IsArgumentNull.Set(futuresContracts)], valueDate, false)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// stop market data feed
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopMarketDataFeedAsync(DateOnly valueDate) 
        => await new StopMarketDataFeedCommand(valueDate)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// reset market data feed
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ResetMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts, 
        DateOnly valueDate) 
        => await new ResetMarketDataFeedCommand( [.. IsArgumentNull.Set(futuresContracts)], valueDate)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// insert futures tick data
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="futuresTickData"></param>
    public async Task<ServiceResult<Guid>> InsertFuturesTickDataAsync(
        FuturesContractV2ReadModel futuresContract, 
        FuturesTickDataV2ReadModel futuresTickData)
        => await new InsertFuturesTickDataCommand( IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresTickData))
            .ExecuteAsync( e => _commandSvc.PostApiCommandAsync(e, FuturesTickDataController) );

    /// <summary>
    /// insert futures option tick data
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="futuresOptionTickData"></param>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(
        FuturesContractV2ReadModel futuresContract, 
        FuturesOptionTickDataV2ReadModel futuresOptionTickData)
        => await new InsertFuturesOptionTickDataCommand(IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresOptionTickData))
            .ExecuteAsync( e => _commandSvc.PostApiCommandAsync(e, FuturesOptionTickDataController) );

    /// <summary>
    /// start futures option tick data streaming
    /// </summary>
    /// <param name="feedId"></param>
    /// <param name="optionContract"></param>
    /// <param name="baseContract"></param>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="riskFreeRate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesOptionTickDataStreamingAsync(
            FeedId feedId,
            FuturesOptionContractReadModel optionContract, 
            FuturesContractV2ReadModel baseContract, 
            DateOnly valueDate, 
            DateOnly maturityDate, 
            double riskFreeRate)
        => await new StartFuturesOptionTickDataStreamingCommand( feedId, IsArgumentNull.Set(optionContract), IsArgumentNull.Set(baseContract),
                valueDate, maturityDate, riskFreeRate)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesOptionTickDataController));

    /// <summary>
    /// stop futures option tick data streaming
    /// </summary>
    /// <param name="feedId"></param>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionTickDataStreamingAsync(FeedId feedId, string contractId)
        => await new StopFuturesOptionTickDataStreamingCommand( feedId!, IsArgumentNull.Set(contractId))
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesOptionTickDataController));

    /// <summary>
    /// delete streaming request id
    /// </summary>
    /// <param name="feedId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteStreamingRequestIdAsync(FeedId feedId)
        => await new DeleteStreamingRequestIdCommand( feedId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

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
        => await new StartFuturesTickDataStreamingCommand( IsArgumentNull.Set(futuresContract), valueDate, resetStream)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesTickDataController));

    /// <summary>
    /// stop futures tick data streaming
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesTickDataStreamingAsync(string contractId, DateOnly valueDate)
        => await new StopFuturesTickDataStreamingCommand ( IsArgumentNull.Set(contractId), valueDate)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesTickDataController));

    /// <summary>
    /// start futures bar data streaming
    /// </summary>
    /// <param name="futuresContracts"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesBarDataStreamingAsync(
        FuturesContractV2ReadModel[] futuresContracts, 
        DateOnly valueDate)
        => await new StartFuturesBarDataStreamingCommand( IsArgumentNull.Set(futuresContracts), valueDate)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesBarDataController));

    /// <summary>
    /// stop futures bar data streaming
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesBarDataStreamingAsync(DateOnly valueDate)
        => await new StopFuturesBarDataStreamingCommand(valueDate)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesBarDataController));

    /// <summary>
    /// enable trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> EnableTradeLiveFeedAsync(int orderId, int tradeId)
        => await new TurnTradeLiveFeedOnCommand( orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DisableTradeLiveFeedAsync(
        int orderId, 
        int tradeId)
        => await new TurnTradeLiveFeedOffCommand( orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// add trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddTradeLiveFeedAsync(int orderId, int tradeId)
        => await new AddTradeLiveFeedCommand( orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// remove trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedAsync(int orderId, int tradeId)
        => await new RemoveTradeLiveFeedCommand(orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// remove trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedsAsync(int orderId)
        => await new RemoveTradeLiveFeedsCommand( orderId )
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e , FuturesTickDataController));

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
        => await new InsertFuturesEodDataCommand(valueDate,  futuresTickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e , MarketDataFeedController));

    /// <summary>
    /// insert vix futures eod data
    /// </summary>
    /// <param name="vixFuturesTickData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel vixFuturesTickData)
        => await new InsertVixFuturesEodDataCommand( IsArgumentNull.Set(vixFuturesTickData))
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e , MarketDataFeedController));

    /// <summary>
    /// insert futures bar data
    /// </summary>
    /// <param name="futuresBarData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData)
        => await new InsertFuturesBarDataCommand( IsArgumentNull.Set(futuresBarData))
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// insert futures closing price
    /// </summary>
    /// <param name="id"></param>
    /// <param name="closingPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesClosingPriceAsync(
        FuturesDataId id,
        decimal closingPrice)
        => await new InsertFuturesClosingPriceCommand( IsArgumentNull.Set(id), closingPrice)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// delete futures bar data
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteFuturesBarDataAsync(FuturesBarDataId id)
        => await new DeleteFuturesBarDataCommand(id)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataFeedController));

    /// <summary>
    /// insert futures option quote data
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="contractId"></param>
    /// <param name="quoteData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionQuoteDataAsync(int quoteId, string contractId, QuoteData quoteData)
          => await new InsertFuturesOptionQuoteDataCommand(quoteId, contractId, quoteData)
                   .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, FuturesOptionQuoteDataController));

    /// <summary>
    /// start futures option quote data streaming
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="futuresOptionQuotes"></param>
    /// <param name="futuresOptionContracts" ></param>
    /// <returns></returns>
    public async  Task<ServiceResult<Guid>> StartFuturesOptionQuoteDataStreamingAsync(
        int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts)
          => await new StartFuturesOptionQuoteDataStreamingCommand(quoteId,  futuresOptionQuotes , futuresOptionContracts)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesOptionQuoteDataController));

    /// <summary>
    /// stop futures option quote data streaming
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionQuoteDataStreamingAsync(int quoteId)
        => await new StopFuturesOptionQuoteDataStreamingCommand(quoteId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FuturesOptionQuoteDataController));
}
