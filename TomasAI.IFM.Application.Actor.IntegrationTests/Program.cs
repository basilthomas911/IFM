using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureApiServer(out var logger);
builder.Services.RegisterServices(builder.Configuration, logger);
var app = builder.Build();
app.ConfigureRequestPipeline(logger);
var isolatedQuoteSoak = Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK") == "true";
if (isolatedQuoteSoak)
    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaDb>().CreateAllAsync();
else
{
    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb>().CreateAllAsync();
    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb>().CreateAllAsync();
}
await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.EventSourceDb.Schema.EventSourceSchemaDb>().CreateAllAsync();
var actorLifecycle = app.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
var supervisorBootstrap = app.Services.GetRequiredService<ISupervisorBootstrap>();
bool actorsStarted = false;
try
{
    await app.MapEventModelActorsAsync(logger);
    actorsStarted = true;
    if (isolatedQuoteSoak)
    {
        await app.StartAsync();
        try
        {
            await new TickQuoteSoakRunner(app.Services).RunAsync(app.Lifetime.ApplicationStopping);
        }
        finally
        {
            await app.StopAsync();
        }
    }
    else
        await app.RunAsync();
}
catch (Exception exception) when (isolatedQuoteSoak)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
finally
{
    if (actorsStarted)
    {
        var shutdown = await actorLifecycle.ShutdownActorsAsync(CancellationToken.None);
        if (!shutdown.Succeeded)
            logger.LogError(
                "Supervisor actor shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                shutdown.OperationId, shutdown.Outcome, shutdown.Stage, shutdown.FailureReason);
        var supervisorShutdown = await supervisorBootstrap.StopSupervisorAsync(CancellationToken.None);
        if (!supervisorShutdown.Succeeded)
            logger.LogError(
                "Supervisor bootstrap shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                supervisorShutdown.OperationId, supervisorShutdown.Outcome,
                supervisorShutdown.Stage, supervisorShutdown.FailureReason);
    }
}


public partial class Program { } // Needed for TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<Program>




