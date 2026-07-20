using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.PredictiveModel.Client;

/// <summary>
/// Provides methods for querying futures ITI trend data, including trend and coastline counters and predicted trend
/// deltas.
/// </summary>
/// <remarks>This API facilitates querying predictive models for futures contracts, enabling users to retrieve
/// calculated trend and coastline counters as well as predicted trend deltas based on provided data. Use this class to
/// interact with the underlying predictive model query service.</remarks>
/// <param name="queryService"></param>
public class FuturesItiTrendQueryApi(IPredictiveModelQueryService queryService) 
    : IFuturesItiTrendQueryApi
{
    readonly IPredictiveModelQueryService _queryService = IsArgumentNull.Set(queryService);
    readonly string _controller = "FuturesItiTrend";

    /// <summary>
    /// Retrieves the trend and coastline counters for a specified futures contract.
    /// </summary>
    /// <remarks>This method executes an API query to retrieve the trend and coastline counters for
    /// the specified  futures contract, based on the provided parameters.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract. Cannot be null or empty.</param>
    /// <param name="valueDate">The date for which the trend and coastline counters are calculated.</param>
    /// <param name="symbol">The symbol representing the futures contract. Cannot be null or empty.</param>
    /// <param name="predictedTrendDelta">The predicted change in trend, used to calculate the counters.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing a <see cref="FuturesItiTrendCoastLineCountersReadModel"/>  with
    /// the calculated trend and coastline counters.</returns>
    public async Task<ServiceResult<FuturesItiTrendCoastLineCountersReadModel>> GetFuturesItiTrendCoastLineCountersAsync(
        string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta)
        => await _queryService.ExecuteApiQueryAsync(new GetFuturesItiTrendCoastLineCountersQuery(contractId, valueDate,  symbol,  predictedTrendDelta), _controller);

    /// <summary>
    /// Asynchronously retrieves the predicted trend delta based on the provided trend data.
    /// </summary>
    /// <remarks>This method sends a query to the underlying API to calculate the predicted trend
    /// delta based on the provided trend data. Ensure that the <paramref name="trendData"/> contains valid and
    /// complete information required for the prediction.</remarks>
    /// <param name="trendData">The trend data used to calculate the predicted trend delta. This parameter must not be null.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing a <see cref="ScalarValue{T}"/> of type <see cref="double"/> that
    /// represents the predicted trend delta. If the operation fails, the result will indicate the failure.</returns>
    public async Task<ServiceResult<ScalarValue<double>>> GetPredictedTrendDeltaAsync(FuturesItiTrendDeltaDataReadModel trendData)
        => await _queryService.PostApiQueryAsync(new GetPredictedTrendDeltaQuery { TrendData = trendData}, _controller);

}
