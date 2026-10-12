using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using LossProbability = TomasAI.IFM.Framework.OptionPricer.Black76.LossProbability;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services.Contracts;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services;

/// <summary>
/// Prices an Iron Condor spread distribution by loading the trade, market data, and live feed prices,
/// computing Greeks and implied volatilities for all four option legs, running the unified pricer via
/// <see cref="OptionSpreadPricer.PriceIronCondor"/>, calculating forward prices
/// and loss probabilities, and persisting the resulting put/call spread distributions.
/// Returns <see cref="ServiceFailed{T}"/> with error codes 2000–2009 on validation or pricing failure.
/// </summary>
internal class IronCondorSpreadDistributionJobService : ISpreadDistributionJobService
{
    /// <summary>The event that triggered this pricing job, containing the <see cref="SpreadDistributionJobReadModel"/> payload.</summary>
    readonly SpreadDistributionJobSubmittedEvent _jobSubmittedEvent;

    /// <summary>The actor context used to query trade, market data, and market data feed information.</summary>
    readonly IEventActorContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="IronCondorSpreadDistributionJobService"/>.
    /// </summary>
    /// <param name="e">The submitted event containing the spread distribution job details.</param>
    /// <param name="state">The event-actor state for accessing shared services.</param>
    /// <param name="context">
    /// The actor context used to retrieve option trades, market data, market data feed prices,
    /// and to persist the computed distribution statistics.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="e"/>, <paramref name="state"/>, or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public IronCondorSpreadDistributionJobService(
        SpreadDistributionJobSubmittedEvent e,
        IEventActorContext context,
        IEventActorContext tradeCommandApi)
    {
        _jobSubmittedEvent = IsArgumentNull.Set(e);
        _context = IsArgumentNull.Set(context);
        IsArgumentNull.Check(tradeCommandApi);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality is required for persisted observation deduplication, exact contract strikes and defined zero/uninitialized sentinels; an epsilon would change these decisions.")]     /// <summary>
    /// Executes the Iron Condor spread distribution pricing job.
    /// </summary>
    /// <returns>
    /// A <see cref="ServiceOk{T}"/> wrapping the enriched <see cref="SpreadDistributionJobReadModel"/>
    /// (including put/call spread distributions and pricing duration) on success; otherwise a
    /// <see cref="ServiceFailed{T}"/> containing a numeric error code and descriptive message.
    /// </returns>
    public async ValueTask<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync()
    {
        var e = _jobSubmittedEvent.SpreadDistributionJob;

        // get option trade ...
        var optionTrade = await (_context.GetOptionTradeAsync(e.OrderId, e.TradeId)).ConfigureAwait(false);
        if (optionTrade is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2000, string.Concat("SpreadDistributionJobFailed: Spread Trade ", e.OrderId, ":", e.TradeId, " not found"));

        // has current trade position been generated?...
        var pcs = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, e.TradeStatus, e.ValueDate);
        var ccs = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, e.TradeStatus, e.ValueDate);
        if (pcs is null || ccs is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2000, "SpreadDistributionJobFailed: Trade Positions not generated");

        var (md, mdf) = await LoadMarketInputsAsync(optionTrade, e).ConfigureAwait(false);
        if (md is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2001, "SpreadDistributionJobFailed: Unable to load Iron Condor Market data");
        if (mdf is null)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2009, "SpreadDistributionJobFailed: Unable to load Iron Condor Market data feed");

        // validate returned iron condor market data...
        if (md.UnderlyingContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2002, string.Concat("SpreadDistributionJobFailed: Underlying Futures contract ", optionTrade.UnderlyingContractId, " not found"));
        if (md.ShortPutOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2003, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, ":", e.TradeId, " Put Credit Spread Short Option contract not found"));
        if (md.LongPutOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2004, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, ":", e.TradeId, " Put Credit Spread Long Option contract not found"));
        if (md.ShortCallOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2005, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, ":", e.TradeId, " Call Credit Spread Short Option contract not found"));
        if (md.LongCallOptionContract is null) return new ServiceFailed<SpreadDistributionJobReadModel>(2006, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, ":", e.TradeId, " Call Credit Spread Long Option contract not found"));

        return PricePreparedTrade(e, optionTrade, pcs, ccs, md, mdf);
    }

    /// <summary>Solves each leg's IV from its observed midpoint and prices the prepared put/call distributions using the existing American futures option convention.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Pricing failures become the established typed job failure result without escaping the event handler.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Zero risk-free rate is the existing unavailable-input sentinel and must retain exact equality.")]
    static ServiceResult<SpreadDistributionJobReadModel> PricePreparedTrade(SpreadDistributionJobReadModel e,
        OptionTradeReadModel optionTrade, TradePositionReadModel pcs, TradePositionReadModel ccs,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf)
    {
        // get risk free rate...
        if (md.RiskFreeRate == 0.0)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2007, "SpreadDistributionJobFailed:Risk Free Rate not found");
        var riskFreeRate = md.RiskFreeRate;

        // get days to maturity...
        var daysToMaturity = Math.Min(48, md.TradingDays);

        // get iron condor market data feed prices...
        var assetPrice = Convert.ToDouble(mdf.AssetPrice);
        var optCalc = new OptionCalculator();
        var timeToExpiry = (optionTrade.MaturityDate.DayNumber - e.ValueDate.DayNumber) / 365d;
        OptionPricingRequest PricingRequest(OptionSide side, double strike) => new(
            UnderlyingKind.Futures, ExerciseKind.American, PremiumKind.PaidUpfront, side,
            assetPrice, strike, timeToExpiry, riskFreeRate);

        if (PrepareQuotedLegs(optionTrade, e, md, mdf, out var inputs) is { } quoteFailure)
            return quoteFailure;
        var legs = inputs!;

        var shortPutResult = optCalc.ImpliedVolatility(PricingRequest(OptionSide.Put, legs.ShortPut.Strike), (legs.ShortPut.Quote.BidPrice + legs.ShortPut.Quote.AskPrice) / 2);
        var longPutResult = optCalc.ImpliedVolatility(PricingRequest(OptionSide.Put, legs.LongPut.Strike), (legs.LongPut.Quote.BidPrice + legs.LongPut.Quote.AskPrice) / 2);
        var shortCallResult = optCalc.ImpliedVolatility(PricingRequest(OptionSide.Call, legs.ShortCall.Strike), (legs.ShortCall.Quote.BidPrice + legs.ShortCall.Quote.AskPrice) / 2);
        var longCallResult = optCalc.ImpliedVolatility(PricingRequest(OptionSide.Call, legs.LongCall.Strike), (legs.LongCall.Quote.BidPrice + legs.LongCall.Quote.AskPrice) / 2);
        if (!shortPutResult.Success || !longPutResult.Success || !shortCallResult.Success || !longCallResult.Success)
            return new ServiceFailed<SpreadDistributionJobReadModel>(2008, "SpreadDistributionJobFailed: Unable to calculate option Greeks");
        var shortPutGreeks = shortPutResult.Value!.Value;
        var longPutGreeks = longPutResult.Value!.Value;
        var shortCallGreeks = shortCallResult.Value!.Value;
        var longCallGreeks = longCallResult.Value!.Value;

        var shortPutImpliedVol = shortPutGreeks.Volatility;
        var longPutImpliedVol = longPutGreeks.Volatility;
        var shortCallImpliedVol = shortCallGreeks.Volatility;
        var longCallImpliedVol = longCallGreeks.Volatility;

        var pcsArgs = CreateSpreadArgs(e, OptionType.Put, daysToMaturity, assetPrice, riskFreeRate,
            (legs.ShortPut.Bid, legs.ShortPut.Ask, legs.ShortPut.Strike, shortPutImpliedVol), (legs.LongPut.Bid, legs.LongPut.Ask, legs.LongPut.Strike, longPutImpliedVol));
        var ccsArgs = CreateSpreadArgs(e, OptionType.Call, daysToMaturity, assetPrice, riskFreeRate,
            (legs.ShortCall.Bid, legs.ShortCall.Ask, legs.ShortCall.Strike, shortCallImpliedVol), (legs.LongCall.Bid, legs.LongCall.Ask, legs.LongCall.Strike, longCallImpliedVol));

        pcsArgs.LossFactor = pcs.OTMProbability > ccs.OTMProbability ? 0 : 1;
        ccsArgs.LossFactor = pcsArgs.LossFactor == 1 ? 0 : 1;
        try
        {
            CalculateDistributions(e, optionTrade, pcs, ccs, pcsArgs, ccsArgs, shortPutGreeks.Delta, shortCallGreeks.Delta);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<SpreadDistributionJobReadModel>(2009, string.Concat("SpreadDistributionJobFailed: ", ex.Message));
        }

        return new ServiceOk<SpreadDistributionJobReadModel>(e);    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    readonly record struct QuotedLeg((double BidPrice, double AskPrice) Quote, decimal Bid, decimal Ask, double Strike);
    sealed record QuotedLegSet(QuotedLeg ShortPut, QuotedLeg LongPut, QuotedLeg ShortCall, QuotedLeg LongCall);

    /// <summary>Captures all four quotes and decimal premiums in their existing leg order before invoking the option calculator.</summary>
    static ServiceFailed<SpreadDistributionJobReadModel>? PrepareQuotedLegs(OptionTradeReadModel optionTrade,
        SpreadDistributionJobReadModel e, TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf, out QuotedLegSet? inputs)
    {
        inputs = null;
        if (GetShortPutQuote(optionTrade, e, md, mdf, out var shortPutOptionData) is { } shortPutFailure)
            return shortPutFailure;
        var shortPutBid = Convert.ToDecimal(shortPutOptionData.BidPrice);
        var shortPutAsk = Convert.ToDecimal(shortPutOptionData.AskPrice);
        var shortPutStrike = md.ShortPutOptionContract.StrikePrice;

        if (GetLongPutQuote(optionTrade, e, md, mdf, out var longPutOptionData) is { } longPutFailure)
            return longPutFailure;
        var longPutBid = Convert.ToDecimal(longPutOptionData.BidPrice);
        var longPutAsk = Convert.ToDecimal(longPutOptionData.AskPrice);
        var longPutStrike = md.LongPutOptionContract.StrikePrice;
        if (GetShortCallQuote(optionTrade, e, md, mdf, out var shortCallOptionData) is { } shortCallFailure)
            return shortCallFailure;
        var shortCallBid = Convert.ToDecimal(shortCallOptionData.BidPrice);
        var shortCallAsk = Convert.ToDecimal(shortCallOptionData.AskPrice);
        var shortCallStrike = md.ShortCallOptionContract.StrikePrice;

        if (GetLongCallQuote(optionTrade, e, md, mdf, out var longCallOptionData) is { } longCallFailure)
            return longCallFailure;
        var longCallBid = Convert.ToDecimal(longCallOptionData.BidPrice);
        var longCallAsk = Convert.ToDecimal(longCallOptionData.AskPrice);
        var longCallStrike = md.LongCallOptionContract.StrikePrice;

        inputs = new(new(shortPutOptionData, shortPutBid, shortPutAsk, shortPutStrike),
            new(longPutOptionData, longPutBid, longPutAsk, longPutStrike),
            new(shortCallOptionData, shortCallBid, shortCallAsk, shortCallStrike),
            new(longCallOptionData, longCallBid, longCallAsk, longCallStrike));
        return null;
    }

    /// <summary>Uses the shortPut live bid/ask or its existing intraday position quote, retaining the original missing-leg failure contract.</summary>
    static ServiceFailed<SpreadDistributionJobReadModel>? GetShortPutQuote(OptionTradeReadModel optionTrade, SpreadDistributionJobReadModel e,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf,
        out (double BidPrice, double AskPrice) shortPutOptionData)
    {
        shortPutOptionData = default;
        if (mdf.ShortPutOptionData is null)
        {
            var shortPutOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.ShortPutOptionContract.ContractId);
            if (shortPutOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, "/", e.TradeId, " Short Put Option Leg data not found"));
            shortPutOptionData = (Convert.ToDouble(shortPutOptionLegData.BidPrice), Convert.ToDouble(shortPutOptionLegData.AskPrice));
        }
        else
            shortPutOptionData = (Convert.ToDouble(mdf.ShortPutOptionData.BidPrice), Convert.ToDouble(mdf.ShortPutOptionData.AskPrice));
        return null;
    }

    /// <summary>Uses the longPut live bid/ask or its existing intraday position quote, retaining the original missing-leg failure contract.</summary>
    static ServiceFailed<SpreadDistributionJobReadModel>? GetLongPutQuote(OptionTradeReadModel optionTrade, SpreadDistributionJobReadModel e,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf,
        out (double BidPrice, double AskPrice) longPutOptionData)
    {
        longPutOptionData = default;
        if (mdf.LongPutOptionData is null)
        {
            var longPutOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.LongPutOptionContract.ContractId);
            if (longPutOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, "/", e.TradeId, " Long Put Option Leg data not found"));
            longPutOptionData = (Convert.ToDouble(longPutOptionLegData.BidPrice), Convert.ToDouble(longPutOptionLegData.AskPrice));
        }
        else
            longPutOptionData = (Convert.ToDouble(mdf.LongPutOptionData.BidPrice), Convert.ToDouble(mdf.LongPutOptionData.AskPrice));
        return null;
    }

    /// <summary>Uses the shortCall live bid/ask or its existing intraday position quote, retaining the original missing-leg failure contract.</summary>
    static ServiceFailed<SpreadDistributionJobReadModel>? GetShortCallQuote(OptionTradeReadModel optionTrade, SpreadDistributionJobReadModel e,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf,
        out (double BidPrice, double AskPrice) shortCallOptionData)
    {
        shortCallOptionData = default;
        if (mdf.ShortCallOptionData is null)
        {
            var shortCallOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData.Get(md.ShortCallOptionContract.ContractId);
            if (shortCallOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, "/", e.TradeId, " Short Call Option Leg data not found"));
            shortCallOptionData = (Convert.ToDouble(shortCallOptionLegData.BidPrice), Convert.ToDouble(shortCallOptionLegData.AskPrice));
        }
        else
            shortCallOptionData = (Convert.ToDouble(mdf.ShortCallOptionData.BidPrice), Convert.ToDouble(mdf.ShortCallOptionData.AskPrice));
        return null;
    }

    /// <summary>Uses the longCall live bid/ask or its existing intraday position quote, retaining the original missing-leg failure contract.</summary>
    static ServiceFailed<SpreadDistributionJobReadModel>? GetLongCallQuote(OptionTradeReadModel optionTrade, SpreadDistributionJobReadModel e,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel md,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel mdf,
        out (double BidPrice, double AskPrice) longCallOptionData)
    {
        longCallOptionData = default;
        if (mdf.LongCallOptionData is null)
        {
            var longCallOptionLegData = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.IntraDay, e.ValueDate)?.OptionLegData?.Get(md.LongCallOptionContract.ContractId);
            if (longCallOptionLegData is null)
                return new ServiceFailed<SpreadDistributionJobReadModel>(2006, string.Concat("SpreadDistributionJobFailed: ", e.OrderId, "/", e.TradeId, " Long Call Option Leg data not found"));
            longCallOptionData = (Convert.ToDouble(longCallOptionLegData.BidPrice), Convert.ToDouble(longCallOptionLegData.AskPrice));
        }
        else
            longCallOptionData = (Convert.ToDouble(mdf.LongCallOptionData.BidPrice), Convert.ToDouble(mdf.LongCallOptionData.AskPrice));
        return null;
    }

    /// <summary>Starts independent history and live-feed actor reads together, then awaits both in their original order.</summary>
    async Task<(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.IronCondorMarketDataReadModel Data,
        TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.IronCondorMarketDataFeedReadModel Feed)> LoadMarketInputsAsync(
        OptionTradeReadModel optionTrade, SpreadDistributionJobReadModel e)
    {
        var shortPutContractId = optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Put) ?? string.Empty;
        var longPutContractId = optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Put) ?? string.Empty;
        var shortCallContractId = optionTrade.OptionLegs?.GetContractId(OptionLegAction.Short, OptionType.Call) ?? string.Empty;
        var longCallContractId = optionTrade.OptionLegs?.GetContractId(OptionLegAction.Long, OptionType.Call) ?? string.Empty;

        // Historical market data and the live feed are independent once the trade is loaded.
        // Start both actor requests before awaiting either one.
        var marketDataPending = _context.GetIronCondorMarketDataAsync(
            underlyingContractId: optionTrade.UnderlyingContractId,
            shortPutOptionContractId: shortPutContractId,
            longPutOptionContractId: longPutContractId,
            shortCallOptionContractId: shortCallContractId,
            longCallOptionContractId: longCallContractId,
            startDate: e.ValueDate,
            endDate: optionTrade.MaturityDate,
            marketType: MarketType.Futures,
            currencyType: CurrencyType.USD);
        var marketDataFeedPending = _context.GetIronCondorMarketDataFeedAsync(
            underlyingContractId: optionTrade.UnderlyingContractId,
            shortPutOptionContractId: shortPutContractId,
            longPutOptionContractId: longPutContractId,
            shortCallOptionContractId: shortCallContractId,
            longCallOptionContractId: longCallContractId,
            valueDate: e.ValueDate);

        return (await marketDataPending.ConfigureAwait(false), await marketDataFeedPending.ConfigureAwait(false));
    }

    /// <summary>Captures the exact American credit-spread pricing inputs with decimal premiums and the observed per-leg implied volatilities.</summary>
    static CreditSpreadPricerArgs CreateSpreadArgs(SpreadDistributionJobReadModel e, OptionType optionType,
        int daysToMaturity, double assetPrice, double riskFreeRate,
        (decimal Bid, decimal Ask, double Strike, double Volatility) shortLeg,
        (decimal Bid, decimal Ask, double Strike, double Volatility) longLeg)
    {
        return new CreditSpreadPricerArgs
        (
            TradeId: e.TradeId,
            TradeType: e.TradeType,
            TradeStatus: e.TradeStatus,
            ValueDate: e.ValueDate,
            OptionStyle: OptionStyle.American,
            OptionType: optionType,
            DaysToMaturity: daysToMaturity,
            AssetPrice: Convert.ToDecimal(assetPrice),
            RiskFreeRate: riskFreeRate,
            ShortBid: shortLeg.Bid,
            ShortAsk: shortLeg.Ask,
            ShortStrike: shortLeg.Strike,
            ShortImpliedVolatility: shortLeg.Volatility,
            LongBid: longLeg.Bid,
            LongAsk: longLeg.Ask,
            LongStrike: longLeg.Strike,
            LongImpliedVolatility: longLeg.Volatility,
            RateOfReturn: riskFreeRate
        );

    }

    /// <summary>Prices both spreads, applies the existing delta-based forward-price rule and populates their distribution snapshots.</summary>
    static void CalculateDistributions(SpreadDistributionJobReadModel e, OptionTradeReadModel optionTrade,
        TradePositionReadModel pcs, TradePositionReadModel ccs, CreditSpreadPricerArgs pcsArgs,
        CreditSpreadPricerArgs ccsArgs, double shortPutDelta, double shortCallDelta)
    {
            // price iron condor via gpu option pricer...
            var (PutSpreadResult, CallSpreadResult, Duration) = OptionSpreadPricer.PriceIronCondor(pcsArgs, ccsArgs);
            var quantity = optionTrade.OptionLegs?.GetQuantity(OptionLegAction.Short, OptionType.Put) ?? 0;
            var multiplier = 50;
            var tradingDays = Convert.ToInt32(optionTrade.MaturityDate.DayNumber - optionTrade.TradeDate.DayNumber);
            var expiryDays = Convert.ToInt32(optionTrade.MaturityDate.DayNumber - e.ValueDate.DayNumber);
            var tradePnl = optionTrade.TradePositions?.GetTradePnl() ?? 0m;
            var pcsOpen = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Put, TradeStatus.Open);
            var ccsOpen = optionTrade.TradePositions?.Get(optionTrade.TradeType, OptionType.Call, TradeStatus.Open);

            _=Math.Abs(pcs.OTMProbability - ccs.OTMProbability);
            var putDelta = Math.Abs(shortPutDelta);
            var callDelta = Math.Abs(shortCallDelta);

            // calculate put spread forward price...
            var pcsSpreadDistValues = new ProbabilityValueCollection(PutSpreadResult);
            var pcsSpreadDist = pcsSpreadDistValues.SetForwardPrice(OptionType.Put, expiryDays, tradingDays, pcsArgs.LossFactor, putDelta, Math.Abs(pcs.NetSpread));

            // calculate call spread forward price...
            var ccsSpreadDistValues = new ProbabilityValueCollection(CallSpreadResult);
            var callSpreadDist = ccsSpreadDistValues.SetForwardPrice(OptionType.Call, expiryDays, tradingDays, ccsArgs.LossFactor, callDelta, Math.Abs(ccs.NetSpread));

            // calculate loss probability and set to spread distribution side that is at risk...
            var (putLossProbability, callLossProbability) = CalculateLossProbabilities(optionTrade, pcs, ccs, pcsArgs,
                pcsSpreadDistValues, ccsSpreadDistValues, quantity, multiplier, tradePnl, pcsOpen, ccsOpen);
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

    /// <summary>When trade PnL is negative, calculates loss probability from both spread distributions and assigns it only to the selected risk side.</summary>
    static (LossProbabilityDataModel Put, LossProbabilityDataModel Call) CalculateLossProbabilities(
        OptionTradeReadModel optionTrade, TradePositionReadModel pcs, TradePositionReadModel ccs,
        CreditSpreadPricerArgs pcsArgs, ProbabilityValueCollection pcsSpreadDistValues,
        ProbabilityValueCollection ccsSpreadDistValues, int quantity, int multiplier, decimal tradePnl,
        TradePositionReadModel? pcsOpen, TradePositionReadModel? ccsOpen)
    {
            var putLossProbability = default(LossProbabilityDataModel);
            var callLossProbability = default(LossProbabilityDataModel);
            var lossProbability = new LossProbability(pcsSpreadDistValues.Values, ccsSpreadDistValues.Values, Convert.ToDouble(optionTrade.TradeLimit?.MaxLoss, System.Globalization.CultureInfo.InvariantCulture));
            var pcsNetSpread = Math.Abs(pcs.NetSpread - (pcsOpen?.NetSpread ?? 0));
            var ccsNetSpread = Math.Abs(ccs.NetSpread - (ccsOpen?.NetSpread ?? 0));
            var calculatedLossProbability = tradePnl < 0.0m
                ? lossProbability.Calculate(quantity, multiplier, Convert.ToDouble(pcsNetSpread), Convert.ToDouble(ccsNetSpread))
                : LossProbability.Empty;
            if (pcsArgs.LossFactor == 1)
            {
                putLossProbability = calculatedLossProbability;
                callLossProbability = LossProbability.Empty;
            }
            else
            {
                putLossProbability = LossProbability.Empty;
                callLossProbability = calculatedLossProbability;
            }
        return (putLossProbability, callLossProbability);
    }


}
