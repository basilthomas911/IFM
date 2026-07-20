using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class TradePlacementCommandApiResult
{
    public static Task FromSignalTradePlacementAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStartTradePlacementAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopTradePlacementAsync(HttpResponse resp)
        => resp.SetResult();
}
