using Microsoft.AspNetCore.Builder;
using TomasAI.IFM.Application.Actor.IntegrationTests;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureApiServer(out var logger);
builder.Services.RegisterServices(builder.Configuration, logger);
var app = builder.Build();
app.ConfigureRequestPipeline(logger);
app.MapApiCommands();
app.MapApiQueries();
app.MapEventModelActors(logger);
app.Run();


public partial class Program { } // Needed for WebApplicationFactory<Program>




