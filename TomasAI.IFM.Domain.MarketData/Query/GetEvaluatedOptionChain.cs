using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetEvaluatedOptionChain
{

    extension(GetEvaluatedOptionChainQuery q)
    {
        public async ValueTask<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteAsync(IMarketDataQueryContext c, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(c);
            ArgumentNullException.ThrowIfNull(q);
            var result = await LoadAsync(q, c, ct).ConfigureAwait(false);
            if (result.Success && result.Value is { } chain && !q.ReleaseOnly)
            {
                var date = c.MarketSessionAuthority.Current.OperationalValueDate;
                var lastTrade = UnderlyingLastTradePrice.Read(c.MarketDataApi, q.UnderlyingContractId);
                var model = chain with { LastUnderlyingPrice = lastTrade.Price, LastUnderlyingTradeAtUtc = lastTrade.At };
                try
                {
                    var bb = await c.DbFactory.MarketDataDb.DbReader.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
                        TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common.MarketSeriesIdentity.ForContract(q.UnderlyingContractId),
                        date, TimeFrameType.FifteenSeconds, ct).ConfigureAwait(false);
                    if (bb is { Metadata.IsValid: true } && bb.Metadata.ValueDate == date)
                        model = model with { BollingerStandardDeviation20 = bb.StandardDeviation20, BollingerAsOfUtc = bb.Metadata.MarketDataAsOfUtc };
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { Microsoft.Extensions.Logging.LoggerExtensions.LogError(c.Logger, e, "Bollinger estimate lookup failed for {ContractId}", q.UnderlyingContractId); }
                decimal? sessionOpen = null;
                try
                {
                    var current = await c.DbFactory.MarketDataDb.DbReader.GetFuturesEodDataAsync(q.UnderlyingContractId, date).ConfigureAwait(false);
                    sessionOpen = current?.OpenPrice;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { Microsoft.Extensions.Logging.LoggerExtensions.LogError(c.Logger, e, "Session open lookup failed for {ContractId} on {ValueDate}", q.UnderlyingContractId, date); }
                try
                {
                    if (c.IvCollector is { } collector && !q.FrozenEmulatorPreviewOnly)
                    {
                        collector.Observe(chain, date);
                        var latest = await collector.GetLatestAsync(chain, ct).ConfigureAwait(false);
                        var metric = latest.Metric?.Snapshot;
                        var rank = latest.FreshnessStatus == TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.VolatilityFreshnessStatus.Accepted ? metric?.IvRank : null;
                        var status = metric is null ? "Waiting for IV observation" : latest.FreshnessStatus == TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.VolatilityFreshnessStatus.Stale
                            ? "Stale IV observation" : $"{metric.RankStatus} ({metric.ValidHistoricalObservationCount}/{metric.ExpectedHistoricalObservationCount} sessions)";
                        model = model with { IvRank = rank, IvRankStatus = status };
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Microsoft.Extensions.Logging.LoggerExtensions.LogError(c.Logger, e, "IV metric lookup failed for {ContractId}", q.UnderlyingContractId);
                    model = model with { IvRankStatus = "IV metric lookup failed" };
                }
                result = new ServiceOk<EvaluatedOptionChainReadModel>(EvaluatedOptionChainSummary.Apply(model, date, sessionOpen));
            }
            return result;
        }
    }
    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> LoadAsync(GetEvaluatedOptionChainQuery q, IMarketDataQueryContext c, CancellationToken ct)
    {
        try { return await LoadCoreAsync(q, c, ct).ConfigureAwait(false); }
        catch (Exception e) when (e is not OperationCanceledException) { return new ServiceFailed<EvaluatedOptionChainReadModel>(503, e.Message); }
    }
    static Task<ServiceResult<EvaluatedOptionChainReadModel>> LoadCoreAsync(GetEvaluatedOptionChainQuery q, IMarketDataQueryContext c, CancellationToken ct)
     => QualifiedEvaluatedOptionChain.ExecuteAsync(q, c, ct);
}
