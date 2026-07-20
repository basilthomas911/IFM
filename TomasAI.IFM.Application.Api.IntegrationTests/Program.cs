using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Api.IntegrationTests;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapCommandApi();
app.MapQueryApi();
app.Run();

public partial class Program { } // Needed for WebApplicationFactory<Program>

public static class ResultHelper
{
    static readonly NewtonSoftJsonSerializer _serializer = new ();

    public static async Task SetResult(this HttpResponse resp)
    {
        var result = new ServiceOk<Guid>(Guid.NewGuid());
        var content = _serializer.Serialize(result);
        resp.StatusCode = (int)HttpStatusCode.OK;
        resp.ContentType = _serializer.ContentType;
        await resp.WriteAsync(content);
    }

    public static async Task SetResult<TResult>(this HttpResponse resp, TResult result)
    {
        var serviceResult = new ServiceOk<TResult>(result);
        var content = _serializer.Serialize(serviceResult);
        resp.StatusCode = (int)HttpStatusCode.OK;
        resp.ContentType = _serializer.ContentType;
        await resp.WriteAsync(content);
    }

}

