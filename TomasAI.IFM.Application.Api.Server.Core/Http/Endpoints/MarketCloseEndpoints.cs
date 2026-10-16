using TomasAI.IFM.Domain.SystemAdmin.MarketClose;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

public static class MarketCloseEndpoints
{
    public static void MapMarketClose(this WebApplication app)
    {
        var endpoint = app.MapPost("/api/market-data/close", async (MarketCloseCoordinator close, IHostApplicationLifetime lifetime) =>
        {
            // A task/client disconnect must not interrupt financial EOD after it has begun.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            try { return Results.Ok(await close.CloseMarketAsync(deadline.Token).ConfigureAwait(false)); }
            catch (Exception error) { return Results.Problem(detail: error.Message, title: "Market Close failed", statusCode: StatusCodes.Status409Conflict); }
        });
        if (app.Environment.IsDevelopment()) endpoint.AllowAnonymous();
        else endpoint.RequireAuthorization();
    }
}
