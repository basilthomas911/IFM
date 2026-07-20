using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class ApplicationCommandApiResult
{
    public static Task FromStartAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromShutdownAsync(HttpResponse resp)
        => resp.SetResult();
}