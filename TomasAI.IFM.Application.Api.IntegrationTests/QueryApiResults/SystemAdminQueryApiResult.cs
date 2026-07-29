using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class SystemAdminQueryApiResult
{
    public static Task FromGetDatabaseNamesAsync(HttpResponse resp)
        => resp.SetResult(new DatabaseNamesReadModel
        {
            Names = [ "EventDb", "FundDb", "LogDb", "MarketDataDb", "OptionPricerDb", "ReferenceDb", "TradeDb" ]
        });
}
