using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

namespace TomasAI.IFM.Application.Api.Server;

public static class FmpMarketDataEndpoints
{
    public static IEndpointRouteBuilder MapFmpMarketDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                FmpMarketDataRoutes.Import,
                async (
                    FmpMarketDataImportRequest request,
                    IFmpMarketDataImportCoordinator coordinator,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await coordinator.ImportAsync(request, cancellationToken)))
            .RequireAuthorization()
            .WithName("ImportFinancialModelingPrepMarketData")
            .WithTags("Market Data")
            .Produces<FmpMarketDataImportResult>();
        return endpoints;
    }
}
