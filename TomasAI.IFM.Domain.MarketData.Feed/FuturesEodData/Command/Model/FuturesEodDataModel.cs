using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;


namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

/// <summary>
/// Provides the compatibility transport model for raw end-of-day futures session facts.
/// </summary>
/// <remarks>Derived indicators are intentionally not calculated here. Analytics actors own them.</remarks>
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
        return new FuturesEodDataV2ReadModel(
            contractId: contract.ContractId,
            valueDate: valueDate,
            symbol: contract.Symbol,
            openPrice: eodDataToday.OpenPrice,
            highPrice: Math.Max(eodDataToday.HighPrice, close),
            lowPrice: Math.Min(eodDataToday.LowPrice, close),
            closePrice: close,
            volume: eodDataToday.Volume
        );
    }

   
   
}
