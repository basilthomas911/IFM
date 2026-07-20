using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class FundCommandApiResult
{
    public static Task FromCreateFundAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddOrderToFundAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveOrderFromFundAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddTradeToFundOrderAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveTradeFromFundOrderAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromCloseFundOrderAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeFundOrderTradeStateAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromCreateFundTransactionAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromCreateFundTransactionsAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFundMaxProfitAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromProcessEndOfDayFundTransactionAsync(HttpResponse resp)
        => resp.SetResult();
}
