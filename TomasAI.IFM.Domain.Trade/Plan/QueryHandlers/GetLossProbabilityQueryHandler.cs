using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using MathNet.Numerics.Distributions;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetLossProbabilityQueryHandler(ITradeDbContext db, IDataCacheService dataCacheService)
    : BaseQueryHandler,
    IAsyncQueryHandler<GetLossProbabilityQuery, LossProbabilityDataModel>,
    IAsyncQueryHandler<GetLossProbabilityDistributionQuery, LossProbabilityDistributionDataModel>
{
    /// <summary>
    /// Executes the query to calculate the loss probability based on the provided forward loss ratio and historical
    /// data.
    /// </summary>
    /// <remarks>This method retrieves historical trade plan forward loss ratios for the specified date range,
    /// calculates statistical metrics such as the cumulative probability and z-score, and returns a data model
    /// containing the loss probability. If no historical data is available for the specified date, it computes the loss
    /// probability using the median and filtered values of the historical data.</remarks>
    /// <param name="q">The query containing the value date and forward loss ratio for which the loss probability is calculated.</param>
    /// <returns>A <see cref="LossProbabilityDataModel"/> containing the calculated loss probability, threshold, and threshold
    /// count.</returns>
    public async Task<LossProbabilityDataModel> ExecuteAsync(GetLossProbabilityQuery q)
    {
        var lossProbability = default(LossProbabilityDataModel);
        var forwardLossRatioMap = GetForwardLossRatioMap(q.ValueDate);
        if (!forwardLossRatioMap.TryGetValue(q.ValueDate, out ICollection<TradePlanForwardLossRatioReadModel>? value))
        {
            // get trade plan loss probabilities for last 30 days...
            var endDate = q.ValueDate.AddDays(0);
            var startDate = endDate.AddDays(-60);
            var lossProbs = new List<TradePlanForwardLossRatioReadModel>(await db.GetTradePlanForwardLossRatiosAsync(startDate, endDate));
            lossProbs = [.. lossProbs.OrderByDescending(e => e.ForwardLossRatio)];
            var lossProbsMedian = lossProbs[lossProbs.Count / 2].ForwardLossRatio;
            lossProbs = [.. lossProbs.Where(e => e.ForwardLossRatio <= lossProbsMedian)];
            value = lossProbs;
            forwardLossRatioMap.Add(q.ValueDate, value);
        }

        value.Add(new TradePlanForwardLossRatioReadModel(q.ForwardLossRatio));
        var forwardLossRatios = value.Select(e => Math.Sqrt(e.ForwardLossRatio)).ToList();
        var lossDistribution = Normal.Estimate(forwardLossRatios);

        // return cumulative probability and z-score from current trade plan forward loss ratio...
        var zscore = GetZScore(lossDistribution);
        var mScore = GetMScore(forwardLossRatios);
        lossProbability = new LossProbabilityDataModel
        (
            Value: lossDistribution.CumulativeDistribution(zscore),
            Threshold: 0,
            ThresholdCount: 0
        );
        return lossProbability;

        double GetZScore(Normal lossDistribution) => (Math.Sqrt(q.ForwardLossRatio) - lossDistribution.Mean) / lossDistribution.StdDev;

        double GetMScore(ICollection<double> forwardLossRatios)
        {
            // get median...
            var median = forwardLossRatios.OrderBy(e => e).ToArray()[(int)(forwardLossRatios.Count / 2)];

            // get the absolute deviations from the median...
            var absDevsFromMedian = forwardLossRatios.Select(x => Math.Abs(x - median)).ToList();

            // get the median of the absolute values...
            var medianAbsDev = absDevsFromMedian.OrderBy(e => e).ToArray()[(int)(absDevsFromMedian.Count / 2)];
            return Math.Sqrt(q.ForwardLossRatio) / (median + (3.00 * medianAbsDev));
        }

    }

    /// <summary>
    /// Executes the specified query to retrieve loss probability distribution data.
    /// </summary>
    /// <param name="q">The query containing the parameters required to fetch the loss probability distribution data. Cannot be <see
    /// langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the  <see
    /// cref="LossProbabilityDistributionDataModel"/> representing the loss probability distribution data.</returns>
    public async Task<LossProbabilityDistributionDataModel> ExecuteAsync(GetLossProbabilityDistributionQuery q)
        => await ExecuteQueryAsync(q, () => GetLossProbabilityDistributionAsync(q));

     async Task<LossProbabilityDistributionDataModel> GetLossProbabilityDistributionAsync(GetLossProbabilityDistributionQuery q)
    {
        var lossProbabilityDistribution = default(LossProbabilityDistributionDataModel);
        // get trade plan loss probabilities for last 30 days...
        var forwardLossRatioMap = GetForwardLossRatioMap(q.ValueDate);
        if (!forwardLossRatioMap.TryGetValue(q.ValueDate, out ICollection<TradePlanForwardLossRatioReadModel>? value))
        {
            var endDate = q.ValueDate;
            var startDate = endDate.AddDays(-30);
            var lossProbs = new List<TradePlanForwardLossRatioReadModel>(await db.GetTradePlanForwardLossRatiosAsync(startDate, endDate));
            value = lossProbs;
            forwardLossRatioMap.Add(q.ValueDate, value);
        }

        // create loss probability distribution from trade plan loss probabilities...
        //var skewedDistribution = SkewedDistribution.Estimate(forwardLossRatioMap[q.ValueDate].Select(e => e.ForwardLossRatio));
        var distribution = Normal.Estimate(value.Select(e => e.ForwardLossRatio));
        lossProbabilityDistribution = new LossProbabilityDistributionDataModel
        (
            Mean: distribution.Mean,
            StdDev: distribution.StdDev
        );
        return lossProbabilityDistribution;
    }

    Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>> GetForwardLossRatioMap(DateOnly valueDate)
    {
        if (!dataCacheService.Exists(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}"))
            dataCacheService.Add(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}", new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>());
        return dataCacheService.Get<string, Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>>(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}");
    }

}
