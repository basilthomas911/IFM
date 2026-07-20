using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class OptionTradeCommandApiResult
{
    public static Task FromSnapshotAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromPlaceOrderAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromOpenOptionTradeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromCloseOptionTradeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertOptionTradeSpreadDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertOptionTradeSpreadBarDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteOptionTradeSpreadBarDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeOptionLegDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeDistributionStatisticsAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromProcessEndOfDayAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromUpdateTradeLimitDailyProfitTargetAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteOptionTradesAsync(HttpResponse resp)
        => resp.SetResult();
}
