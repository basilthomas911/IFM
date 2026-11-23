namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Registers missing, provider-defined contracts through the Securities owner.</summary>
public interface ICurrentFuturesContractDiscovery
{
    Task EnsureEligibleAsync(string symbol, DateOnly valueDate, int count, CancellationToken cancellationToken);
}
