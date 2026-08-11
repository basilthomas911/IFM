using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    internal static ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractFromProviderAsync(
        this GetFuturesOptionContractQuery q, ApplicationMarketDataApi marketDataApi)
        => GetFuturesOptionContractFromProviderAsync(marketDataApi, q.ContractId);

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

}
