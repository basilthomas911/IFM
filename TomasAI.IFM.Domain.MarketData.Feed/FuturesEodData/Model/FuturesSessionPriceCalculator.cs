using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;

/// <summary>
/// Calculates session-relative price facts from the latest accepted close and
/// the authoritative trading-session open.
/// </summary>
internal static class FuturesSessionPriceCalculator
{
    /// <summary>
    /// Returns the decimal ratio <c>(currentClose - sessionOpen) / sessionOpen</c>,
    /// rounded to four decimal places for the established EOD transport contract.
    /// </summary>
    internal static double CalculateDailyPercentChange(
        decimal currentClosePrice,
        decimal sessionOpenPrice) => sessionOpenPrice <= 0m
            ? 0d
            : Convert.ToDouble(Math.Round(
                (currentClosePrice - sessionOpenPrice) / sessionOpenPrice,
                4));

    /// <summary>
    /// Classifies the current close relative to the session open. An invalid
    /// session open, or an unchanged close, has no directional movement.
    /// </summary>
    internal static PriceDirectionType CalculatePriceDirection(
        decimal currentClosePrice,
        decimal sessionOpenPrice) => sessionOpenPrice <= 0m
            ? PriceDirectionType.Flat
            : currentClosePrice switch
            {
                _ when currentClosePrice > sessionOpenPrice => PriceDirectionType.Rising,
                _ when currentClosePrice < sessionOpenPrice => PriceDirectionType.Falling,
                _ => PriceDirectionType.Flat
            };
}
