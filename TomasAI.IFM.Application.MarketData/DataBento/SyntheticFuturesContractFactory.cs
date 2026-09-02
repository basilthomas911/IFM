using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

internal static class SyntheticFuturesContractFactory
{
    public static FuturesContractV3ReadModel Create(
        DatabentoContractRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var parsed = new FuturesContractIdParser(registration.DomainContractId);
        var symbol = string.IsNullOrWhiteSpace(registration.RootSymbol)
            ? parsed.Symbol
            : registration.RootSymbol.Trim().ToUpperInvariant();
        if (!string.Equals(parsed.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new FuturesContractRolloverConfigurationException(
                $"The configured synthetic contract '{registration.DomainContractId}' does not match '{symbol}'.");
        }

        return new FuturesContractV3ReadModel(
            registration.DomainContractId,
            $"Synthetic {symbol} futures contract",
            symbol,
            registration.ProviderContractName,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            parsed.MaturityDate,
            registration.OnTheRun,
            registration.Rollover);
    }
}
