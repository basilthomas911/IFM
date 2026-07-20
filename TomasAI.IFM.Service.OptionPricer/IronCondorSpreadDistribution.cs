using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Service.OptionPricer;

public class IronCondorSpreadDistribution(
    ITradeQueryApi tradeQuery,
    ITradeCommandApi tradeCommand,
    IOptionSpreadPricer creditSpreadPricer,
    IMarketDataFeedQueryApi mktDataFeedQuery,
    IMarketDataQueryApi mktDataQuery) : ISpreadDistributionServiceApi
{
    readonly ITradeQueryApi _tradeQuery = IsArgumentNull.Set( tradeQuery);
    readonly ITradeCommandApi _tradeCommand = IsArgumentNull.Set( tradeCommand);
    readonly IOptionSpreadPricer _creditSpreadPricer = IsArgumentNull.Set(creditSpreadPricer);
    readonly IMarketDataFeedQueryApi _mktDataFeedQuery = IsArgumentNull.Set(mktDataFeedQuery);
    readonly IMarketDataQueryApi _mktDataQuery = IsArgumentNull.Set(mktDataQuery);

    public Task CreateOptionSpreadPricerAsync() => throw new NotImplementedException();
    public Task DestroyOptionSpreadPriceAsync() =>  throw new NotImplementedException();

    public async Task<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync(SpreadDistributionJobReadModel e)
    {
        // get option trade ...
        var optionTrade = (await _tradeQuery.GetOptionTradeAsync(e.OrderId, e.TradeId)).Value;
        if (optionTrade is null) 
            return new ServiceFailed<SpreadDistributionJobReadModel>(2000, $"SpreadDistributionJobFailed: Spread Trade {e.TradeId} not found");

        // has current trade position been generated?...
        var pcs = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, e.TradeStatus, e.ValueDate);
        var ccs = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, e.TradeStatus, e.ValueDate);
        if (pcs is null || ccs is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2000, $"SpreadDistributionJobFailed: Trade Positions not generated");

        // get iron condor market data...
        var md = (await _mktDataQuery.GetIronCondorMarketDataAsync(
            underlyingContractId: optionTrade.UnderlyingContractId,
            shortPutOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Put) ?? string.Empty,
            longPutOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Put) ?? string.Empty,
            shortCallOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Call) ?? string.Empty,
            longCallOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Call) ?? string.Empty,
            startDate: e.ValueDate,
            endDate: optionTrade.MaturityDate,
            marketType: MarketType.Futures,
            currencyType: CurrencyType.USD)).Value;
        if (md is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2001, $"SpreadDistributionJobFailed: Unable to load Iron Condor Market data");

        // validate returned iron condor market data...
        if (md.UnderlyingContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2002, $"SpreadDistributionJobFailed: Underlying Futures contract {optionTrade.UnderlyingContractId} not found");
        if (md.ShortPutOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2003, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Put Credit Spread Short Option contract not found");
        if (md.LongPutOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2004, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Put Credit Spread Long Option contract not found");
        if (md.ShortCallOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2005, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Call Credit Spread Short Option contract not found");
        if (md.LongCallOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2006, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Call Credit Spread Long Option contract not found");

        // get risk free rate...
        if (md.RiskFreeRate == 0.0) 
            return new ServiceFailed<SpreadDistributionJobReadModel>(2007, $"SpreadDistributionJobFailed:Risk Free Rate not found");
        var riskFreeRate = md.RiskFreeRate; // < 0.02 ? 0.02 : md.RiskFreeRate;
        var rateOfReturn = riskFreeRate;

        // get days to maturity...
        //if (md.TradingDays == 0) return new ServiceFailed<SpreadDistributionJobReadModel>(2008, $"SpreadDistributionJobFailed: Trading Days not found");
        var daysToMaturity = Math.Min(48, md.TradingDays);


        // get iron condor market data feed prices...
        var mdf = (await _mktDataFeedQuery.GetIronCondorMarketDataFeedAsync(
            underlyingContractId: optionTrade.UnderlyingContractId,
            shortPutOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Put) ?? string.Empty,
            longPutOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Put) ?? string.Empty,
            shortCallOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Call) ?? string.Empty,
            longCallOptionContractId: optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Call) ?? string.Empty,
            valueDate: e.ValueDate)).Value;
        if (mdf is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2009, $"SpreadDistributionJobFailed: Unable to load Iron Condor Market data feed");

        var assetPrice = Convert.ToDouble(mdf.AssetPrice);
        var optCalc = new OptionCalculator(e.ValueDate, optionTrade.MaturityDate);

        (double BidPrice, double AskPrice) shortPutOptionData;
        if (mdf.ShortPutOptionData is null)
        {
            var shortPutOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.ShortPutOptionContract.ContractId);
            if (shortPutOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Short Put Option Leg data not found");
            shortPutOptionData = (Convert.ToDouble(shortPutOptionLegData.BidPrice),  Convert.ToDouble(shortPutOptionLegData.AskPrice));
        }
        else
            shortPutOptionData = (Convert.ToDouble(mdf.ShortPutOptionData.BidPrice), Convert.ToDouble(mdf.ShortPutOptionData.AskPrice));
        var shortPutGreeks = optCalc.GetOptionGreeks(OptionTypeName.Put, assetPrice, md.ShortPutOptionContract.StrikePrice, (shortPutOptionData.BidPrice + shortPutOptionData.AskPrice) / 2, riskFreeRate);
        var shortPutImpliedVol = shortPutGreeks.ImpliedVolatility;
        var shortPutBid = Convert.ToDecimal(shortPutOptionData.BidPrice);
        var shortPutAsk = Convert.ToDecimal(shortPutOptionData.AskPrice);
        var shortPutStrike = md.ShortPutOptionContract.StrikePrice;

        (double BidPrice, double AskPrice) longPutOptionData;
        if (mdf.LongPutOptionData is null)
        {
            var longPutOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.LongPutOptionContract.ContractId);
            if (longPutOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Long Put Option Leg data not found");
            longPutOptionData = (Convert.ToDouble(longPutOptionLegData.BidPrice), Convert.ToDouble(longPutOptionLegData.AskPrice));
        }
        else
            longPutOptionData = (Convert.ToDouble(mdf.LongPutOptionData.BidPrice), Convert.ToDouble(mdf.LongPutOptionData.AskPrice));
        var longPutGreeks = optCalc.GetOptionGreeks(OptionTypeName.Put, assetPrice, md.LongPutOptionContract.StrikePrice, (longPutOptionData.BidPrice + longPutOptionData.AskPrice) / 2, riskFreeRate);
        var longPutImpliedVol = longPutGreeks.ImpliedVolatility;
        var longPutBid = Convert.ToDecimal(longPutOptionData.BidPrice);
        var longPutAsk = Convert.ToDecimal(longPutOptionData.AskPrice);
        var longPutStrike = md.LongPutOptionContract.StrikePrice;
        var pcsArgs = new CreditSpreadPricerArgs
        (
            TradeId: e.TradeId,
            TradeType: e.TradeType,
            TradeStatus: e.TradeStatus,
            ValueDate: e.ValueDate,
            OptionStyle: OptionStyle.American,
            OptionType: OptionType.Put,
            DaysToMaturity: daysToMaturity,
            AssetPrice: Convert.ToDecimal(assetPrice),
            RiskFreeRate: riskFreeRate,
            ShortBid: Convert.ToDecimal(shortPutBid),
            ShortAsk: Convert.ToDecimal(shortPutAsk),
            ShortStrike: shortPutStrike,
            ShortImpliedVolatility: shortPutImpliedVol,
            LongBid: Convert.ToDecimal(longPutBid),
            LongAsk: Convert.ToDecimal(longPutAsk),
            LongStrike: longPutStrike,
            LongImpliedVolatility: longPutImpliedVol,
            RateOfReturn: rateOfReturn
        );

        (double BidPrice, double AskPrice) shortCallOptionData ;
        if (mdf.ShortCallOptionData is null)
        {
            var shortCallOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData.Get(md.ShortCallOptionContract.ContractId);
            if (shortCallOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Short Call Option Leg data not found");
            shortCallOptionData = (Convert.ToDouble(shortCallOptionLegData.BidPrice), Convert.ToDouble(shortCallOptionLegData.AskPrice));
        }
        else
            shortCallOptionData = (Convert.ToDouble(mdf.ShortCallOptionData.BidPrice), Convert.ToDouble(mdf.ShortCallOptionData.AskPrice));
        var shortCallGreeks = optCalc.GetOptionGreeks(OptionTypeName.Call, assetPrice, md.ShortCallOptionContract.StrikePrice, (shortCallOptionData.BidPrice + shortCallOptionData.AskPrice) / 2, riskFreeRate);
        var shortCallImpliedVol = shortCallGreeks.ImpliedVolatility;
        var shortCallBid = Convert.ToDecimal(shortCallOptionData.BidPrice);
        var shortCallAsk = Convert.ToDecimal(shortCallOptionData.AskPrice);
        var shortCallStrike = md.ShortCallOptionContract.StrikePrice;

        (double BidPrice, double AskPrice) longCallOptionData;
        if (mdf.LongCallOptionData is null)
        {
            var longCallOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.LongCallOptionContract.ContractId);
            if (longCallOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, $"SpreadDistributionJobFailed: {e.OrderId}/{e.TradeId} Long Call Option Leg data not found");
            longCallOptionData = (Convert.ToDouble(longCallOptionLegData.BidPrice), Convert.ToDouble(longCallOptionLegData.AskPrice));
        }
        else
            longCallOptionData = (Convert.ToDouble(mdf.LongCallOptionData.BidPrice), Convert.ToDouble(mdf.LongCallOptionData.AskPrice));
        var longCallGreeks = optCalc.GetOptionGreeks(OptionTypeName.Call, assetPrice, md.LongCallOptionContract.StrikePrice, (longCallOptionData.BidPrice + longCallOptionData.AskPrice) / 2, riskFreeRate);
        var longCallImpliedVol = longCallGreeks.ImpliedVolatility;
        var longCallBid = Convert.ToDecimal(longCallOptionData.BidPrice);
        var longCallAsk = Convert.ToDecimal(longCallOptionData.AskPrice);
        var longCallStrike = md.LongCallOptionContract.StrikePrice;

        var ccsArgs = new CreditSpreadPricerArgs
        (
            TradeId: e.TradeId,
            TradeType: e.TradeType,
            TradeStatus: e.TradeStatus,
            ValueDate: e.ValueDate,
            OptionStyle: OptionStyle.American,
            OptionType: OptionType.Call,
            DaysToMaturity: daysToMaturity,
            AssetPrice: Convert.ToDecimal(assetPrice),
            RiskFreeRate: riskFreeRate,
            ShortBid: Convert.ToDecimal(shortCallBid),
            ShortAsk: Convert.ToDecimal(shortCallAsk),
            ShortStrike: shortCallStrike,
            ShortImpliedVolatility: shortCallImpliedVol,
            LongBid: Convert.ToDecimal(longCallBid),
            LongAsk: Convert.ToDecimal(longCallAsk),
            LongStrike: longCallStrike,
            LongImpliedVolatility: longCallImpliedVol,
            RateOfReturn: rateOfReturn
        );

        pcsArgs.LossFactor = pcs.OTMProbability > ccs.OTMProbability ? 0 : 1;
        ccsArgs.LossFactor = pcsArgs.LossFactor == 1 ? 0 : 1;
        try
        {
            // price iron condor via gpu option pricer...
            var (PutSpreadResult, CallSpreadResult, Duration) = _creditSpreadPricer.PriceIronCondor(pcsArgs, ccsArgs);
            var quantity = optionTrade.OptionLegs?.GetQuantity(OptionLegAction.Short, OptionType.Put) ?? 0;
            var multiplier = 50;
            var tradingDays = Convert.ToInt32(optionTrade.MaturityDate.DayNumber - optionTrade.TradeDate.DayNumber);
            var expiryDays = Convert.ToInt32(optionTrade.MaturityDate.DayNumber - e.ValueDate.DayNumber);
            var tradePnl = optionTrade.TradePositions?.GetTradePnl() ?? 0m;
            var pcsOpen = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.Open);
            var ccsOpen = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.Open);

            var spreadDivergence = Math.Abs(pcs.OTMProbability - ccs.OTMProbability);
            var putDelta =  Math.Abs(shortPutGreeks.Delta);
            var callDelta = Math.Abs(shortCallGreeks.Delta);
  
            // calculate put spread forward price...
            var pcsSpreadDistValues = new ProbabilityValueCollection(PutSpreadResult);
            var pcsSpreadDist = pcsSpreadDistValues.SetForwardPrice(OptionType.Put, expiryDays, tradingDays,  pcsArgs.LossFactor, putDelta, Math.Abs(pcs.NetSpread));

            // calculate call spread forward price...
            var ccsSpreadDistValues = new ProbabilityValueCollection(CallSpreadResult);
            var callSpreadDist = ccsSpreadDistValues.SetForwardPrice(OptionType.Call, expiryDays, tradingDays, ccsArgs.LossFactor, callDelta, Math.Abs(ccs.NetSpread));

            // calculate loss probability and set to spread distribution side that is at risk...
            var putLossProbability = default(LossProbabilityDataModel);
            var callLossProbability = default(LossProbabilityDataModel);
            var lossProbability = new LossProbability(pcsSpreadDistValues.SpreadValues, ccsSpreadDistValues.SpreadValues, Convert.ToDouble(optionTrade.TradeLimit?.MaxLoss));
            var pcsNetSpread = Math.Abs(pcs.NetSpread - (pcsOpen?.NetSpread ?? 0));
            var ccsNetSpread = Math.Abs(ccs.NetSpread - (ccsOpen?.NetSpread ?? 0));
            var putSpreadPnl = lossProbability.GetExpectedPnlValues(OptionType.Put, quantity, multiplier, Convert.ToDouble(pcsNetSpread)).ToList();
            var callSpreadPnl = lossProbability.GetExpectedPnlValues(OptionType.Call, quantity, multiplier, Convert.ToDouble(ccsNetSpread)).ToList();
            if (pcsArgs.LossFactor == 1)
            {
                putLossProbability = tradePnl < 0.0m ? lossProbability.ToViewModel(putSpreadPnl, callSpreadPnl) : LossProbability.Empty;
                callLossProbability = LossProbability.Empty;
            }
            else
            {
                putLossProbability = LossProbability.Empty;
                callLossProbability = tradePnl < 0.0m ? lossProbability.ToViewModel(putSpreadPnl, callSpreadPnl) : LossProbability.Empty; 
            }
            var putSpreadType = optionTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
            var pcsViewModel = pcsSpreadDist.ToViewModel(e.TradeId, putSpreadType, e.TradeStatus, e.ValueDate, putLossProbability);
            pcsViewModel = pcsViewModel with { ShortVolatility = pcsArgs.ShortImpliedVolatility };
            pcsViewModel = pcsViewModel with { LongVolatility = pcsArgs.LongImpliedVolatility };

            var callSpreadType = optionTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
            var ccsViewModel = callSpreadDist.ToViewModel(e.TradeId, callSpreadType, e.TradeStatus, e.ValueDate, callLossProbability);
            ccsViewModel = ccsViewModel with { ShortVolatility = ccsArgs.ShortImpliedVolatility };
            ccsViewModel = ccsViewModel with { LongVolatility = ccsArgs.LongImpliedVolatility };
            e.PutSpreadDistribution = pcsViewModel;
            e.CallSpreadDistribution = ccsViewModel;
            e.Duration = Duration;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<SpreadDistributionJobReadModel>(2009, $"SpreadDistributionJobFailed: {ex.Message}");
        }

        // change spread trade distribution status...
        await _tradeCommand.ChangeDistributionStatisticsAsync(
            orderId: e.OrderId,
            tradeId: e.TradeId,
            tradeType: e.TradeType,
            valueDate: e.ValueDate,
            tradeStatus: e.TradeStatus,
            putSpreadDistribution: e.PutSpreadDistribution,
            callSpreadDistribution: e.CallSpreadDistribution);
        return new ServiceOk<SpreadDistributionJobReadModel>(e);
    }
}
