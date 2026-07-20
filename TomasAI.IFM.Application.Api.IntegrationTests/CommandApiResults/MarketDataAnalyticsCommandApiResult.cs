using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class MarketDataAnalyticsCommandApiResult
{
    public static Task FromStartFuturesRsiSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopFuturesRsiSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesRsiSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesRsiDailySignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromUpdateFuturesTradeSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesTdiSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesItiSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromSetFuturesItiSignalHoldTradeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromClearFuturesItiSignalHoldTradeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesAtrSignalAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesAtrSignalFromIntraDayDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromGenerateFuturesAdxSignalAsync(HttpResponse resp)
        => resp.SetResult();
}
