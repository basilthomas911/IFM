using Microsoft.ML;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.PredictiveModel;

namespace TomasAI.IFM.Application.Query.PredictiveModel.FuturesItiTrend
{
    public class FuturesItiTrendQueries :
        IAsyncQueryHandler<GetPredictedTrendDeltaQuery, ScalarValue<double>>
    {
        readonly IObjectRepository<MarketDataDbContext> _dbMarketData;
        readonly MLContext _mlContext;
        ITransformer _trainedModel;
        PredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction> _predictionEngine;

        /// <summary>
        /// futures trend iti query handlers constructor
        /// </summary>
        /// <param name="dbFactory"></param>
        public FuturesItiTrendQueries(IDbContextFactory dbFactory)
        {
            IsArgumentNull.Check(dbFactory);
            _dbMarketData = dbFactory.MarketDataDb;
            _mlContext = new MLContext(seed: 0);
        }

        IMarketDataDbReadContext MarketDataDb => _dbMarketData as IMarketDataDbReadContext;

        /// <summary>
        /// return predicted trend delta
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
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
                    {
                        Symbol = e.Symbol,
                        ValueDate = e.ValueDate,
                        Timestamp = e.Timestamp,
                        TrendDelta = 0,
                        TargetDelta = e.TargetDelta,
                        TrendDirection = e.TrendDirection,
                        TrendDirectionMode = e.TrendDirectionMode,
                        FuturesPrice = e.FuturesPrice,
                        TrendExtreme = e.TrendExtreme,
                        FuturesMDI = e.FuturesMDI,
                        FuturesStdDev = e.FuturesStdDev,
                        FuturesRSI = e.FuturesRSI,
                        FuturesFiftyDMA = e.FuturesFiftyDMA,
                        FuturesTwoHundredDMA = e.FuturesTwoHundredDMA
                    };
                    var prediction = predictionEngine.Predict(trendDeltaSample);
                    predictedTrendDelta = prediction.TrendDelta;
                }
            }
            catch (Exception ex)
            {
                throw new QueryException(q.ErrorCode, ex.Message, ex.InnerException);
            }
            return await Task.FromResult( new ScalarValue<double> (predictedTrendDelta));

           async Task< PredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>> GetPredictionEngineAsync(string symbol, DateOnly valueDate)
            {
                if (_predictionEngine is null)
                {
                    var trainedModel = await LoadModelAsync(symbol, valueDate);
                    if (trainedModel is null)
                        return default;
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>(trainedModel);
                }
                return _predictionEngine;
            }

            async Task<ITransformer> LoadModelAsync(string symbol, DateOnly valueDate)
            {
                if (_trainedModel is null)
                {
                    var trendModelData = await MarketDataDb.GetFuturesItiTrendModelAsync(symbol, valueDate);
                    if ((trendModelData.ModelData?.Length ?? 0) == 0)
                        return default;
                    var memoryStream = new MemoryStream(trendModelData.ModelData);
                    _trainedModel = _mlContext.Model.Load(memoryStream, out var modelInputSchema);
                }
                return _trainedModel;
            }
        }
    }
}


