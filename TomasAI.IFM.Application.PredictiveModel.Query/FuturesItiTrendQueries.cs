using Microsoft.ML;
using System.Diagnostics.CodeAnalysis;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.PredictiveModel.Query;

/// <summary>
/// Provides query handling for futures ITI trend predictions and coast line counters.
/// </summary>
/// <remarks>This class implements asynchronous query handlers for predictive trend delta calculations and coast
/// line counter updates. It utilizes machine learning models to predict trend deltas and interacts with the market data
/// database for retrieving necessary data.</remarks>
/// <param name="db"></param>
/// <param name="predictiveTrendModel"></param>
public class FuturesItiTrendQueries(IMarketDataDbContext db, IFuturesItiPredictiveTrendModel predictiveTrendModel)
    : IAsyncQueryHandler<GetPredictedTrendDeltaQuery, ScalarValue<double>>,
    IAsyncQueryHandler<GetFuturesItiTrendCoastLineCountersQuery, FuturesItiTrendCoastLineCountersReadModel>
{
    readonly MLContext _mlContext = new(seed: 0);
    ITransformer? _trainedModel;
    PredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>? _predictionEngine;

    /// <summary>
    /// Executes the specified query to predict the trend delta for a given set of trend data.
    /// </summary>
    /// <remarks>This method uses a machine learning prediction engine to calculate the trend delta based on
    /// the provided trend data. If the prediction engine is not initialized, it attempts to load the model
    /// asynchronously. In case of an error during execution, a <see cref="QueryException"/> is thrown with details
    /// about the failure.</remarks>
    /// <param name="q">The query containing the trend data and associated parameters required for prediction.</param>
    /// <returns>A <see cref="ScalarValue{T}"/> containing the predicted trend delta as a <see cref="double"/>. If the prediction
    /// engine is unavailable or the prediction cannot be made, the returned value will be 0.</returns>
    /// <exception cref="QueryException">Thrown if an error occurs while processing the query or during prediction. The exception contains the error code
    /// and message from the query.</exception>
    public async Task<ScalarValue<double>> ExecuteAsync(GetPredictedTrendDeltaQuery q)
    {
        double predictedTrendDelta = 0;
        try
        {
            var e = q.TrendData;
            var predictionEngine = await GetPredictionEngineAsync(e.Symbol, e.ValueDate);
            if (predictionEngine is not null)
            {
                var trendDeltaSample = new FuturesItiTrendDelta
                (
                    Symbol: e.Symbol,
                    ValueDate: e.ValueDate,
                    Timestamp: e.Timestamp,
                    TrendDelta: 0,
                    TrendDirection: e.TrendDirection,
                    TrendDirectionMode: e.TrendDirectionMode,
                    FuturesPrice: e.FuturesPrice,
                    TrendExtreme: e.TrendExtreme,
                    FuturesRSI: e.FuturesRSI
                );
                trendDeltaSample = e switch
                {
                    _ when e.FuturesRSI == -1 => trendDeltaSample with { FuturesRSI = 50 },
                    _ => trendDeltaSample
                };
                var prediction = predictionEngine.Predict(trendDeltaSample);
                predictedTrendDelta = prediction.TrendDelta;
            }
        }
        catch (Exception ex)
        {
            throw new QueryException(q.ErrorCode, ex.Message, ex.InnerException!);
        }
        return await Task.FromResult( new ScalarValue<double> (predictedTrendDelta));

        async Task<PredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>?> GetPredictionEngineAsync(string symbol, DateOnly valueDate)
        {
            if (_predictionEngine is null)
            {
                var trainedModel = await LoadModelAsync(symbol, valueDate);
                if (trainedModel is not null)
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>(trainedModel);
            }
            return _predictionEngine;
        }

        async Task<ITransformer?> LoadModelAsync(string symbol, DateOnly valueDate)
        {
            if (_trainedModel is null)
            {
                var trendDeltaModelData = await db.GetFuturesItiTrendDeltaModelAsync(symbol, valueDate);
                if (trendDeltaModelData.ModelData is not null and var modelData)
                {
                    var memoryStream = new MemoryStream( modelData);
                    _trainedModel = _mlContext.Model.Load(memoryStream, out var modelInputSchema);
                }
            }
            return _trainedModel;
        }
    }

    /// <summary>
    /// Executes the query to calculate the uptrend and downtrend coast line counters for a specified futures contract.
    /// </summary>
    /// <remarks>This method retrieves the latest futures ITI signal for the specified contract and value
    /// date. If a signal is found, it calculates the uptrend and downtrend coast line counters based on the provided
    /// symbol and predicted trend delta. If no signal is found, both counters will default to zero.</remarks>
    /// <param name="q">The query containing the contract ID, value date, symbol, and predicted trend delta used to perform the
    /// calculation.</param>
    /// <returns>A <see cref="FuturesItiTrendCoastLineCountersReadModel"/> containing the calculated uptrend and downtrend coast
    /// line counters.</returns>
    public async Task<FuturesItiTrendCoastLineCountersReadModel> ExecuteAsync(GetFuturesItiTrendCoastLineCountersQuery q)
    {
        var futuresItiSignal = await db.GetLastFuturesItiSignalAsync(q.ContractId, q.ValueDate);
        var upTrendCount = 0;
        var downTrendCount = 0;
        if (futuresItiSignal is not null )
        {
            upTrendCount = predictiveTrendModel.SetUpTrendCoastLineCounter(futuresItiSignal, q.Symbol, q.PredictedTrendDelta);
            downTrendCount = predictiveTrendModel.SetDownTrendCoastLineCounter(futuresItiSignal, q.Symbol, q.PredictedTrendDelta);
        }
        return new FuturesItiTrendCoastLineCountersReadModel(upTrendCount, downTrendCount);
    }
}


