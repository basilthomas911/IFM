using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class ReferenceCommandApiResult
{
    public static Task FromAddEconomicCalendarAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveEconomicCalendarAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeEconomicCalendarAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromImportEconomicCalendarsAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddLookupTypeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveLookupTypeAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromChangeLookupTypeAsync(HttpResponse resp)
        => resp.SetResult();
}
