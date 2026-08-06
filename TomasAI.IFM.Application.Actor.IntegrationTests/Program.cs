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
await app.MapEventModelActorsAsync(logger);
try
{
    await app.RunAsync();
}
finally
{
    await app.Services
        .GetRequiredService<IActorSupervisor>()
        .ShutdownAsync(CancellationToken.None);
}


public partial class Program { } // Needed for WebApplicationFactory<Program>




