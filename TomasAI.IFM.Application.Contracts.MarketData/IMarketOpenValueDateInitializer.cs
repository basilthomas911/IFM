namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Compatibility entry point for reconciling the API-owned opening date; ordinary reads also reconcile it.</summary>
public interface IMarketOpenValueDateInitializer
{
    /// <summary>Resolves the opening date using the API's configured calendar and clock.</summary>
    DateOnly InitializeMarketOpen();
}
