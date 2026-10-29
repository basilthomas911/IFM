using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Optional schema-specific availability; no quote or subscription ownership.</summary>
public interface IHistoricalProviderAvailability
{
    /// <summary>Reads the provider's exclusive published end for this exact schema.</summary>
    ValueTask<DateTimeOffset> GetAvailableEndAsync(HistoricalProviderRequest request, CancellationToken cancellationToken);
}
