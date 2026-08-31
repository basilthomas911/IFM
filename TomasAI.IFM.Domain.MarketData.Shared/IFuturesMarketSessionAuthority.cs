using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Supplies the API server's single authoritative futures-session snapshot.
/// Consumers must not replace this decision with a locally calculated value date.
/// </summary>
public interface IFuturesMarketSessionAuthority
{
    /// <summary>Gets the newest coherent session snapshot owned by the API process.</summary>
    MarketSessionReadModel Current { get; }
}
