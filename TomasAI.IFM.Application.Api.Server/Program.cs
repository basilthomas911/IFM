using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server;

try
{
    var builder = WebApplication.CreateBuilder(args);
    using var gcHistory = ApiServerProcess.RecordGc(builder.Configuration);
    if (!await ApiServerModes.RunBeforeBuildAsync(builder, args))
    {
        builder.ConfigureApiServer(out var logger);
        Startup.ConfigureRunMode(builder, args);
        builder.Services.RegisterServices(builder.Configuration, logger, builder.Environment);
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IApiServerLifecycle>().RunAsync(app, args, logger);
    }
}
catch (Exception exception)
{
    ApiServerProcess.ReportFailure(exception, args);
}
finally { ApiServerProcess.FlushLogs(); }

namespace TomasAI.IFM.Application.Api.Server
{
    /// <summary>Marker for the executable assembly used by integration hosts.</summary>
    public sealed class ApiServerEntryPoint;
}
