namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public interface IFmpMarketDataImportCoordinator
{
    Task<FmpMarketDataImportResult> ImportAsync(
        FmpMarketDataImportRequest request,
        CancellationToken cancellationToken = default);
}
