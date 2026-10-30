namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Explicit API-owned opening boundary; ordinary reads never advance the session date.</summary>
public interface IMarketOpenValueDateInitializer
{
    /// <summary>Resolves the opening date using the API's configured calendar and clock.</summary>
    DateOnly InitializeMarketOpen();
}
