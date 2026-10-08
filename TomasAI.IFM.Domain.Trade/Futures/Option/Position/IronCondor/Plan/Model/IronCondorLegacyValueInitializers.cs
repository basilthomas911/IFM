using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Calculates recovered legacy Iron Condor monitoring values without mutating actor state.</summary>
/// <remarks>Formulas follow the calculator preceding commit 281550666. Prices are points, ratios are fractions.
/// Callers capture versioned inputs and apply calculated values through their owning command's event.</remarks>
public static class IronCondorLegacyValueInitializers
{
    /// <summary>Calculates FiveDayXMA from completed daily closes: seed = mean(first five closes);
    /// then EMA(today) = close(today)/3 + 2*EMA(previous)/3, because alpha = 2/(5+1).</summary>
    /// <param name="dailyClosingPrices">At least five completed-session closes, ordered oldest to newest.</param>
    /// <returns>The five-session EMA in futures price points. The open session is excluded by the reader.</returns>
    /// <exception cref="ArgumentException">Fewer than five completed sessions are available.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A closing price is not positive.</exception>
    public static double CalculateFiveDayXma(IReadOnlyList<decimal> dailyClosingPrices)
    {
        ArgumentNullException.ThrowIfNull(dailyClosingPrices);
        if (dailyClosingPrices.Count < 5)
            throw new ArgumentException("IronCondorTradePlan: FiveDayXMA requires five completed daily sessions.", nameof(dailyClosingPrices));
        foreach (var close in dailyClosingPrices)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(close, nameof(dailyClosingPrices));
        var movingAverage = dailyClosingPrices.Take(5).Sum() / 5m;
        for (var index = 5; index < dailyClosingPrices.Count; index++)
            movingAverage = dailyClosingPrices[index] / 3m + movingAverage * 2m / 3m;
        return Convert.ToDouble(movingAverage);
    }

    /// <summary>Calculates ForwardDelta = sum(sign(signedLegQuantity) * qualifiedOptionDelta) for four legs,
    /// per strategy unit. Quantity and contract multiplier are not applied to this unit delta.</summary>
    /// <param name="qualifiedLegDeltas">Four matched, qualified option deltas and their signed strategy quantities.</param>
    /// <returns>The signed delta of one complete Iron Condor strategy unit.</returns>
    /// <exception cref="ArgumentException">The four legs do not have equal nonzero absolute quantities.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A qualified delta is not finite.</exception>
    public static double CalculateForwardDelta(IReadOnlyList<(int SignedQuantity, double OptionDelta)> qualifiedLegDeltas)
    {
        ArgumentNullException.ThrowIfNull(qualifiedLegDeltas);
        if (qualifiedLegDeltas.Count != 4 || qualifiedLegDeltas[0].SignedQuantity == 0)
            throw new ArgumentException("IronCondorTradePlan: ForwardDelta requires four complete strategy legs.", nameof(qualifiedLegDeltas));
        var strategyQuantity = Math.Abs((long)qualifiedLegDeltas[0].SignedQuantity);
        var forwardDelta = 0d;
        foreach (var leg in qualifiedLegDeltas)
        {
            if (Math.Abs((long)leg.SignedQuantity) != strategyQuantity)
                throw new ArgumentException("IronCondorTradePlan: ForwardDelta requires equal leg quantities.", nameof(qualifiedLegDeltas));
            if (!double.IsFinite(leg.OptionDelta))
                throw new ArgumentOutOfRangeException(nameof(qualifiedLegDeltas), "Qualified option deltas must be finite.");
            forwardDelta += Math.Sign(leg.SignedQuantity) * leg.OptionDelta;
        }
        return forwardDelta;
    }

    /// <summary>Calculates forwardPrice = abs(putForwardPrice) + abs(callForwardPrice), in price points.</summary>
    /// <param name="putForwardPrice">The put spread's forward price in points.</param>
    /// <param name="callForwardPrice">The call spread's forward price in points.</param>
    /// <returns>The combined absolute forward spread price.</returns>
    public static decimal CalculateForwardPrice(decimal putForwardPrice, decimal callForwardPrice)
        => Math.Abs(putForwardPrice) + Math.Abs(callForwardPrice);

    /// <summary>Calculates forwardLossRatio = forwardPrice / limitPrice; zero forward price returns zero.</summary>
    /// <param name="forwardPrice">The combined absolute forward price in points.</param>
    /// <param name="limitPrice">Positive MaxLossLimit for a short condor or MinProfitLimit for a long condor, in points.</param>
    /// <returns>The dimensionless forward-loss ratio.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The price is negative or its limit is not positive.</exception>
    public static double CalculateForwardLossRatio(decimal forwardPrice, decimal limitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(forwardPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limitPrice);
        return (double)(forwardPrice / limitPrice);
    }

    /// <summary>Calculates MScore = sqrt(currentRatio) / (median(sqrt(ratios)) + 3.5 * MAD(sqrt(ratios))).</summary>
    /// <param name="currentForwardLossRatio">The current nonnegative forward-loss ratio.</param>
    /// <param name="historicalForwardLossRatios">The preceding 60-day sample, excluding this observation; duplicates of other observations are retained.</param>
    /// <returns>The legacy median score, which is not a probability and can exceed one.</returns>
    /// <remarks>The current observation is appended once. Legacy median sorts descending and chooses index count/2,
    /// including for even counts; it does not average the middle pair. No input collection is modified.</remarks>
    /// <exception cref="ArgumentException">A ratio is negative/nonfinite, history is empty, or the normalization denominator is zero.</exception>
    public static double CalculateMScore(double currentForwardLossRatio, IReadOnlyCollection<double> historicalForwardLossRatios)
    {
        ArgumentNullException.ThrowIfNull(historicalForwardLossRatios);
        RequireNonnegativeFinite(currentForwardLossRatio, nameof(currentForwardLossRatio));
        if (historicalForwardLossRatios.Count == 0)
            throw new ArgumentException("IronCondorTradePlan: MScore requires historical forward-loss observations.", nameof(historicalForwardLossRatios));
        var values = new double[historicalForwardLossRatios.Count + 1];
        var index = 0;
        foreach (var ratio in historicalForwardLossRatios)
        {
            RequireNonnegativeFinite(ratio, nameof(historicalForwardLossRatios));
            values[index++] = Math.Sqrt(ratio);
        }
        values[index] = Math.Sqrt(currentForwardLossRatio);
        Array.Sort(values);
        var median = values[(values.Length - 1) / 2];
        for (index = 0; index < values.Length; index++)
            values[index] = Math.Abs(values[index] - median);
        Array.Sort(values);
        var denominator = median + 3.5 * values[(values.Length - 1) / 2];
        if (denominator <= 0)
            throw new ArgumentException("IronCondorTradePlan: MScore normalization denominator must be positive.", nameof(historicalForwardLossRatios));
        return Math.Sqrt(currentForwardLossRatio) / denominator;
    }

    /// <summary>Classifies MScore using legacy bands: short scores >=0.8 High, >=0.7 Medium, else Low;
    /// long scores >=0.8 Low, >=0.7 Medium, else High. Critical overrides elevated bands.</summary>
    /// <param name="tradeType">The short or long Iron Condor strategy.</param>
    /// <param name="medianScore">The finite nonnegative MScore.</param>
    /// <param name="isCriticalRisk">Whether a separate critical risk guard has fired.</param>
    /// <returns>The legacy trade risk classification.</returns>
    public static TradeRiskType CalculateTradeRisk(TradeType tradeType, double medianScore, bool isCriticalRisk = false)
    {
        RequireNonnegativeFinite(medianScore, nameof(medianScore));
        if (tradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor))
            throw new ArgumentOutOfRangeException(nameof(tradeType));
        return TomasAI.IFM.Domain.Trade.Shared.TradePlan.ViewModels.IronCondorTradePlanReadModel
            .FromMScore(medianScore, isCriticalRisk, tradeType);
    }

    /// <summary>Calculates the MDI warning ratio from ten-point bands: short 0.70 + band*0.01; long 0.52 + band*0.01.</summary>
    /// <param name="tradeType">ShortIronCondor or LongIronCondor.</param>
    /// <param name="marketDirectionIndex">Finite MDI; values below zero use band zero and values above 100 use band ten.</param>
    /// <returns>The configured legacy warning ratio.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The strategy is unsupported or MDI is nonfinite.</exception>
    public static double CalculateMdiWarningRatio(TradeType tradeType, double marketDirectionIndex)
    {
        if (!double.IsFinite(marketDirectionIndex))
            throw new ArgumentOutOfRangeException(nameof(marketDirectionIndex));
        var band = (int)Math.Floor(Math.Clamp(marketDirectionIndex, 0, 100) / 10);
        var basis = tradeType switch
        {
            TradeType.ShortIronCondor => 0.70m,
            TradeType.LongIronCondor => 0.52m,
            _ => throw new ArgumentOutOfRangeException(nameof(tradeType), "Iron Condor strategy required.")
        };
        return (double)(basis + band * 0.01m);
    }

    /// <summary>Calculates the MDI limit ratio: short warning + 0.03; long warning - 0.03.</summary>
    /// <param name="tradeType">The Iron Condor strategy.</param>
    /// <param name="marketDirectionIndex">The MDI used to initialize the warning ratio.</param>
    /// <returns>The legacy forward-loss limit ratio.</returns>
    public static double CalculateMdiLimitRatio(TradeType tradeType, double marketDirectionIndex)
        => (double)((decimal)CalculateMdiWarningRatio(tradeType, marketDirectionIndex)
            + (tradeType == TradeType.ShortIronCondor ? 0.03m : -0.03m));

    /// <summary>Calculates the short-condor MDI price threshold = ratio * 2 * abs(openingNetSpread).</summary>
    /// <param name="mdiRatio">The warning or limit ratio.</param>
    /// <param name="openingNetSpread">The opening spread price in points.</param>
    /// <returns>The threshold in price points; no multiplier or quantity scaling is applied.</returns>
    public static decimal CalculateMdiPriceThreshold(double mdiRatio, decimal openingNetSpread)
    {
        RequireNonnegativeFinite(mdiRatio, nameof(mdiRatio));
        return (decimal)mdiRatio * 2 * Math.Abs(openingNetSpread);
    }

    /// <summary>Classifies gamma using abs(round(callGamma*100000) - round(putGamma*100000)): >=30 High, >=20 Low, else None.</summary>
    /// <param name="shortCallGamma">The nonnegative short call contract gamma, without exposure scaling.</param>
    /// <param name="shortPutGamma">The nonnegative short put contract gamma, without exposure scaling.</param>
    /// <returns>The legacy call-named classification; either rounded gamma zero yields None.</returns>
    /// <remarks>Convert.ToInt32 uses midpoint-to-even rounding. The historical names are retained even when put gamma is larger.</remarks>
    public static GammaRiskType CalculateGammaRisk(double shortCallGamma, double shortPutGamma)
    {
        RequireNonnegativeFinite(shortCallGamma, nameof(shortCallGamma));
        RequireNonnegativeFinite(shortPutGamma, nameof(shortPutGamma));
        var callGamma = Convert.ToInt32(shortCallGamma * 100000);
        var putGamma = Convert.ToInt32(shortPutGamma * 100000);
        if (callGamma == 0 || putGamma == 0)
            return GammaRiskType.None;
        return Math.Abs((long)callGamma - putGamma) switch
        {
            >= 30 => GammaRiskType.HighShortCallGamma,
            >= 20 => GammaRiskType.LowShortCallGamma,
            _ => GammaRiskType.None
        };
    }

    /// <summary>Calculates the next trailing-stop ratio: zero initializes to 0.20; otherwise add 0.05.</summary>
    /// <param name="currentStopLossRatio">The nonnegative currently accepted trailing-stop ratio.</param>
    /// <returns>The proposed ratio; the owning command must decide whether to apply it.</returns>
    public static double CalculateNextTrailingStopRatio(double currentStopLossRatio)
    {
        RequireNonnegativeFinite(currentStopLossRatio, nameof(currentStopLossRatio));
        return currentStopLossRatio == 0 ? 0.20 : (double)((decimal)currentStopLossRatio + 0.05m);
    }

    /// <summary>Calculates the active trailing-stop money threshold = (stopLossRatio - 0.05) * maximumProfit.</summary>
    /// <param name="stopLossRatio">An active trailing-stop ratio of at least 0.20.</param>
    /// <param name="maximumProfit">Positive maximum profit in currency units.</param>
    /// <returns>The currency threshold; the legacy stop guard is averageTradePnl strictly below this amount.</returns>
    public static decimal CalculateTrailingStopThreshold(double stopLossRatio, decimal maximumProfit)
    {
        RequireNonnegativeFinite(stopLossRatio, nameof(stopLossRatio));
        if (stopLossRatio < 0.20)
            throw new ArgumentOutOfRangeException(nameof(stopLossRatio), "An active trailing stop is required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumProfit);
        return ((decimal)stopLossRatio - 0.05m) * maximumProfit;
    }

    /// <summary>Validates that a formula input is finite and nonnegative rather than substituting a default value.</summary>
    /// <param name="value">The numeric input.</param>
    /// <param name="argumentName">The domain argument name reported on rejection.</param>
    /// <exception cref="ArgumentOutOfRangeException">The input is negative or nonfinite.</exception>
    static void RequireNonnegativeFinite(double value, string argumentName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(argumentName, "A finite nonnegative value is required.");
    }
}
