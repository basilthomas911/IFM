using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;


namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

/// <summary>
/// Provides static methods for creating end-of-day (EOD) data models for futures contracts, including calculated
/// Bollinger Bands and related statistical indicators based on historical and tick data.
/// </summary>
/// <remarks>This class is intended to facilitate the generation of EOD data for futures by aggregating contract
/// information, tick data, and historical ranges. It is important to ensure that the input data collections are valid
/// and that the window size parameter is appropriate for the dataset being analyzed. The resulting model includes
/// statistical measures such as Bollinger Bands, standard deviation, and market direction indicators, which can be used
/// for further analysis or reporting.</remarks>
internal static class FuturesEodDataModel 
{
    public static FuturesEodDataV2ReadModel CreateFuturesEodData(
        this InsertFuturesEodDataCommand e,
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel contract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
    {
        var eodDataList = new List<FuturesEodDataV2ReadModel>
        {
            eodDataToday with
            {
                ValueDate = valueDate,
                ClosePrice = futuresTickData.Price
            }
        };
        eodDataList.AddRange(eodDataRange.Skip(1));
        var bb = new BollingerBands(windowSize, [.. eodDataList], normCurveData, vixEodData);
        return new FuturesEodDataV2ReadModel(
            contractId: contract.ContractId,
            valueDate: valueDate,
            symbol: contract.Symbol,
            openPrice: bb.Open,
            highPrice: bb.High,
            lowPrice: bb.Low,
            closePrice: bb.Close,
            volume: bb.Volume,
            dailyPercentChange: bb.DailyPercentageChange,
            dailyStdDev: bb.StdDev,
            dailyStdDevAmount: bb.StdDevAmount,
            upperBand: bb.UpperBand,
            mean: bb.Mean,
            lowerBand: bb.LowerBand,
            marketDirection: bb.MarketDirection,
            marketVolatility: bb.MarketVolatility,
            priceDirection: bb.PriceDirection,
            priceVolatility: bb.PriceVolatility,
            marketDirectionIndicator: bb.MarketDirectionIndicator,
            windowSize: bb.WindowSize
        );
    }

   
   
}
