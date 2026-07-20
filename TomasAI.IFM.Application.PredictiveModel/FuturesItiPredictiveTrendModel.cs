using System.Collections.Immutable;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.PredictiveModel;

public class FuturesItiPredictiveTrendModel(IMarketDataDbContext db)
    : IFuturesItiPredictiveTrendModel
{

    public async Task<(IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Model,
        IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Training,
        IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Test)> LoadTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var modelData = await db.GetFuturesItiTrendDeltaDataAsync(symbol, startDate, endDate);
        var valueDates = modelData.Select(x => x.ValueDate).Distinct().ToArray();
        var trainDataValueDateRows = Convert.ToInt32(Convert.ToDouble(valueDates?.Length) * 0.80);
        var trainDataValueDates = valueDates.Take(trainDataValueDateRows);
        var testDataValueDates = valueDates.Skip(trainDataValueDateRows);
        var trainData = new List<FuturesItiTrendDeltaDataReadModel>( modelData.Where(e => trainDataValueDates.Contains(e.ValueDate)) );
        var testData = new List<FuturesItiTrendDeltaDataReadModel>( modelData.Where(e => testDataValueDates.Contains(e.ValueDate)) );
        return (modelData.ToImmutableArray(), trainData.ToImmutableArray(), testData.ToImmutableArray());
    }

    public async Task SaveTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel trendModel)
        => await db.InsertFuturesItiTrendDeltaModelAsync(trendModel);

    public async Task<(IReadOnlyList<FuturesItiTrendClassDataReadModel> Model,
       IReadOnlyList<FuturesItiTrendClassDataReadModel> Training,
       IReadOnlyList<FuturesItiTrendClassDataReadModel> Test)> LoadTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var modelData = await db.GetFuturesItiTrendClassDataAsync(symbol, startDate, endDate);
        var valueDates = modelData.Select(x => x.ValueDate).Distinct().ToArray();
        var trainDataValueDateRows = Convert.ToInt32(Convert.ToDouble(valueDates?.Length) * 0.80);
        var trainDataValueDates = valueDates?.Take(trainDataValueDateRows);
        var testDataValueDates = valueDates?.Skip(trainDataValueDateRows);
        var trainData = new List<FuturesItiTrendClassDataReadModel>(modelData.Where(e => trainDataValueDates!.Contains(e.ValueDate)));
        var testData = new List<FuturesItiTrendClassDataReadModel>(modelData.Where(e => testDataValueDates!.Contains(e.ValueDate)));
        return (modelData.ToImmutableArray(), trainData.ToImmutableArray(), testData.ToImmutableArray());
    }

    public async Task SaveTrendClassModelAsync(FuturesItiTrendClassModelReadModel trendModel)
        => await db.InsertFuturesItiTrendClassModelAsync(trendModel);

    public double PredictedUpTrendDelta(FuturesItiSignalV2ReadModel e)
    {
        var avgPredictedTrendDelta = db.GetFuturesItiSignalAveragePredictedTrendDeltaAsync ( e.ContractId, e.ValueDate).Result;
        return avgPredictedTrendDelta?.PredictedUpTrendDelta ?? 0;
    }

    public double PredictedDownTrendDelta(FuturesItiSignalV2ReadModel e)
    {
        var avgPredictedTrendDelta = db.GetFuturesItiSignalAveragePredictedTrendDeltaAsync(e.ContractId, e.ValueDate).Result;
        return avgPredictedTrendDelta?.PredictedDownTrendDelta ?? 0;
    }

    public double AverageUpTrendFuturesRSI(FuturesItiSignalV2ReadModel e)
    {
        var avgPredictedTrendDelta = db.GetFuturesItiSignalAveragePredictedTrendDeltaAsync(e.ContractId, e.ValueDate).Result;
        return avgPredictedTrendDelta?.UpTrendFuturesRSI ?? 0;
    }

    public double AverageDownTrendFuturesRSI(FuturesItiSignalV2ReadModel e)
    {
        var avgPredictedTrendDelta = db.GetFuturesItiSignalAveragePredictedTrendDeltaAsync(e.ContractId, e.ValueDate).Result;
        return avgPredictedTrendDelta?.DownTrendFuturesRSI ?? 0;
    }

    /// <summary>
    /// return new up trend coast line counter
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public int SetUpTrendCoastLineCounter(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta)
    {
        var upTrendCoastLineCounterIncrement =  1;
        var upTrendCoastLineCounterDecrement =  1;
        var upTrendStrength = PredictedUpTrendStrength(e, symbol, predictedTrendDelta);
        return default(int) switch
        {
            _ when upTrendStrength == IntrinsicTimePredictedTrendStrengthType.High => Math.Min(upTrendCoastLineCounterIncrement, 5),
            _ when upTrendStrength == IntrinsicTimePredictedTrendStrengthType.Low => Math.Max(upTrendCoastLineCounterDecrement, 1),
            _ => 3
        };
    }

    /// <summary>
    /// return new down trend coast line counter
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public int SetDownTrendCoastLineCounter(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta)
    {
        var downTrendCoastLineCounterIncrement =  1;
        var downTrendCoastLineCounterDecrement =  1;
        var downTrendStrength = PredictedDownTrendStrength(e, symbol, predictedTrendDelta);
        return default(int) switch
        {
            _ when downTrendStrength == IntrinsicTimePredictedTrendStrengthType.High => Math.Min(downTrendCoastLineCounterIncrement, 5),
            _ when downTrendStrength == IntrinsicTimePredictedTrendStrengthType.Low => Math.Max(downTrendCoastLineCounterDecrement, 1),
            _ => 3
        };
    }

    public IntrinsicTimePredictedTrendStrengthType PredictedUpTrendStrength(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta)
        => IntrinsicTimePredictedTrendStrengthType.Low;

    public IntrinsicTimePredictedTrendStrengthType PredictedDownTrendStrength(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta)
        => IntrinsicTimePredictedTrendStrengthType.Low;

}
