using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class OptionPricerCommandApiResult
{
    public static Task FromInsertSpreadDistributionAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromSubmitSpreadDistributionJobAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromCompleteSpreadDistributionJobAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromFailSpreadDistributionJobAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromClearSpreadDistributionJobAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteSpreadDistributionJobsInProgressAsync(HttpResponse resp)
        => resp.SetResult();
}
