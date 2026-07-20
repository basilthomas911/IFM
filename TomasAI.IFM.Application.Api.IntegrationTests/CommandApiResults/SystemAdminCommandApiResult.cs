using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class SystemAdminCommandApiResult
{
    public static Task FromBackupDatabaseAsync(HttpResponse resp)
        => resp.SetResult(Guid.NewGuid());
}
