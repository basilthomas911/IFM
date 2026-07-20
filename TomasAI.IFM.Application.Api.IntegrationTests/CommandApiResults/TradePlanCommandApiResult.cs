using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class TradePlanCommandApiResult
{
    public static Task FromUpdateTradePlanAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromUpdateIronCondorTradePlanAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromUpdateTradePlanForwardLossLimitAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromClearTradePlanForwardLossLimitAsync(HttpResponse resp)
        => resp.SetResult();
}
