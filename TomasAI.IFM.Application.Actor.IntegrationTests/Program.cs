using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureApiServer(out var logger);
builder.Services.RegisterServices(builder.Configuration, logger);
var app = builder.Build();
app.ConfigureRequestPipeline(logger);
app.MapApiCommands();
app.MapApiQueries();
var isolatedQuoteSoak = Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK") == "true";
if (isolatedQuoteSoak)
    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaDb>().CreateAllAsync();
else
    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb>().CreateAllAsync();
var actorSupervisor = app.Services.GetRequiredService<IActorSupervisor>();
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
        await actorSupervisor.ShutdownAsync(CancellationToken.None);
}


public partial class Program { } // Needed for WebApplicationFactory<Program>




