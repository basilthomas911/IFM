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
    internal static ValueTask<FuturesOptionContractReadModel> ExecuteAsync(
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


    /// <summary>Reads and replies with the requested market-data feed result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesOptionContractQuery query,
        TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
        MarketDataFeedQueryParameters parameters)
    {
        var result = await query.ExecuteAsync(parameters.MarketDataApi).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new TomasAI.IFM.Shared.EventSourcing.ServiceResult<FuturesOptionContractReadModel>(result)).ConfigureAwait(false);
    }
}
