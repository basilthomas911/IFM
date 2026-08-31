using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

/// <summary>
/// Provides the compatibility transport model for raw end-of-day futures session facts.
/// </summary>
/// <remarks>
/// Session-relative percentage and direction are live price facts and are recalculated here.
/// Historical analytics indicators are preserved because Analytics actors own them.
/// </remarks>
internal static class FuturesEodDataModel 
{
    public static FuturesEodDataV2ReadModel CreateFuturesEodData(
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel contract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
    {
        ArgumentNullException.ThrowIfNull(futuresTickData);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(eodDataToday);
        var close = futuresTickData.Price;
        return eodDataToday with
        {
            ContractId = contract.ContractId,
            ValueDate = valueDate,
            Symbol = contract.Symbol,
            HighPrice = Math.Max(eodDataToday.HighPrice, close),
            LowPrice = Math.Min(eodDataToday.LowPrice, close),
            ClosePrice = close,
            DailyPercentChange = FuturesSessionPriceCalculator.CalculateDailyPercentChange(
                close,
                eodDataToday.OpenPrice),
            PriceDirection = FuturesSessionPriceCalculator.CalculatePriceDirection(
                close,
                eodDataToday.OpenPrice)
        };
    }

   
   
}
