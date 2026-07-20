namespace TomasAI.IFM.Shared.Application.Commands;

public class ApplicationUriPath
{
    public const string Start = "/api/application/start";
    public const string Shutdown = "/api/application/shutdown";
}

public class FundUriPath
{
    public const string Create = "/api/fund/create";
    public const string AddOrderToFund = "/api/fund/order/add";
    public const string AddTradeToFundOrder = "/api/fund/order/trade/add";
    public const string ChangeFundOrderTradeState = "/api/fund/order/trade/state/change";
    public const string RemoveOrderFromFund = "/api/fund/order/remove";
    public const string RemoveTradeFromFundOrder = "/api/fund/order/trade/remove";
    public const string CloseFundOrder = "/api/fund/order/close";
    public const string GenerateFundMaxProfit = "/api/fund/maxprofit/generate";
}


public class FundTransactionUriPath
{
    public const string ChangeBalance = "/api/fund/transaction/balance/change";
    public const string ChangeTradePnl = "/api/fund/transaction/tradepnl/change";
    public const string Create = "/api/fund/transaction/create";
    public const string CreateTransactions = "/api/fund/transactions/create";
    public const string ProcessEndOfDay = "/api/fund/transaction/endofday/process";

}

public class FuturesBarDataUriPath
{
    public const string StartStreaming = "/api/futures/bardata/streaming/start";
    public const string StopStreaming = "/api/futures/bardata/streaming/stop";
    public const string Insert = "/api/futures/bardata/streaming/insert";
    public const string Delete = "/api/futures/bardata/streaming/delete";
}

public class FuturesItiTrendUriPath
{
    public const string BuildModel = "/api/futures/iti/trend/model/build";
    public const string LoadDeltaModelData = "/api/futures/iti/trend/model/delta/load";
    public const string LoadClassModelData = "/api/futures/iti/trend/model/class/load";
    public const string TrainDeltaModel = "/api/futures/iti/trend/model/delta/train";
    public const string TrainClassModel = "/api/futures/iti/trend/model/class/train";
}

public class FuturesOptionQuoteUriPath
{
    public const string StartStreaming = "/api/futures/option/quote/streaming/start";
    public const string StopStreaming = "/api/futures/option/quote/streaming/stop";
    public const string Insert = "/api/futures/option/quote/streaming/insert";
}


public class FuturesOptionTickUriPath
{
    public const string StartStreaming = "/api/futures/option/tick/streaming/start";
    public const string StopStreaming = "/api/futures/option/tick/streaming/stop";
    public const string Insert = "/api/futures/option/tick/streaming/insert";
}

public class FuturesTickUriPath
{
    public const string StartStreaming = "/api/futures/tick/streaming/start";
    public const string StopStreaming = "/api/futures/tick/streaming/stop";
    public const string Insert = "/api/futures/tick/streaming/insert";
}
public class FuturesOptionTradeUriPath
{
    public const string Open = "/api/futures/option/trade/open";
    public const string Close = "/api/futures/option/trade/close";
    public const string Delete = "/api/futures/option/trade/delete";
    public const string DeleteOptionTrades = "/api/futures/option/trades/delete";
    public const string PlaceOrder = "/api/futures/option/trade/order/place";
    public const string ProcessEndOfDay = "/api/futures/option/trade/endofday/process";
    public const string ChangeDistributionStatistics = "/api/futures/option/trade/DistributionStatistics/change";
    public const string ChangeLegData = "/api/futures/option/trade/leg/data/change";
    public const string Snapshot = "/api/futures/option/trade/snapshot";
    public const string UpdateDailyProfitTarget = "/api/futures/option/trade/dailyprofittarget/update";
    public const string InsertSpreadData = "/api/futures/option/trade/spread/data/insert";
    public const string InsertSpreadBarData = "/api/futures/option/trade/spread/bardata/insert";
    public const string DeleteSpreadBarData = "/api/futures/option/trade/spread/bardata/delete";
}



public class MarketDataAnalyticsUriPath
{
    public const string StartFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/start";
    public const string StopFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/stop";
    public const string GenerateFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/Generate";
    public const string GenerateFuturesRsiDailySignal = "/api/marketdata/Analytics/Futures/RsiSignal/Daily/Generate";
    public const string UpdateFuturesTradeSignal = "/api/marketdata/Analytics/Futures/TradeSignal/update";
    public const string GenerateFuturesTdiSignal = "/api/marketdata/Analytics/Futures/TdiSignal/Generate";
    public const string GenerateFuturesItiSignal = "/api/marketdata/Analytics/Futures/ItiSignal/Generate";
    public const string SetFuturesItiSignalHoldTrade = "/api/marketdata/Analytics/Futures/ItiSignal/HoldTrade/Set";
    public const string ClearFuturesItiSignalHoldTrade = "/api/marketdata/Analytics/Futures/ItiSignal/HoldTrade/Clear";
    public const string GenerateFuturesAtrSignal = "/api/marketdata/Analytics/Futures/AtrSignal/Generate";
    public const string GenerateFuturesAtrSignalFromIntraDayData = "/api/marketdata/Analytics/Futures/AtrSignal/IntraDayData/Generate";
    public const string GenerateFuturesAdxSignal = "/api/marketdata/Analytics/Futures/AdxSignal/Generate";
    public const string GenerateFuturesMacdSignal = "/api/marketdata/Analytics/Futures/MacdSignal/Generate";
}

public class MarketDataUriPath
{
    public const string AddFuturesContract = "/api/marketdata/futures/contract/add";
    public const string ChangeFuturesContract = "/api/marketdata/futures/contract/change";
    public const string UpdateFuturesContract = "/api/marketdata/futures/contract/update";
    public const string RemoveFuturesContract = "/api/marketdata/futures/contract/remove";
    public const string AddFuturesOptionContract = "/api/marketdata/futures/option/contract/add";
    public const string AddFuturesOptionContracts = "/api/marketdata/futures/option/contracts/add";
    public const string ChangeFuturesOptionContract = "/api/marketdata/futures/option/contract/change";
    public const string RemoveFuturesOptionContract = "/api/marketdata/futures/option/contract/remove";
    public const string AddYieldCurveRate = "/api/marketdata/yieldcurverate/add";
    public const string ChangeYieldCurveRate = "/api/marketdata/yieldcurverate/change";
    public const string RemoveYieldCurveRate = "/api/marketdata/yieldcurverate/remove";
    public const string ImportYieldCurveRates = "/api/marketdata/yieldcurverates/import";

}

public class MarketDataFeedUriPath
{
    public const string StartMarketDataFeed = "/api/marketdata/feed/start";
    public const string StopMarketDataFeed = "/api/marketdata/feed/stop";
    public const string ResetMarketDataFeed = "/api/marketdata/feed/reset";
    public const string InsertFuturesEodData = "/api/marketdata/futures/eod/insert";
    public const string InsertFuturesTickData = "/api/marketdata/futures/tick/insert";
    public const string InsertFuturesOptionTickData = "/api/marketdata/futures/option/tick/insert";
    public const string InsertVixFuturesEodData = "/api/marketdata/futures/eod/vix/insert";
    public const string InsertFuturesBarData = "/api/marketdata/futures/bardata/insert";
    public const string DeleteFuturesBarData = "/api/marketdata/futures/bardata/delete";
    public const string InsertFuturesOptionQuoteData = "/api/marketdata/futures/option/quote/insert";
    public const string InsertFuturesClosingPrice = "/api/marketdata/futures/closingprice/insert";
    public const string TurnTradeLiveFeedOn = "/api/marketdata/livefeed/turn/on";
    public const string TurnTradeLiveFeedOff = "/api/marketdata/livefeed/turn/off";
    public const string AddTradeLiveFeed = "/api/marketdata/livefeed/trade/add";
    public const string RemoveTradeLiveFeed = "/api/marketdata/livefeed/trade/remove";
    public const string HaltTradeLiveFeed = "/api/marketdata/livefeed/trade/halt";
    public const string DeleteStreamingRequestId = "/api/marketdata/streaming/requestid/delete";
    public const string StartFuturesOptionTickDataStreaming = "/api/marketdata/futures/option/tick/streaming/start";
    public const string StopFuturesOptionTickDataStreaming = "/api/marketdata/futures/option/tick/streaming/stop";
    public const string StartFuturesTickDataStreaming = "/api/marketdata/futures/tick/streaming/start";
    public const string StopFuturesTickDataStreaming = "/api/marketdata/futures/tick/streaming/stop";
    public const string StartFuturesBarDataStreaming = "/api/marketdata/futures/bardata/streaming/start";
    public const string StopFuturesBarDataStreaming = "/api/marketdata/futures/bardata/streaming/stop";
    public const string EnableTradeLiveFeed = "/api/marketdata/livefeed/trade/enable";
    public const string DisableTradeLiveFeed = "/api/marketdata/livefeed/trade/disable";
    public const string RemoveTradeLiveFeeds = "/api/marketdata/livefeeds/trade/remove";
    public const string StartFuturesOptionQuoteDataStreaming = "/api/marketdata/futures/option/quote/streaming/start";
    public const string StopFuturesOptionQuoteDataStreaming = "/api/marketdata/futures/option/quote/streaming/stop";
}

public class OptionPricerUriPath
{
    public const string InsertSpreadDistribution = "/api/optionpricer/spreaddistribution/insert";
    public const string DeleteSpreadDistribution = "/api/optionpricer/spreaddistribution/delete";
    public const string SubmitSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/submit";
    public const string CompleteSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/complete";
    public const string FailSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/failed";
    public const string ClearSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/clear";
    public const string DeleteSpreadDistributionJobsInProgress = "/api/optionpricer/spreaddistribution/job/inprogress/delete";
}

public class OptionTradeUriPath
{
    public const string Open = "/api/option/trade/open";
    public const string Close = "/api/option/trade/close";
    public const string Delete = "/api/option/trade/delete";
    public const string DeleteOptionTrades = "/api/option/trades/delete";
    public const string PlaceOrder = "/api/option/trade/order/place";
    public const string ProcessEndOfDay = "/api/option/trade/endofday/process";
    public const string ChangeDistributionStatistics = "/api/option/trade/DistributionStatistics/change";
    public const string ChangeLegData = "/api/option/trade/leg/data/change";
    public const string Snapshot = "/api/option/trade/snapshot";
    public const string UpdateDailyProfitTarget = "/api/option/trade/dailyprofittarget/update";
    public const string InsertSpreadData = "/api/option/trade/spread/data/insert";
    public const string InsertSpreadBarData = "/api/option/trade/spread/bardata/insert";
    public const string DeleteSpreadBarData = "/api/option/trade/spread/bardata/delete";
}

public class ReferenceUriPath
{
    public const string ChangeEconomicCalendar = "/api/reference/economiccalendar/change";
    public const string RemoveEconomicCalendar = "/api/reference/economiccalendar/remove";
    public const string AddEconomicCalendar = "/api/reference/economiccalendar/add";
    public const string ImportEconomicCalendars = "/api/reference/economiccalendars/import";
    public const string AddLookupType = "/api/reference/lookuptype/add";
    public const string ChangeLookupType = "/api/reference/lookuptype/change";
    public const string RemoveLookupType = "/api/reference/lookuptype/remove";
}

public class TradePlacementUriPath
{
    public const string Signal = "/api/trade/placement/signal";
    public const string Start = "/api/trade/placement/start";
    public const string Stop = "/api/trade/placement/stop";
}

public class TradePlanUriPath
{
    public const string Update = "/api/trade/plan/update";
    public const string UpdateTradePlanForwardLossLimit = "/api/trade/plan/forwardlosslimit/update";
    public const string ClearTradePlanForwardLossLimit = "/api/trade/plan/forwardlosslimit/clear";
    public const string RemoveTradePlan = "/api/trade/plan/remove";
    public const string UpdateIronCondorTradePlan = "/api/trade/plan/ironcondor/update";
    public const string UpdateForwardLossLimit = "/api/trade/plan/forwardlosslimit/update";
    public const string ClearForwardLossLimit = "/api/trade/plan/forwardlosslimit/clear";
}

public class SystemAdminUriPath
{
    public const string BackupDatabase = "/api/systemadmin/backup";
}
