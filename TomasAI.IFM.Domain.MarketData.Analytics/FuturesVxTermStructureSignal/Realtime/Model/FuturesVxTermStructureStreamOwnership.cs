using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Model;

/// <summary>Owns the two live VX stream leases required by the term-structure workflow.</summary>
public sealed class FuturesVxTermStructureStreamOwnership
{
    static readonly TickerStreamOwner FrontOwner = new(
        "FuturesVxTermStructureSignal", "CurrentCurve", "Front");
    static readonly TickerStreamOwner BackOwner = new(
        "FuturesVxTermStructureSignal", "CurrentCurve", "Back");
    string? frontContractId;
    string? backContractId;

    /// <summary>Resolves and idempotently leases the startup-configured front/back contracts.</summary>
    public async ValueTask<FuturesTermStructureContracts> EnsureAsync(IMarketDataApi marketDataApi)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        if (!marketDataApi.TryGetFuturesTermStructureContracts("VX", out var pair) || !pair.IsValid)
            throw new FuturesContractRolloverConfigurationException(
                "The front and second VX contracts are not available in the startup registry.");
        var frontChanged = !StringComparer.Ordinal.Equals(frontContractId, pair.Front.ContractId);
        var backChanged = !StringComparer.Ordinal.Equals(backContractId, pair.Back.ContractId);
        var acquiredFront = false;
        var acquiredBack = false;
        try
        {
            if (frontChanged || !marketDataApi.IsTickDataStreamActive(pair.Front.ContractId))
            {
                _ = await marketDataApi.StartStreamingFuturesTickDataAsync(
                    pair.Front.ContractId, FrontOwner).ConfigureAwait(false);
                acquiredFront = true;
            }
            if (backChanged || !marketDataApi.IsTickDataStreamActive(pair.Back.ContractId))
            {
                _ = await marketDataApi.StartStreamingFuturesTickDataAsync(
                    pair.Back.ContractId, BackOwner).ConfigureAwait(false);
                acquiredBack = true;
            }
        }
        catch
        {
            if (acquiredBack)
                await TryReleaseAsync(marketDataApi, pair.Back.ContractId, BackOwner).ConfigureAwait(false);
            if (acquiredFront)
                await TryReleaseAsync(marketDataApi, pair.Front.ContractId, FrontOwner).ConfigureAwait(false);
            throw;
        }
        var oldFront = frontContractId;
        var oldBack = backContractId;
        frontContractId = pair.Front.ContractId;
        backContractId = pair.Back.ContractId;
        if (oldFront is not null && !StringComparer.Ordinal.Equals(oldFront, frontContractId))
            _ = await marketDataApi.StopStreamingFuturesTickDataAsync(oldFront, FrontOwner).ConfigureAwait(false);
        if (oldBack is not null && !StringComparer.Ordinal.Equals(oldBack, backContractId))
            _ = await marketDataApi.StopStreamingFuturesTickDataAsync(oldBack, BackOwner).ConfigureAwait(false);
        return pair;
    }

    /// <summary>Releases both actor-owned stream leases.</summary>
    public async ValueTask ReleaseAsync(IMarketDataApi marketDataApi)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        var front = frontContractId;
        var back = backContractId;
        frontContractId = null;
        backContractId = null;
        if (back is not null) await TryReleaseAsync(marketDataApi, back, BackOwner).ConfigureAwait(false);
        if (front is not null) await TryReleaseAsync(marketDataApi, front, FrontOwner).ConfigureAwait(false);
    }

    static async ValueTask TryReleaseAsync(IMarketDataApi api, string contractId, TickerStreamOwner owner)
    {
        try { _ = await api.StopStreamingFuturesTickDataAsync(contractId, owner).ConfigureAwait(false); }
        catch (MarketDataApiNotRunningException) { }
        catch (MarketDataContractNotFoundException) { }
    }
}
