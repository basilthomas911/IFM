using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionContract
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetFuturesOptionContractQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        internal ValueTask<FuturesOptionContractReadModel> ExecuteAsync(
        ApplicationMarketDataApi marketDataApi)
            => GetFuturesOptionContractFromProviderAsync(marketDataApi, q.ContractId);
    }

    internal static async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractFromProviderAsync(
        ApplicationMarketDataApi marketDataApi,
        string contractId)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        return await marketDataApi.GetFuturesOptionContractAsync(contractId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Futures option contract definition '{contractId}' is not configured in the active market-data epoch.");
    }

    extension(GetFuturesOptionContractQuery query)
    {
        /// <summary>Returns the typed market-data feed result.</summary>
        public async ValueTask<ServiceResult<FuturesOptionContractReadModel>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
            MarketDataFeedQueryParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(parameters.MarketDataApi).ConfigureAwait(false);
            return new TomasAI.IFM.Shared.EventSourcing.ServiceResult<FuturesOptionContractReadModel>(result);
        }
    }
}
