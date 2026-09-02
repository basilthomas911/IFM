using Serilog;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

try
{
    var bootstrapTradeStrategyFamiliesOnly = args.Contains(
        "--bootstrap-trade-strategy-families-only",
        StringComparer.OrdinalIgnoreCase);
    var builder = WebApplication.CreateBuilder(args);
    builder.ConfigureApiServer(out var logger);
    builder.Services.RegisterServices(builder.Configuration, logger);
    var app = builder.Build();
    app.ConfigureRequestPipeline(logger);
    app.MapApiCommands(logger);
    app.MapApiQueries(logger);
    if (bootstrapTradeStrategyFamiliesOnly)
    {
        // Deliberately avoid HTTP binding and actor startup. This narrow process mode
        // lets deployment/startup qualification race independent initializers against
        // the same ReferenceDb and PostgreSQL sequence infrastructure.
        await app.Services.GetRequiredService<TradeStrategyFamilyBootstrapper>().EnsureV1Async();
        Log.Information("TradeStrategyFamily bootstrap-only process completed.");
    }
    else
    {
        // Portfolio projections are rebuildable, but their idempotent schema must exist
        // before command actors can start durable projector workers.
        await app.Services.GetRequiredService<PortfolioSchemaDb>().CreateAllAsync();
        await app.Services.GetRequiredService<ReferenceSchemaDb>().CreateAllAsync();
        await app.Services.GetRequiredService<SequenceIdSchemaDb>().CreateAllAsync();
        await app.Services.GetRequiredService<SecuritiesSchemaDb>().CreateAllAsync();
        await app.Services.GetRequiredService<TradeStrategyFamilyBootstrapper>().EnsureV1Async();
        app.EnableServerManagerStandardInputShutdown(args, logger);
        // Bind the HTTP endpoint and start hosted infrastructure before exposing
        // any NATS actor subscriptions. If Kestrel cannot bind (for example, a
        // duplicate API host owns the port), no actor can consume messages from
        // a service provider that is immediately torn down.
        await app.StartAsync();
        var actorSupervisor = app.Services.GetRequiredService<IActorSupervisor>();
        var actorsStarted = false;
        try
        {
            await app.MapEventModelActorsAsync(logger);
            actorsStarted = true;
            await app.WaitForShutdownAsync();
        }
        finally
        {
            if (actorsStarted)
                await actorSupervisor.ShutdownAsync(CancellationToken.None);
        }
    }
}
catch (Exception ex)
{
    Environment.ExitCode = 1;
    Log.Fatal(ex, "IFM WebApiServer: startup failed");
}
finally
{
    Log.CloseAndFlush();
}

/*
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (HttpRequest request) =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
*/
