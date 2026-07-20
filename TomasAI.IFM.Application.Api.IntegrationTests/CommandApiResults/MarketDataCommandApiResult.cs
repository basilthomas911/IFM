using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class MarketDataCommandApiResult
{
    public static Task FromAddFuturesContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeFuturesContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveFuturesContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddFuturesOptionContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddFuturesOptionContractsAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeFuturesOptionContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveFuturesOptionContractAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddYieldCurveRateAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeYieldCurveRateAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveYieldCurveRateAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromImportYieldCurveRatesAsync(HttpResponse resp)
        => resp.SetResult();
}
