using Microsoft.ML;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.PredictiveModel.EventHandlers;

public class FuturesItiTrendModelUseCaseEvents(
    IFuturesItiPredictiveTrendModel predictiveTrendModel,
    IFuturesItiTrendEventProducer eventProducer,
    IStatusConsoleWriter statusConsoleWriter) : BaseEventServiceHandler(statusConsoleWriter),
    IAsyncEventHandler<FuturesItiTrendDeltaModelTrainedEvent, PredictiveModelService>,
    IAsyncEventHandler<FuturesItiTrendClassModelTrainedEvent, PredictiveModelService>
{
    readonly IFuturesItiPredictiveTrendModel _predictiveTrendModel = IsArgumentNull.Set(predictiveTrendModel);
    readonly IFuturesItiTrendEventProducer _eventProducer = IsArgumentNull.Set(eventProducer);

    /// <summary>
    /// train futures iti trend models
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesItiTrendDeltaModelTrainedEvent e)
    {
        try
        {
            // create ML context with seed for repeteable/deterministic results...
            var mlContext = new MLContext(seed: 1);

            // create data processing pipeline...
            var trendDataPipeline = mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: nameof(FuturesItiTrendDelta.TrendDelta))
                .Append(mlContext.Transforms.NormalizeMinMax(outputColumnName: nameof(FuturesItiTrendDelta.TrendDirection)))
                .Append(mlContext.Transforms.NormalizeMinMax(outputColumnName: nameof(FuturesItiTrendDelta.TrendDirectionMode)))
                .Append(mlContext.Transforms.NormalizeMeanVariance(outputColumnName: nameof(FuturesItiTrendDelta.FuturesPrice)))
                .Append(mlContext.Transforms.NormalizeMeanVariance(outputColumnName: nameof(FuturesItiTrendDelta.TrendExtreme)))
                .Append(mlContext.Transforms.NormalizeMinMax(outputColumnName: nameof(FuturesItiTrendDelta.FuturesRSI)))
                .Append(mlContext.Transforms.Concatenate("Features",
                       nameof(FuturesItiTrendDelta.TrendDirection),
                       nameof(FuturesItiTrendDelta.TrendDirectionMode),
                       nameof(FuturesItiTrendDelta.FuturesPrice),
                       nameof(FuturesItiTrendDelta.TrendExtreme),
                       nameof(FuturesItiTrendDelta.FuturesRSI)
            ));

            //  create data view from trend data...
            var trendDeltaData = await LoadTrendDeltaDataAsync(e.EntityId.Symbol, e.StartDate, e.EndDate);
            var trainingDataView = mlContext.Data.LoadFromEnumerable(trendDeltaData.Training);
            var testDataView = mlContext.Data.LoadFromEnumerable(trendDeltaData.Test);

            // set the training algorithm, then create and config the modelBuilder - Selected Trainer (LightGbm Regression algorithm)...
            var trainer = mlContext.Regression.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features", learningRate: 0.5, numberOfIterations: 20000, numberOfLeaves: 5);
            var trainingPipeline = trendDataPipeline.Append(trainer);

            // train the trend data using selected regression algo...
            var trainedModel = trainingPipeline.Fit(trainingDataView);

            // evaluate model...
            var predictions = trainedModel.Transform(testDataView);
            var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            // sample prediction...
            var predEngine = mlContext.Model.CreatePredictionEngine<FuturesItiTrendDelta, FuturesItiTrendDeltaPrediction>(trainedModel);

            var sd = trendDeltaData.Test
                ?.Where(o => o.TrendDirectionMode == 0 && o.TrendDirection == 1)
                .LastOrDefault();
            if (sd is not null)
            {
                var trendDeltaSample = new FuturesItiTrendDelta
                (
                    Symbol: sd.Symbol,
                    ValueDate: sd.ValueDate,
                    Timestamp: sd.Timestamp,
                    TrendDelta: sd.TrendDelta,
                    TrendDirection: sd.TrendDirection,
                    TrendDirectionMode: sd.TrendDirectionMode,
                    FuturesPrice: sd.FuturesPrice,
                    TrendExtreme: sd.TrendExtreme,
                    FuturesRSI: sd.FuturesRSI
                );
                var predition = predEngine.Predict(trendDeltaSample);
            }

            // save trained model with stats/metrics
            var memoryStream = new MemoryStream();
            mlContext.Model.Save(trainedModel, trainingDataView.Schema, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            var byteArraySize = (int)memoryStream.Length;
            var byteArray = new List<byte>();
            byteArray.AddRange(memoryStream.GetBuffer().Take(byteArraySize));
            var trendModelData = new FuturesItiTrendDeltaModelReadModel(
                symbol: e.EntityId.Symbol,
                valueDate: e.EntityId.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Statistics.Count,
                maximum: e.Statistics.Maximum,
                mean: e.Statistics.Mean,
                median: e.Statistics.Median,
                minimum: e.Statistics.Minimum,
                skewness: e.Statistics.Skewness,
                stdDev: e.Statistics.StdDev,
                variance: e.Statistics.Variance,
                meanAbsoluteError: metrics.MeanAbsoluteError,
                meanSquaredError: metrics.MeanSquaredError,
                rootMeanSquaredError: metrics.RootMeanSquaredError,
                lossFunction: metrics.LossFunction,
                rSquared: metrics.RSquared,
                modelData: byteArray.ToArray());
            await SaveTrendDeltaModelAsync(trendModelData);
            await _eventProducer.PostEventAsync(e.ToCompletedEvent());
        }
        catch (Exception ex)
        {
            await _eventProducer.PostEventAsync(e.ToFailedEvent(ex));
        }

        async Task<(IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Model,
                IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Training,
                IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Test)> LoadTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
            => await _predictiveTrendModel.LoadTrendDeltaDataAsync(symbol, startDate, endDate);

        async Task SaveTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel trendModel)
             => await _predictiveTrendModel.SaveTrendDeltaModelAsync(trendModel);
    }

    public async Task ExecuteAsync(FuturesItiTrendClassModelTrainedEvent e)
    {
        try
        {
            // create ML context with seed for repeteable/deterministic results...
            var mlContext = new MLContext(seed: 1);

            // create data processing pipeline...
            var trendDataPipeline = mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: nameof(FuturesItiTrendClass.TrendClass))
                .Append(mlContext.Transforms.NormalizeMinMax(outputColumnName: nameof(FuturesItiTrendClass.TrendDirection)))
                .Append(mlContext.Transforms.NormalizeMinMax(outputColumnName: nameof(FuturesItiTrendClass.TrendDirectionMode)))
                .Append(mlContext.Transforms.NormalizeMeanVariance(outputColumnName: nameof(FuturesItiTrendClass.TrendDelta)))
                .Append(mlContext.Transforms.NormalizeMeanVariance(outputColumnName: nameof(FuturesItiTrendClass.FuturesRSI)))
                .Append(mlContext.Transforms.Concatenate("Features",
                       nameof(FuturesItiTrendClass.TrendDirection),
                       nameof(FuturesItiTrendClass.TrendDirectionMode),
                       nameof(FuturesItiTrendClass.TrendDelta),
                       nameof(FuturesItiTrendClass.FuturesRSI)
            ));

            //  create data view from trend data...
            var modelData = await LoadTrendClassDataAsync(e.EntityId.Symbol, e.StartDate, e.EndDate);
            var trainingDataView = mlContext.Data.LoadFromEnumerable(modelData.Training);
            var testDataView = mlContext.Data.LoadFromEnumerable(modelData.Test);

            // set the training algorithm, then create and config the modelBuilder - Selected Trainer (LightGbm Regression algorithm)...
            var trainer = mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features", learningRate: 0.5, numberOfIterations: 10000, numberOfLeaves: 5);
            var trainingPipeline = trendDataPipeline.Append(trainer);

            // train the trend data using selected regression algo...
            var trainedModel = trainingPipeline.Fit(trainingDataView);

            // evaluate model...
            var predictions = trainedModel.Transform(testDataView);
            var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label", predictedLabelColumnName: "PredictedLabel");

            // sample prediction...
            var predEngine = mlContext.Model.CreatePredictionEngine<FuturesItiTrendClass, FuturesItiTrendClassPrediction>(trainedModel);
            var sd = modelData.Test
                ?.Where(o => o.TrendDirectionMode == 0 && o.TrendDirection == 1)
                .LastOrDefault();
            if (sd is not null)
            {
                var trendClassSample = new FuturesItiTrendClass(
                    Symbol: sd.Symbol,
                    ValueDate: sd.ValueDate,
                    Timestamp: sd.Timestamp,
                    TrendClass: false,
                    TrendDelta: sd.TrendDelta,
                    TrendDirection: sd.TrendDirection,
                    TrendDirectionMode: sd.TrendDirectionMode,
                    FuturesRSI: sd.FuturesRSI);
                var prediction = predEngine.Predict(trendClassSample);
            }

            // save trained model with stats/metrics
            var memoryStream = new MemoryStream();
            mlContext.Model.Save(trainedModel, trainingDataView.Schema, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            var byteArraySize = (int)memoryStream.Length;
            var byteArray = new List<byte>();
            byteArray.AddRange(memoryStream.GetBuffer().Take(byteArraySize));
            var trendModelData = new FuturesItiTrendClassModelReadModel(
                symbol: e.EntityId.Symbol,
                valueDate: e.EntityId.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Statistics.Count,
                maximum: e.Statistics.Maximum,
                mean: e.Statistics.Mean,
                median: e.Statistics.Median,
                minimum: e.Statistics.Minimum,
                skewness: e.Statistics.Skewness,
                stdDev: e.Statistics.StdDev,
                variance: e.Statistics.Variance,
                accuracy: metrics.Accuracy,
                areaUnderPrecisionRecallCurve: metrics.AreaUnderPrecisionRecallCurve,
                areaUnderRocCurve: metrics.AreaUnderRocCurve,
                entropy: metrics.Entropy,
                f1Score: metrics.F1Score,
                modelData: byteArray.ToArray());
            await SaveTrendClassModelAsync(trendModelData);
            await _eventProducer.PostEventAsync(e.ToCompletedEvent());
        }
        catch (Exception ex)
        {
            await _eventProducer.PostEventAsync(e.ToFailedEvent(ex));
        }
        return;

        async Task<(IReadOnlyList<FuturesItiTrendClassDataReadModel> Model,
                        IReadOnlyList<FuturesItiTrendClassDataReadModel> Training,
                        IReadOnlyList<FuturesItiTrendClassDataReadModel> Test)> LoadTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
            => await _predictiveTrendModel.LoadTrendClassDataAsync(symbol, startDate, endDate);

        async Task SaveTrendClassModelAsync(FuturesItiTrendClassModelReadModel trendModel)
            => await _predictiveTrendModel.SaveTrendClassModelAsync(trendModel);

    }

}
