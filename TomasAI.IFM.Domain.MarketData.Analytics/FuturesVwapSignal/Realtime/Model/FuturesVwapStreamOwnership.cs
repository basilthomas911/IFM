using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Model;

/// <summary>Owns the configured current futures-contract trade stream lease.</summary>
public sealed class FuturesVwapStreamOwnership
{
    static readonly TickerStreamOwner Owner = new("FuturesVwapSignal", "CurrentSession", "Trades");
    string? contractId;

    /// <summary>Resolves and idempotently leases the configured current contract.</summary>
    public async ValueTask<FuturesContractV2ReadModel> EnsureAsync(
        IMarketDataApi marketDataApi, string rootSymbol)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSymbol);
        if (!marketDataApi.TryGetCurrentlyTradedFuturesContract(rootSymbol, out var contract))
            throw new FuturesContractRolloverConfigurationException(
                $"The current {rootSymbol} futures contract is unavailable for VWAP.");
        var changed = !StringComparer.Ordinal.Equals(contractId, contract.ContractId);
        if (changed || !marketDataApi.IsTickDataStreamActive(contract.ContractId))
            _ = await marketDataApi.StartStreamingFuturesTickDataAsync(
                contract.ContractId, Owner).ConfigureAwait(false);
        var previous = contractId;
        contractId = contract.ContractId;
        if (previous is not null && !StringComparer.Ordinal.Equals(previous, contractId))
            await TryReleaseAsync(marketDataApi, previous).ConfigureAwait(false);
        return contract;
    }

    /// <summary>Releases the actor-owned current-contract stream lease.</summary>
    public async ValueTask ReleaseAsync(IMarketDataApi marketDataApi)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        var current = contractId;
        contractId = null;
        if (current is not null) await TryReleaseAsync(marketDataApi, current).ConfigureAwait(false);
    }

    static async ValueTask TryReleaseAsync(IMarketDataApi api, string current)
    {
        try { _ = await api.StopStreamingFuturesTickDataAsync(current, Owner).ConfigureAwait(false); }
        catch (MarketDataApiNotRunningException) { }
        catch (MarketDataContractNotFoundException) { }
    }
}
