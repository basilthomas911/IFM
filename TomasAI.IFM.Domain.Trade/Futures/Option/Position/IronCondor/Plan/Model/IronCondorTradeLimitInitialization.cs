using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Initializes the legacy overall and per-spread limits from captured business inputs.</summary>
/// <remarks>Recovered from CreateIronCondorTrade, quote updates and UpdateTradeLimitValues before commit 281550666.
/// This pure calculation creates proposed data; authoritative application and persistence belong to commands/events.</remarks>
public static class IronCondorTradeLimitInitialization
{
    /// <summary>Calculates spread price = (shortBid + shortAsk)/2 - (longBid + longAsk)/2 in points.</summary>
    /// <param name="shortBid">Short contract bid.</param><param name="shortAsk">Short contract ask.</param>
    /// <param name="longBid">Long contract bid.</param><param name="longAsk">Long contract ask.</param>
    /// <returns>The spread midpoint in points.</returns>
    public static decimal CalculateSpreadPrice(decimal shortBid, decimal shortAsk, decimal longBid, decimal longAsk)
    {
        ValidateQuote(shortBid, shortAsk);
        ValidateQuote(longBid, longAsk);
        return (shortBid + shortAsk) / 2m - (longBid + longAsk) / 2m;
    }

    /// <summary>Calculates spread value = spreadPrice * quantity * cashMultiplier in currency units.</summary>
    /// <param name="spreadPrice">The spread price in points.</param><param name="quantity">Whole strategy contracts, at least one.</param>
    /// <param name="cashMultiplier">Positive currency value per point from contract metadata, replacing legacy hardcoded 50.</param>
    /// <returns>The legacy spread trade value.</returns>
    public static decimal CalculateSpreadValue(decimal spreadPrice, int quantity, decimal cashMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cashMultiplier);
        return spreadPrice * quantity * cashMultiplier;
    }

    /// <summary>Calculates maxLossLimit: short = 2*spreadPrice; long = 2*spreadPrice/8, in points.</summary>
    /// <param name="tradeType">Short or long Iron Condor.</param><param name="spreadPrice">Nonnegative magnitude of actual short-minus-long opening prices; equal prices give zero.</param>
    /// <returns>The spread's loss price limit.</returns>
    public static decimal CalculateMaximumLossPriceLimit(TradeType tradeType, decimal spreadPrice)
        => CalculateBasePriceLimit(tradeType, spreadPrice) / (tradeType == TradeType.ShortIronCondor ? 1m : 8m);

    /// <summary>Calculates minProfitLimit: short = 2*spreadPrice/8; long = 2*spreadPrice, in points.</summary>
    /// <param name="tradeType">Short or long Iron Condor.</param><param name="spreadPrice">Nonnegative magnitude of actual short-minus-long opening prices; equal prices give zero.</param>
    /// <returns>The spread's minimum profit price limit.</returns>
    public static decimal CalculateMinimumProfitPriceLimit(TradeType tradeType, decimal spreadPrice)
        => CalculateBasePriceLimit(tradeType, spreadPrice) / (tradeType == TradeType.ShortIronCondor ? 8m : 1m);

    /// <summary>Calculates maxProfitLimit using the same legacy formula as minProfitLimit.</summary>
    /// <param name="tradeType">Short or long Iron Condor.</param><param name="spreadPrice">Nonnegative magnitude of actual short-minus-long opening prices; equal prices give zero.</param>
    /// <returns>The spread's maximum profit price limit in points.</returns>
    public static decimal CalculateMaximumProfitPriceLimit(TradeType tradeType, decimal spreadPrice)
        => CalculateMinimumProfitPriceLimit(tradeType, spreadPrice);

    /// <summary>Calculates maxProfit = putSpreadValue + callSpreadValue in currency units.</summary>
    /// <param name="putSpreadValue">Captured put spread value.</param><param name="callSpreadValue">Captured call spread value.</param>
    /// <returns>The legacy profit basis; this is not a general expiry-payoff maximum for all condor configurations.</returns>
    public static decimal CalculateMaximumProfit(decimal putSpreadValue, decimal callSpreadValue)
        => putSpreadValue + callSpreadValue;

    /// <summary>Calculates configured maximum loss = -0.02 * fundBalance in currency units.</summary>
    /// <param name="fundBalance">Positive captured fund balance; no default balance is invented.</param>
    /// <returns>A negative legacy loss threshold, distinct from maximum theoretical payoff loss.</returns>
    public static decimal CalculateMaximumLoss(decimal fundBalance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fundBalance);
        return -0.02m * fundBalance;
    }

    /// <summary>Calculates maxReturn = maximumProfit/riskMargin; a zero margin yields zero per legacy behavior.</summary>
    /// <param name="maximumProfit">Profit basis in currency.</param><param name="riskMargin">Captured nonnegative margin in the same currency.</param>
    /// <returns>The dimensionless return ratio.</returns>
    public static decimal CalculateMaximumReturn(decimal maximumProfit, decimal riskMargin)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(riskMargin);
        return riskMargin == 0 ? 0 : maximumProfit / riskMargin;
    }

    /// <summary>Calculates minimum profit target = 0.50*maximumProfit + 2*tradeCommission.</summary>
    /// <param name="maximumProfit">The newly calculated profit basis, not the previous state's value.</param>
    /// <param name="tradeCommission">Nonnegative total opening commission in currency units.</param>
    /// <returns>The legacy target including its round-trip commission allowance.</returns>
    public static decimal CalculateMinimumProfitTarget(decimal maximumProfit, decimal tradeCommission)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tradeCommission);
        return 0.50m * maximumProfit + 2m * tradeCommission;
    }

    /// <summary>Calculates daily target = maximumProfit/daysToExpiry + 2*tradeCommission; nonpositive days yield zero.</summary>
    /// <param name="maximumProfit">The newly calculated profit basis in currency.</param><param name="daysToExpiry">Expiry calendar days.</param>
    /// <param name="tradeCommission">Nonnegative total opening commission.</param><returns>The currency daily target.</returns>
    public static decimal CalculateDailyProfitTarget(decimal maximumProfit, int daysToExpiry, decimal tradeCommission)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tradeCommission);
        return daysToExpiry <= 0 ? 0 : maximumProfit / daysToExpiry + 2m * tradeCommission;
    }

    /// <summary>Creates initialized trade and spread limits, summing spread price limits for the overall trade.</summary>
    /// <param name="tradeId">Positive persisted trade identifier.</param><param name="tradeType">Short or long Iron Condor.</param>
    /// <param name="putSpreadPrice">Nonnegative put spread magnitude in points.</param><param name="callSpreadPrice">Nonnegative call spread magnitude in points.</param>
    /// <param name="quantity">Whole strategy quantity.</param><param name="cashMultiplier">Common validated contract multiplier.</param>
    /// <param name="fundBalance">Captured positive fund balance.</param><param name="riskMargin">Captured nonnegative margin.</param>
    /// <param name="tradeCommission">Total opening commission.</param><param name="daysToExpiry">Expiry calendar days.</param>
    /// <param name="initializedAtUtc">Captured UTC initialization time.</param><param name="initializedBy">Initiating business actor/operator.</param>
    /// <returns>The proposed overall trade limit and two per-spread limits ready for an owning command event.</returns>
    public static (TradeLimitReadModel TradeLimit, TradeTypeLimitReadModel[] SpreadLimits) Initialize(
        int tradeId, TradeType tradeType, decimal putSpreadPrice, decimal callSpreadPrice, int quantity,
        decimal cashMultiplier, decimal fundBalance, decimal riskMargin, decimal tradeCommission,
        int daysToExpiry, DateTime initializedAtUtc, string initializedBy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tradeId);
        if (initializedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC initialization time required.", nameof(initializedAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(initializedBy);
        var put = CreateSpreadLimit(tradeId, tradeType,
            tradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread, putSpreadPrice);
        var call = CreateSpreadLimit(tradeId, tradeType,
            tradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread, callSpreadPrice);
        var profit = CalculateMaximumProfit(CalculateSpreadValue(putSpreadPrice, quantity, cashMultiplier),
            CalculateSpreadValue(callSpreadPrice, quantity, cashMultiplier));
        return (new TradeLimitReadModel
        {
            TradeId = tradeId, TradeType = tradeType, RiskMargin = riskMargin,
            MaxProfit = profit, MaxLoss = CalculateMaximumLoss(fundBalance),
            MaxReturn = CalculateMaximumReturn(profit, riskMargin),
            MaxLossLimit = put.MaxLossLimit + call.MaxLossLimit,
            MinProfitLimit = put.MinProfitLimit + call.MinProfitLimit,
            MaxProfitLimit = put.MaxProfitLimit + call.MaxProfitLimit,
            MinProfitTarget = CalculateMinimumProfitTarget(profit, tradeCommission),
            DailyProfitTarget = CalculateDailyProfitTarget(profit, daysToExpiry, tradeCommission),
            CreatedOn = initializedAtUtc, UpdatedOn = initializedAtUtc,
            CreatedBy = initializedBy, UpdatedBy = initializedBy
        }, [put, call]);
    }

    /// <summary>Creates a spread limit from its three individually calculated price thresholds.</summary>
    /// <param name="tradeId">Trade identity.</param><param name="condorType">Iron Condor strategy.</param>
    /// <param name="spreadType">Put or call credit/debit spread.</param><param name="spreadPrice">Spread midpoint.</param>
    /// <returns>The proposed spread limit.</returns>
    static TradeTypeLimitReadModel CreateSpreadLimit(int tradeId, TradeType condorType, TradeType spreadType, decimal spreadPrice)
        => new(tradeId, spreadType, CalculateMaximumLossPriceLimit(condorType, spreadPrice),
            CalculateMinimumProfitPriceLimit(condorType, spreadPrice), CalculateMaximumProfitPriceLimit(condorType, spreadPrice));

    /// <summary>Validates strategy and price, then calculates the common base price limit = 2*spreadPrice.</summary>
    /// <param name="tradeType">Iron Condor strategy.</param><param name="spreadPrice">Positive spread midpoint.</param>
    /// <returns>The common doubled spread price.</returns>
    static decimal CalculateBasePriceLimit(TradeType tradeType, decimal spreadPrice)
    {
        if (tradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor))
            throw new ArgumentOutOfRangeException(nameof(tradeType));
        ArgumentOutOfRangeException.ThrowIfNegative(spreadPrice);
        return 2m * spreadPrice;
    }

    /// <summary>Rejects negative or crossed quote inputs before midpoint calculation.</summary>
    /// <param name="bid">Nonnegative bid.</param><param name="ask">Nonnegative ask at least equal to bid.</param>
    static void ValidateQuote(decimal bid, decimal ask)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bid);
        if (ask < bid) throw new ArgumentOutOfRangeException(nameof(ask), "Ask must be at least bid.");
    }
}
